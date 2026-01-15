using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentSearchRagDotNetAI.Utils;

/// <summary>
/// Classe utilitária para centralizar e padronizar o logging da aplicação.
/// Configura o ILogger e fornece métodos estáticos para diferentes níveis de log.
/// </summary>
public static class ProgramLogger
{
    private static readonly ILogger _logger;

    static ProgramLogger()
    {
        // Configura a fábrica de logs para saída no console com nível mínimo de informação
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });
        
        // Obtém o nome do assembly atual para usar como categoria do log
        var assembly = Assembly.GetExecutingAssembly().GetName().Name ?? "Program";

        var logger = loggerFactory.CreateLogger(assembly);

        _logger = logger;
    }

    /// <summary>
    /// Registra uma mensagem simples no console (stdout).
    /// </summary>
    public static void Log(string message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// Registra uma mensagem de informação (Info level) via ILogger.
    /// </summary>
    public static void LogInfo(string message)
    {
        _logger.LogInformation(message);
    }

    /// <summary>
    /// Registra uma mensagem de sucesso com destaque visual (cor verde).
    /// </summary>
    public static void LogSuccess(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        _logger.LogInformation(message);
        Console.ForegroundColor = previousColor;
    }

    /// <summary>
    /// Registra uma mensagem de erro (Error level) via ILogger.
    /// </summary>
    public static void LogError(string message)
    {
        _logger.LogError(message);
    }


    /// <summary>
    /// Exibe a resposta gerada pela IA com destaque visual (cor amarela).
    /// </summary>
    public static void LogAIResponse(string message)
    {
        var previousColor = Console.ForegroundColor;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\n --- Resposta da IA ---");

        Console.WriteLine(message);

        Console.WriteLine("\n ---------------------");
        Console.ForegroundColor = previousColor;
    }


}
