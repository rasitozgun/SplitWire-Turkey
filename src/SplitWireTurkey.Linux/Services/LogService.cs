using System;
using System.IO;
using System.Text;

namespace SplitWireTurkey.Linux.Services;

/// <summary>Small append-only logger writing to ~/.local/share/SplitWire-Turkey/Logs.</summary>
public static class LogService
{
    private static readonly object Gate = new();

    public static string CurrentLogFile
    {
        get
        {
            return Path.Combine(Paths.LogDir, $"SplitWire-Turkey-{DateTime.Now:yyyy-MM-dd}.log");
        }
    }

    public static void Info(string component, string message) => Write("INFO", component, message);
    public static void Warn(string component, string message) => Write("WARN", component, message);
    public static void Error(string component, string message) => Write("ERROR", component, message);

    /// <summary>Raised for every log line so the UI log pane can mirror it.</summary>
    public static event EventHandler<string>? LineAdded;

    private static void Write(string level, string component, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] [{component}] {message}";
        try
        {
            lock (Gate)
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }

        try
        {
            LineAdded?.Invoke(null, line);
        }
        catch
        {
            // UI may not be present (headless/unit tests).
        }
    }
}
