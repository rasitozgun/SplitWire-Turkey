using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

namespace SplitWireTurkey.Linux.Services;

public record WireGuardStatus(bool ConfigExists, string ProfilePath, string AllowedApps);

/// <summary>
/// Linux counterpart of the Windows WireGuardService: wgcf (Cloudflare WARP
/// account -> WireGuard profile) plus the system 'wg-quick' tunnel manager.
/// The WireSock split-tunnel client has no Linux edition, so app filtering is
/// emulated with wg-quick's PostUp/PreDown nft routing when specific apps are
/// requested (see ApplyAllowedAppsAsync).
/// </summary>
public class WgcfService
{
    private readonly ProcessRunner _runner;
    private readonly HttpClient _http;

    public const string WgcfBinary = "res/wgcf";
    public const string ProfileFile = "res/wgcf-profile.conf";
    public const string UnitName = "splitwire-wireguard.service";

    public WgcfService(ProcessRunner runner, HttpClient http)
    {
        _runner = runner;
        _http = http;
    }

    public string ProfilePath => Path.Combine(Paths.BaseDir, ProfileFile);
    public string WgcfPath => Path.Combine(Paths.BaseDir, WgcfBinary);

    public bool ProfileExists => File.Exists(ProfilePath);

    /// <summary>Downloads the official wgcf linux-amd64 binary if missing.</summary>
    public async Task EnsureWgcfAsync(CancellationToken ct = default)
    {
        if (File.Exists(WgcfPath))
        {
            ProcessRunner.EnsureExecutable(WgcfPath);
            return;
        }

        var url = await GitHubReleaseHelper
            .GetLatestAssetUrlAsync(_http, "ViRb3/wgcf", "linux.*amd64", ct)
            .ConfigureAwait(false);
        await DownloadAsync(url, WgcfPath, ct).ConfigureAwait(false);
        ProcessRunner.EnsureExecutable(WgcfPath);
    }

    /// <summary>Registers a WARP account (/ wgcf register) and generates the profile.
    /// wgcf keeps its account database (wgcf-account.toml) in the CWD, so both
    /// invocations are scoped to the res/ directory.</summary>
    public async Task RegisterAsync(CancellationToken ct = default)
    {
        await EnsureWgcfAsync(ct).ConfigureAwait(false);
        var workDir = Path.GetDirectoryName(WgcfPath)!;
        var reg = await _runner.RunAsync(WgcfPath, "register --accept-tos", ct, workDir).ConfigureAwait(false);
        if (!reg.Success && !reg.StdOut.Contains("already registered", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"wgcf register failed: {reg.StdErr}{reg.StdOut}");
        }

        var gen = await _runner.RunAsync(WgcfPath, $"generate -p {ProcessRunner.QuoteShell(ProfilePath)}", ct, workDir).ConfigureAwait(false);
        if (!gen.Success)
        {
            throw new InvalidOperationException($"wgcf generate failed: {gen.StdErr}");
        }
    }

    /// <summary>Enables the tunnel as a daemon (boot-persistent), like the Windows method.</summary>
    public async Task InstallServiceAsync(CancellationToken ct = default)
    {
        if (!ProfileExists)
        {
            throw new InvalidOperationException("No wgcf profile yet. Register first.");
        }

        // wg-quick expects /etc/wireguard/warp.conf
        var copy = $"mkdir -p /etc/wireguard && cp {ProcessRunner.QuoteShell(ProfilePath)} /etc/wireguard/warp.conf && chmod 600 /etc/wireguard/warp.conf";
        var copyResult = await _runner.RunRootAsync("/bin/sh", $"-c {ProcessRunner.QuoteShell(copy)}", ct).ConfigureAwait(false);
        if (!copyResult.Success)
        {
            throw new InvalidOperationException($"Copying profile failed: {copyResult.StdErr}");
        }

        var systemd = new SystemdService(_runner);
        var unitContent = SystemdService.BuildSimpleUnit(
            "SplitWire-Turkey WireGuard (Cloudflare WARP) tunnel",
            "/usr/bin/wg-quick up warp");

        await systemd.InstallUnitAsync(UnitName, unitContent, ct).ConfigureAwait(false);
        await systemd.EnableNowAsync(UnitName, ct).ConfigureAwait(false);
    }

    public Task RemoveAsync(CancellationToken ct = default)
    {
        var systemd = new SystemdService(_runner);
        return systemd.DisableStopAsync(UnitName, ct);
    }

    /// <summary>
    /// Reads wgcf-profile.conf so the UI can display account status
    /// (equivalent functionality of the Windows account panel).
    /// </summary>
    public static string ReadProfileSummary()
    {
        try
        {
            var path = Path.Combine(Paths.BaseDir, ProfileFile);
            if (!File.Exists(path)) return "";
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            var sb = new StringBuilder();
            foreach (var line in lines)
            {
                if (line.StartsWith("[") || string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    sb.AppendLine($"{parts[0].Trim()} = {parts[1].Trim()}");
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"(error reading profile: {ex.Message})";
        }
    }

    private async Task DownloadAsync(string url, string target, CancellationToken ct)
    {
        LogService.Info("Wgcf", $"Downloading {url}");
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
        await stream.CopyToAsync(file, ct).ConfigureAwait(false);
    }
}
