// See https://aka.ms/new-console-template for more information
using DocumentSearchRagDotNetAI.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.Configuration;
using System.Text.RegularExpressions;
using static DocumentSearchRagDotNetAI.Utils.ProgramLogger;

LogInfo("Consultando uma base de documentos de uma Empresa via RAG");

// Configuração do modleo LLM e gerador de Embedding
var config = new OllamaConfig
{
    Endpoint = "http://localhost:11434", // endpoint do ollama local
    TextModel = new OllamaModelConfig("deepseek-r1:8b", 131072), // modelo da LLM da deepsek, limite máximo de toknes -> 100 páginas de 
    //TextModel = new OllamaModelConfig("phi3:mini" , 131072), // modelo da LLM da deepsek, limite máximo de toknes -> 100 páginas de 
    //TextModel = new OllamaModelConfig("phi3:mini" , 4096),
    EmbeddingModel = new OllamaModelConfig("nomic-embed-text", 768) // modelo que gera embeddings, -> 768 dimensões -> dimensão dos embeddings
};


// Cria a configuração do KernelMemory com o Ollama para geração de texto e embeddings
// Usa o KernelMemoryBuilder com configuração explícita
var memoryBuilder = new KernelMemoryBuilder()
    .WithOllamaTextGeneration(config)
    .WithOllamaTextEmbeddingGeneration(config)
    .WithCustomTextPartitioningOptions(new TextPartitioningOptions
    {
        //MaxTokensPerParagraph = 256, // define o tamanho máximo dos tokens para geração de embeddings, -> chunks de até 256 tokens
        //OverlappingTokens = 32 // define a sobreposição entre os chunks para melhorar a coesão dos embeddings
        MaxTokensPerParagraph = 180, // Isso reduz a chance do modelo “conectar” conceitos que não estão no mesmo texto.
        OverlappingTokens = 40
    });


memoryBuilder.Services.ConfigureHttpClientDefaults(config =>
{
    config.ConfigureHttpClient(options =>
    {
        options.Timeout = TimeSpan.FromMinutes(5); // aumenta o timeout para 5 minutos
    });
});

var kernelMemory = memoryBuilder.Build();


LogInfo("Inciando a ingestão de Documentos...");
// Ingestão de documentos na base de conhecimento
// --- 2. Ingestão de Dados ---

var documentsFiles = DocumentService.GetAllTxtDocumentsFromDirectoryPath("Files");


foreach (var documentFilePath in documentsFiles)
{
    try
    {
        var fileExists = File.Exists(documentFilePath);

        var fileInfo = new FileInfo(documentFilePath);
        var fileName = fileInfo.Name;

        // limpar caracteres especiais do nome do arquivo para usar como documentId

        var parsedFileName = Regex.Replace(fileName, @"[^a-zA-Z0-9]", "_");

        if (!fileExists)
            throw new FileNotFoundException($"Arquivo não encontrado: {documentFilePath}");

        var x = await kernelMemory.ImportDocumentAsync(
            filePath: documentFilePath,
            documentId: parsedFileName);

        LogSuccess($"Ingestão do documento '{parsedFileName}' concluída com sucesso!");
    }
    catch (Exception ex)
    {
        LogError($"Erro durante a ingestão do arquivo: {ex.Message}");
        LogError(ex.StackTrace ?? "unknown stacktrace");
        throw;
    }
}



LogInfo("Modelo pronto para perguntas");

while (true)
{
    LogInfo("Faça uma pergunta sobre as políticas da empresa (ou digite 'sair' para encerrar):");

    var input = Console.ReadLine();

    var isInputToStop = string.Equals(input, "sair", StringComparison.OrdinalIgnoreCase);
    var isInputEmpty = string.IsNullOrWhiteSpace(input);

    if (isInputToStop)
        break;

    if (isInputEmpty)
        continue;

    var securePrompt = $"""
        Você é um assistente que responde APENAS com base no conteúdo fornecido no CONTEXTO.

        REGRAS OBRIGATÓRIAS:
        - Use somente informações que estejam explicitamente no contexto.
        - NÃO use conhecimento prévio.
        - NÃO faça inferências.
        - NÃO complete informações ausentes.
        - Se a resposta não estiver claramente no contexto, responda exatamente:
          "Desculpe, não tenho essa informação no momento."

        Pergunta:
        {input}
        """;


    // timer pra verificar o tempo que leva pra fazer a pergunta
    var startTime = DateTime.UtcNow;


    // O AskAsync faz parte do RAG:
    // 1. Gera embeddings da pergunta
    // 2. Busca os chunks mais relevantes na base de conhecimento
    // 3. Envia o contexto + pergunta para a LLM gerar a resposta
    var response = await kernelMemory.AskAsync(securePrompt);

    var endTime = DateTime.UtcNow;

    var duration = endTime - startTime;

    Console.WriteLine($"\nTempo para resposta: {duration.TotalSeconds} segundos");

    LogAIResponse(response.Result);

    if (response.RelevantSources.Count == 0)
    {
        Log("Nenhuma fonte relevante encontrada.");
    }
    else
    {
        Log("\n --- Contexto da IA ---");

        var relevantSourceOrderedByRelevant = response.RelevantSources
            .OrderByDescending(source => source.Partitions.FirstOrDefault()?.Relevance)
            .ToList();

        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Cyan;
        foreach (var source in response.RelevantSources)
        {
            Log($"\n--> Fonte Relevante ({source.DocumentId}) arquivo: '{source.SourceName}' --> Relevância: {source.Partitions.FirstOrDefault()?.Relevance:f5}");
            //Log($"\nTrechos:");
            source.Partitions.ForEach(partition => Log($"\n- ${partition.Text}"));
            Log("\n ---------------------");
        }
        Console.ForegroundColor = previousColor;
    }

}
;