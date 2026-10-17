using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using LogService = SplitWireTurkey.Linux.Services.LogService;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Checks the fork's "linux" GitHub release for a newer build and notifies the
/// user with a small dialog (download → opens the release page in the browser,
/// skip → remembered for this version). Version comes from the release title
/// ("... build 1.5.6") compared against the compiled app version below.
/// </summary>
public static class UpdateService
{
    public const string CurrentVersion = "1.5.6";
    private const string Repo = "rasitozgun/SplitWire-Turkey";
    private const string ReleaseTag = "linux";
    private const string AssetName = "SplitWire-Turkey-Linux-x64.tar.gz";
    public const string ReleasesPage = $"https://github.com/{Repo}/releases/tag/{ReleaseTag}";

    public sealed record UpdateResult(bool HasUpdate, string LatestVersion, string ReleaseUrl, string DownloadUrl);

    private static string StateFile => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SplitWire-Turkey", "update-state.json");

    private static readonly HttpClient Http = NewClient();

    private static HttpClient NewClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("SplitWire-Turkey-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>Fire-and-forget startup check: waits a few seconds, then dialogs if new.</summary>
    public static void CheckAndNotify(Window? owner) => _ = CheckAndNotifyCoreAsync(owner);

    private static async Task CheckAndNotifyCoreAsync(Window? owner)
    {
        try
        {
            await Task.Delay(4000);
            var r = await CheckAsync();
            if (r is null)
            {
                return;
            }
            LogService.Info("Update", $"Release version: {r.LatestVersion} (local: {CurrentVersion})");
            if (!r.HasUpdate) return;
            if (r.LatestVersion == ReadSkipVersion()) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                try { ShowDialog(owner, r); }
                catch (Exception ex) { LogService.Warn("Update", $"Dialog failed: {ex.Message}"); }
            });
        }
        catch (Exception ex)
        {
            LogService.Warn("Update", $"Update check failed: {ex.Message}");
        }
    }

    /// <summary>Queries the release API. Returns null when offline / no usable info.</summary>
    public static async Task<UpdateResult?> CheckAsync()
    {
        try
        {
            using var resp = await Http.GetAsync($"https://api.github.com/repos/{Repo}/releases/tags/{ReleaseTag}").ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                LogService.Warn("Update", $"GitHub API returned {resp.StatusCode}; update check skipped.");
                return null;
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync().ConfigureAwait(false));
            var root = doc.RootElement;

            var title = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
            var tag = root.TryGetProperty("tag_name", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            var page = root.TryGetProperty("html_url", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString()! : ReleasesPage;

            string? download = null;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                download = assets.EnumerateArray()
                    .Where(a => a.TryGetProperty("name", out var an) &&
                                string.Equals(an.GetString(), AssetName, StringComparison.OrdinalIgnoreCase))
                    .Select(a => a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null)
                    .FirstOrDefault(u => u is not null);
            }

            var m = Regex.Match($"{title} {tag}", @"\d+\.\d+(\.\d+)?");
            if (!m.Success) return null;

            var latest = m.Value;
            var hasUpdate = Version.TryParse(latest, out var v1) &&
                            Version.TryParse(CurrentVersion, out var v2) &&
                            v1 > v2;
            return new UpdateResult(hasUpdate, latest, page, download ?? page);
        }
        catch (Exception ex)
        {
            LogService.Warn("Update", $"Check request failed: {ex.Message}");
            return null;
        }
    }

    private static void ShowDialog(Window? owner, UpdateResult r)
    {
        var panel = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 12,
        };

        panel.Children.Add(new TextBlock
        {
            Text = "New version available!  ·  Yeni sürüm mevcut!",
            FontWeight = FontWeight.Bold,
            FontSize = 17,
        });

        panel.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Text = $"SplitWire-Turkey {r.LatestVersion} is available" +
                   $" (installed version: {CurrentVersion}).\n\n" +
                   "Open the download page, then follow the usual install steps.",
        });

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
        };
        panel.Children.Add(buttons);

        var win = new Window
        {
            Title = "SplitWire-Turkey — Update",
            Width = 430,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = panel,
        };

        var download = new Button { Content = "Download", Padding = new Avalonia.Thickness(16, 6) };
        download.Click += (_, _) =>
        {
            OpenBrowser(r.DownloadUrl);
            win.Close();
        };
        buttons.Children.Add(download);

        var skip = new Button { Content = "Skip this version", Padding = new Avalonia.Thickness(16, 6) };
        skip.Click += (_, _) =>
        {
            WriteSkipVersion(r.LatestVersion);
            win.Close();
        };
        buttons.Children.Add(skip);

        if (owner is { IsVisible: true })
        {
            _ = win.ShowDialog(owner);
        }
        else
        {
            win.Show();
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = true });
        }
        catch
        {
            try { Process.Start("/usr/bin/xdg-open", url); }
            catch (Exception ex) { LogService.Warn("Update", $"Cannot open browser: {ex.Message}"); }
        }
    }

    private static string? ReadSkipVersion()
    {
        try
        {
            using var d = JsonDocument.Parse(File.ReadAllText(StateFile));
            if (d.RootElement.TryGetProperty("skipVersion", out var v) && v.ValueKind == JsonValueKind.String)
            {
                return v.GetString();
            }
        }
        catch { }
        return null;
    }

    private static void WriteSkipVersion(string version)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
            File.WriteAllText(StateFile, "{\"skipVersion\":\"" + version + "\"}");
        }
        catch { }
    }
}
