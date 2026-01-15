using DocumentSearchRagDotNetAI.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using System.Text.RegularExpressions;
using static DocumentSearchRagDotNetAI.Utils.ProgramLogger;

LogInfo("[SETUP] Iniciando aplicação RAG (Retrieval-Augmented Generation) para consulta de base de documentos corporativa.");

// Configuração do modelo de LLM (Large Language Model) para geração de texto e do modelo de Embeddings para vetorização.
var config = new OllamaConfig
{
    Endpoint = "http://localhost:11434", // Endpoint local do serviço Ollama
    TextModel = new OllamaModelConfig("deepseek-r1:8b", 131072), // Modelo LLM (DeepSeek). Contexto de 131k tokens permite processar prompts extensos.
    //TextModel = new OllamaModelConfig("phi3:mini" , 131072),
    //TextModel = new OllamaModelConfig("phi3:mini" , 4096),
    //EmbeddingModel = new OllamaModelConfig("nomic-embed-text", 768) // Modelo de embedding anterior (768 dimensões)
    EmbeddingModel = new OllamaModelConfig("bge-m3", 1024) // Modelo de Embeddings atual (bge-m3). Gera vetores de 1024 dimensões para alta precisão semântica.
};


// Inicializa a construção do KernelMemory integrando com Ollama.
// O KernelMemory abstrai a complexidade de ingestão, armazenamento vetorial e recuperação de informações.
var memoryBuilder = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)    // Configura o gerador de texto (LLM)
    .WithOllamaTextEmbeddingGeneration(config) // Configura o gerador de embeddings (Vetorização)
    .WithCustomTextPartitioningOptions(new TextPartitioningOptions
    {
        // Configuração de Chunking (Particionamento de texto):
        // Divide os documentos em pedaços menores para indexação e busca vetorial.
        
        //MaxTokensPerParagraph = 256, 
        //OverlappingTokens = 32 
        
        MaxTokensPerParagraph = 120, // Tamanho máximo do chunk em tokens. Chunks menores focam em conceitos específicos, melhorando a precisão da busca.
        OverlappingTokens = 30 // Sobreposição de tokens entre chunks adjacentes para preservar o contexto nas quebras de texto.
    });


// Configuração do cliente HTTP para evitar timeouts em operações longas (como ingestão de grandes arquivos ou inferência de modelos pesados).
memoryBuilder.Services.ConfigureHttpClientDefaults(config =>
{
    config.ConfigureHttpClient(options =>
    {
        options.Timeout = TimeSpan.FromMinutes(5); // Define timeout de 5 minutos (padrão costuma ser 100s e gerava exception).
    });
});

var kernelMemory = memoryBuilder.Build();


LogInfo("Iniciando processo de ingestão e vetorização de documentos...");

// --- Ingestão de Dados (Indexing) ---
// Lê os arquivos do diretório 'Files', processa o texto, gera embeddings e armazena na memória vetorial.

var documentsFiles = DocumentService.GetAllTxtDocumentsFromDirectoryPath("Files");


foreach (var documentFilePath in documentsFiles)
{
    try
    {
        var fileExists = File.Exists(documentFilePath);

        var fileInfo = new FileInfo(documentFilePath);
        var fileName = fileInfo.Name;

        // Normalização do nome do arquivo para garantir um DocumentId válido no sistema.
        // Substitui caracteres não alfanuméricos por sublinhado.
        var parsedFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9]", "_");

        if (!fileExists)
            throw new FileNotFoundException($"Arquivo não encontrado: {documentFilePath}");

        // Importa o documento para o KernelMemory:
        // 1. Extração de texto.
        // 2. Particionamento (Chunking).
        // 3. Geração de Embeddings.
        // 4. Armazenamento vetorial.
        await kernelMemory.ImportDocumentAsync(
            filePath: documentFilePath,
            documentId: parsedFileName,
            tags: new TagCollection
            {
                { "tipo", "politica" }, // Metadados para filtragem posterior
                { "departamento", "rh" },
                { "fonte", "interna" }
            });

        LogSuccess($"[INGEST] [SUCESSO] Documento indexado: '{parsedFileName}'");
    }
    catch (Exception ex)
    {
        LogError($"[INGEST] [ERRO] Falha na ingestão do arquivo '{documentFilePath}': {ex.Message}");
        LogError(ex.StackTrace ?? "Stacktrace indisponível");
        throw;
    }
}



LogInfo("[SETUP] Sistema RAG inicializado e pronto para processar consultas.");

while (true)
{
    LogInfo("[INTERFACE] Digite sua pergunta sobre as políticas da empresa (ou 'sair' para encerrar):");

    var question = Console.ReadLine();

    var isInputToStop = string.Equals(question, "sair", StringComparison.OrdinalIgnoreCase);
    var isInputEmpty = string.IsNullOrWhiteSpace(question);

    if (isInputToStop)
        break;

    if (isInputEmpty)
        continue;

    // Prompt Engineering: Definição da "persona" e regras estritas para o modelo.
    // O objetivo é evitar alucinações (respostas fora do contexto) e manter o tom corporativo.
    var securePrompt = $"""
        Você é um assistente corporativo.

        INSTRUÇÕES OBRIGATÓRIAS:
        - Responda SOMENTE com base no CONTEXTO fornecido.
        - NÃO utilize conhecimento externo.
        - NÃO faça suposições.
        - Se não houver informação suficiente, responda EXATAMENTE:
          "Desculpe, não tenho essa informação no momento."

        FORMATO DA RESPOSTA:
        - Resposta curta e objetiva
        - No máximo 5 linhas

        PERGUNTA:
        {question}
    """;


    // Monitoramento de latência da resposta.
    var startTime = DateTime.UtcNow;


    // Execução do fluxo RAG (Retrieval-Augmented Generation):
    // 1. Embedding: A pergunta do usuário é convertida em vetor.
    // 2. Retrieval (Busca): O sistema busca na memória os chunks (trechos) mais similares semanticamente à pergunta.
    //    - Filtro aplicado: Apenas documentos com tags tipo=politica e departamento=rh.
    // 3. Augmentation (Enriquecimento): Os chunks encontrados são anexados ao prompt como contexto.
    // 4. Generation (Geração): O LLM gera a resposta baseada apenas nesse contexto enriquecido.
    LogInfo($"[RAG] Processando pergunta: \"{question}\"...");

    var response = await kernelMemory.AskAsync(
        securePrompt,
        filter: new MemoryFilter()
            .ByTag("tipo", "politica")
            .ByTag("departamento", "rh")
    );

    var endTime = DateTime.UtcNow;

    var duration = endTime - startTime;

    LogInfo($"[PERFORMANCE] Tempo de processamento: {duration.TotalSeconds:F2} segundos");

    LogAIResponse(response.Result);

    if (response.RelevantSources.Count == 0)
    {
        LogWarning("[RAG] Nenhum contexto relevante encontrado na base de documentos.");
    }
    else
    {
        LogInfo("\n[RAG] --- Contexto Recuperado (RAG Retrieval) ---");

        // Ordena as fontes pela relevância do primeiro chunk encontrado
        var relevantSourceOrderedByRelevant = response.RelevantSources
            .OrderByDescending(source => source.Partitions.FirstOrDefault()?.Relevance)
            .ToList();

        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (var source in response.RelevantSources)
        {
            // Exibe a fonte e o grau de similaridade (Relevance Score)
            Log($"[SOURCE] ID: {source.DocumentId} | Arquivo: '{source.SourceName}' | Relevância: {source.Partitions.FirstOrDefault()?.Relevance:f5}");
            
            // source.Partitions.ForEach(partition => Log($"\n- {partition.Text}")); 
            // Melhorando a formatação dos trechos
            foreach(var partition in source.Partitions)
            {
                 Log($"-- Trecho: {partition.Text}");
            }
            Log("--------------------------------------------------");
        }
        Console.ForegroundColor = previousColor;
    }

    // Calcula a relevância média dos trechos recuperados para métricas de qualidade
    var avgRelevance = response.RelevantSources
    .SelectMany(s => s.Partitions)
    .Average(p => p.Relevance);

    LogInfo($"[METRICS] Relevância média dos trechos: {avgRelevance:F4}\n");

}
;

string Normalize(string text) =>
    Regex.Replace(text, @"\s+", " ").Trim();
