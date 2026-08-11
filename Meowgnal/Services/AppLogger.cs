using System;
using System.IO;

namespace Meowgnal.Services;

/// <summary>Minimal local file logger for diagnostics. Never logs sensitive data.</summary>
public static class AppLogger
{
    private static readonly object Lock = new();

    private static string LogFile =>
        Path.Combine(AppPaths.AppDataFolder, "logs", "app.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message} :: {ex.Message}");

    public static void Fatal(string message, Exception? ex) =>
        Write("FATAL", $"{message} :: {ex?.Message} :: {ex?.StackTrace}");

    private static void Write(string level, string text)
    {
        try
        {
            lock (Lock)
            {
                var dir = Path.GetDirectoryName(LogFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] [{level}] {text}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // The logger must never crash the app.
        }
    }
}