using System;
using System.IO;
using System.Text;

namespace DownTrack.Core.Logging;

public static class AppLogger
{
    private static readonly object LockObj = new();
    private static string? _logFilePath;

    public static void Initialize(string appName = "DownTrack")
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                appName,
                "logs"
            );

            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"{appName}_{DateTime.UtcNow:yyyy-MM-dd}.log");
            
            Info($"=== {appName} Session Started at {DateTime.UtcNow:O} UTC ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to initialize logger: {ex.Message}");
        }
    }

    public static void Info(string message) => WriteEntry("INFO", message);
    public static void Warn(string message) => WriteEntry("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var text = ex != null ? $"{message} | Exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}" : message;
        WriteEntry("ERROR", text);
    }

    private static void WriteEntry(string level, string message)
    {
        var line = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{level}] {message}";
        Console.WriteLine(line);

        if (string.IsNullOrEmpty(_logFilePath)) return;

        lock (LockObj)
        {
            try
            {
                File.AppendAllText(_logFilePath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // Never crash the main app if logging fails
            }
        }
    }
}
