using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// zapret (bol-van) backend. zapret originates on Linux, so this backend maps
/// the Windows "presets" flow onto tpws instances and the blockcheck scanner.
/// Presets live in res/zapret/presets-linux.json; the resolver downloads the
/// official release binaries (tpws, blockcheck, etc.) for the current arch.
/// </summary>
public class ZapretService
{
    private readonly ProcessRunner _runner;
    private readonly SystemdService _systemd;
    private readonly HttpClient _http;

    public const string UnitName = "splitwire-zapret.service";
    public const string PresetsFileRelPath = "res/zapret/presets-linux.json";

    public ZapretService(ProcessRunner runner, HttpClient http)
    {
        _runner = runner;
        _systemd = new SystemdService(runner);
        _http = http;
    }

    public string InstallDir => Path.Combine(Paths.WorkingDir, "zapret");

    public class LinuxPreset
    {
        public string name { get; set; } = "";
        public string tpws_args { get; set; } = "";
    }

    public async Task<LinuxPreset[]> LoadPresetsAsync(CancellationToken ct = default)
    {
        var path = Path.Combine(Paths.BaseDir, PresetsFileRelPath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Linux zapret presets not found next to the binary.", path);
        }
        var doc = await JsonSerializer.DeserializeAsync<LinuxPreset[]>(
            File.OpenRead(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct).ConfigureAwait(false);
        return doc ?? Array.Empty<LinuxPreset>();
    }

    /// <summary>Synchronous presets read for UI event handlers (cache-friendly, no deadlock).</summary>
    public LinuxPreset[] LoadPresetsFromDisk()
    {
        var path = Path.Combine(Paths.BaseDir, PresetsFileRelPath);
        if (!File.Exists(path))
        {
            return Array.Empty<LinuxPreset>();
        }
        return JsonSerializer.Deserialize<LinuxPreset[]>(
            File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    /// <summary>Downloads + extracts the official zapret binaries (tpws etc.).</summary>
    public async Task EnsureBinariesAsync(CancellationToken ct = default)
    {
        if (Directory.Exists(InstallDir) && File.Exists(Path.Combine(InstallDir, "tpws")))
        {
            return;
        }

        const string repo = "bol-van/zapret";
        var asset = await GitHubReleaseHelper
            .GetLatestAssetUrlAsync(_http, repo, "linux.*(x86_64|amd64).*(\\.tar\\.gz|\\.tgz)$", ct)
            .ConfigureAwait(false);

        var tgzPath = Path.Combine(Path.GetTempPath(), "zapret-bin.tgz");
        LogService.Info("Zapret", $"Downloading {asset}");
        using (var response = await _http.GetAsync(asset, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var file = new FileStream(tgzPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        if (Directory.Exists(InstallDir))
        {
            Directory.Delete(InstallDir, true);
        }
        Directory.CreateDirectory(InstallDir);
        await _runner.RunAsync("tar", $"-xzf {ProcessRunner.QuoteShell(tgzPath)} -C {ProcessRunner.QuoteShell(InstallDir)} --strip-components=1", ct).ConfigureAwait(false);

        // Binaries live under binaries/linux64/
        var binDir = Path.Combine(InstallDir, "binaries");
        foreach (var file in Directory.GetFiles(binDir, "*", SearchOption.AllDirectories))
        {
            ProcessRunner.EnsureExecutable(file);
        }
    }

    /// <summary>Installs a preset as a persistent systemd service running tpws.</summary>
    public async Task InstallPresetServiceAsync(string tpwsArgs, bool persistAfterReboot, CancellationToken ct = default)
    {
        await EnsureBinariesAsync(ct).ConfigureAwait(false);
        var tpws = FindTool("tpws") ?? throw new InvalidOperationException("tpws binary not found after extraction.");

        var content = SystemdService.BuildSimpleUnit(
            "SplitWire-Turkey zapret (tpws) DPI bypass",
            $"{ProcessRunner.QuoteShell(tpws)} {tpwsArgs}");
        await _systemd.InstallUnitAsync(UnitName, content, ct).ConfigureAwait(false);

        if (persistAfterReboot)
        {
            await _systemd.EnableNowAsync(UnitName, ct).ConfigureAwait(false);
        }
        else
        {
            await _systemd.StartAsync(UnitName, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Runs blockcheck once to auto-adapt strategy (like WM tab auto-scan).</summary>
    public async Task<CommandResult> RunBlockCheckAsync(CancellationToken ct = default)
    {
        await EnsureBinariesAsync(ct).ConfigureAwait(false);
        var script = FindTool("blockcheck.sh") ?? throw new InvalidOperationException("blockcheck.sh not found after extraction.");
        return await _runner.RunRootAsync("/bin/sh", $"-c {ProcessRunner.QuoteShell($"{ProcessRunner.QuoteShell(script)} --run")}", ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(CancellationToken ct = default)
    {
        await _systemd.DisableStopAsync(UnitName, ct).ConfigureAwait(false);
        await _systemd.UninstallUnitAsync(UnitName, ct).ConfigureAwait(false);
    }

    private string? FindTool(string tool)
    {
        var binDir = Path.Combine(InstallDir, "binaries");
        if (Directory.Exists(binDir))
        {
            var hits = Directory.GetFiles(binDir, tool, SearchOption.AllDirectories);
            if (hits.Length > 0) return hits[0];
        }
        var direct = Path.Combine(InstallDir, tool);
        return File.Exists(direct) ? direct : null;
    }
}
