using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DocumentSearchRagDotNetAI.Utils;

public static class ProgramLogger
{
    private static readonly ILogger _logger;

    static ProgramLogger()
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .SetMinimumLevel(LogLevel.Information);
        });
        var assembly = Assembly.GetExecutingAssembly().GetName().Name ?? "Program";

        var logger = loggerFactory.CreateLogger(assembly);

        _logger = logger;
    }

    public static void Log(string message)
    {
        Console.WriteLine(message);
    }

    public static void LogInfo(string message)
    {
        _logger.LogInformation(message);
    }

    public static void LogSuccess(string message)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Green;
        _logger.LogInformation(message);
        Console.ForegroundColor = previousColor;
    }

    public static void LogError(string message)
    {
        _logger.LogError(message);
    }


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
