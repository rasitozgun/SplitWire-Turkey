using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// ByeDPI (ciadpi) backend. ciadpi ships official linux builds, so unlike the
/// Windows edition no proxy pre-launcher (ProxiFyre/drover) is needed:
/// ciadpi runs as its own systemd service and applications are pointed at the
/// local SOCKS5 proxy (127.0.0.1:by default 1080).
/// </summary>
public class ByeDpiService
{
    private readonly ProcessRunner _runner;
    private readonly SystemdService _systemd;
    private readonly HttpClient _http;

    public const string UnitName = "splitwire-byedpi.service";
    public const string BinaryRelPath = "byedpi/ciadpi";
    public const string ArgsFileRelPath = "byedpi/ciadpi.args";

    public ByeDpiService(ProcessRunner runner, HttpClient http)
    {
        _runner = runner;
        _systemd = new SystemdService(runner);
        _http = http;
    }

    public string BinaryPath => Path.Combine(Paths.WorkingDir, BinaryRelPath);
    public string ArgsFilePath => Path.Combine(Paths.WorkingDir, ArgsFileRelPath);

    public bool IsInstalled => File.Exists(BinaryPath);

    public async Task EnsureBinaryAsync(CancellationToken ct = default)
    {
        if (File.Exists(BinaryPath))
        {
            ProcessRunner.EnsureExecutable(BinaryPath);
            return;
        }

        // byedpi releases contain e.g. byedpi-v18-linux-x86_64.tar.gz
        // -> resolve the current linux x86_64 archive via the GitHub release API
        //    and extract the ciadpi binary into WorkingDir.
        const string repo = "hufrea/byedpi";
        var asset = await GitHubReleaseHelper
            .GetLatestAssetUrlAsync(_http, repo, "linux.*(x86_64|amd64).*(\\.tar|\\.zip)", ct)
            .ConfigureAwait(false);

        var archiveExt = asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? ".zip" : ".tar.gz";
        var (archivePath, extractDir) = (Path.Combine(Path.GetTempPath(), "byedpi-asset" + (archiveExt == ".zip" ? ".zip" : ".tgz")), Path.Combine(Path.GetTempPath(), "byedpi-extract"));

        LogService.Info("ByeDPI", $"Downloading {asset}");
        using (var response = await _http.GetAsync(asset, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
        {
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var file = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None);
            await stream.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        if (Directory.Exists(extractDir))
        {
            Directory.Delete(extractDir, true);
        }
        Directory.CreateDirectory(extractDir);

        if (archiveExt == ".zip")
        {
            ZipFile.ExtractToDirectory(archivePath, extractDir);
        }
        else
        {
            await _runner.RunAsync("tar", $"-xzf {ProcessRunner.QuoteShell(archivePath)} -C {ProcessRunner.QuoteShell(extractDir)}", ct).ConfigureAwait(false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(BinaryPath)!);
        foreach (var candidate in Directory.GetFiles(extractDir, "ciadpi", SearchOption.AllDirectories))
        {
            File.Move(candidate, BinaryPath, overwrite: true);
            break;
        }

        if (!File.Exists(BinaryPath))
        {
            throw new InvalidOperationException("ciadpi binary not found inside the byedpi release archive.");
        }

        ProcessRunner.EnsureExecutable(BinaryPath);
    }

    /// <summary>Installs (or refreshes) the systemd service with the given strategy arguments.</summary>
    public async Task InstallServiceAsync(string strategyArgs, bool persistAfterReboot, CancellationToken ct = default)
    {
        await EnsureBinaryAsync(ct).ConfigureAwait(false);
        Directory.CreateDirectory(Path.GetDirectoryName(ArgsFilePath)!);
        await File.WriteAllTextAsync(ArgsFilePath, strategyArgs, Encoding.UTF8, ct).ConfigureAwait(false);

        var content = SystemdService.BuildSimpleUnit(
            "SplitWire-Turkey ByeDPI (ciadpi) local proxy",
            $"{ProcessRunner.QuoteShell(BinaryPath)} {strategyArgs}");
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

    public async Task RemoveAsync(CancellationToken ct = default)
    {
        await _systemd.DisableStopAsync(UnitName, ct).ConfigureAwait(false);
        await _systemd.UninstallUnitAsync(UnitName, ct).ConfigureAwait(false);
    }

    public Task<bool> IsRunningAsync(CancellationToken ct = default)
        => new SystemdService(_runner).IsInstalledAsync(UnitName, ct);
}
