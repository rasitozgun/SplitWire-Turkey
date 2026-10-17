using System;
using System.IO;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Central filesystem locations for the Linux edition.
/// No Windows registry: settings live under ~/.config, logs under ~/.local/share.
/// </summary>
public static class Paths
{
    public static string Home { get; } =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static string BaseDir { get; } = AppContext.BaseDirectory;

    /// <summary>Folder next to the binary holding tool binaries and languages (res/).</summary>
    public static string ResDir { get; } = Path.Combine(BaseDir, "res");

    public static string LanguagesDir { get; } = Path.Combine(ResDir, "Languages");

    /// <summary>Downloaded/config working folder for installed tools.</summary>
    public static string WorkingDir { get; } = Path.Combine(ResDir, "appdata-linux");

    public static string ConfigDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SplitWire-Turkey");

    public static string SettingsFile { get; } = Path.Combine(ConfigDir, "settings.json");

    public static string LogDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SplitWire-Turkey", "Logs");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(ResDir);
        Directory.CreateDirectory(WorkingDir);
        Directory.CreateDirectory(ConfigDir);
        Directory.CreateDirectory(LogDir);
    }
}
