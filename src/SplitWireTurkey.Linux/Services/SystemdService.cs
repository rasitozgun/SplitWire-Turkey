using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

/// <summary>ThinInstallation over systemd: unit files + control commands.</summary>
public class SystemdService
{
    private readonly ProcessRunner _runner;

    public SystemdService(ProcessRunner runner) => _runner = runner;

    public const string UnitDir = "/etc/systemd/system";

    public static string UnitFile(string unitName) => Path.Combine(UnitDir, unitName);

    /// <summary>
    /// Installs a transient unit describing how the DPI tool should run as root.
    /// Written to a temp file, then copied into /etc/systemd/system with root privileges.
    /// </summary>
    public async Task InstallUnitAsync(string unitName, string content, CancellationToken ct = default)
    {
        var tmp = Path.Combine(Path.GetTempPath(), unitName);
        await File.WriteAllTextAsync(tmp, content, Encoding.UTF8, ct).ConfigureAwait(false);

        var copy = $"cp {ProcessRunner.QuoteShell(tmp)} {UnitFile(unitName)} && chmod 644 {UnitFile(unitName)}";
        var result = await _runner.RunRootAsync("/bin/sh", $"-c {ProcessRunner.QuoteShell(copy)}", ct).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Installing unit {unitName} failed: {result.StdErr}");
        }

        await ReloadAsync(ct).ConfigureAwait(false);
    }

    public Task UninstallUnitAsync(string unitName, CancellationToken ct = default)
        => _runner.RunRootAsync("/bin/sh", $"-c {ProcessRunner.QuoteShell($"rm -f {UnitFile(unitName)}")}", ct);

    public Task<CommandResult> ReloadAsync(CancellationToken ct = default)
        => _runner.RunRootAsync("systemctl", "daemon-reload", ct);

    public Task<CommandResult> EnableNowAsync(string unitName, CancellationToken ct = default)
        => _runner.RunRootAsync("systemctl", $"enable --now {unitName}", ct);

    public Task<CommandResult> DisableStopAsync(string unitName, CancellationToken ct = default)
        => _runner.RunRootAsync("systemctl", $"disable --now {unitName}", ct);

    public Task<CommandResult> StopAsync(string unitName, CancellationToken ct = default)
        => _runner.RunRootAsync("systemctl", $"stop {unitName}", ct);

    public Task<CommandResult> StartAsync(string unitName, CancellationToken ct = default)
        => _runner.RunRootAsync("systemctl", $"start {unitName}", ct);

    public async Task<bool> IsInstalledAsync(string unitName, CancellationToken ct = default)
    {
        var result = await _runner.RunAsync("systemctl", $"status {unitName}", ct).ConfigureAwait(false);
        return result.ExitCode != 4; // 4 = no such unit
    }

    public static string BuildSimpleUnit(string description, string execStart, bool oneshot = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[Unit]");
        sb.AppendLine($"Description={description}");
        sb.AppendLine("After=network-online.target");
        sb.AppendLine("Wants=network-online.target");
        sb.AppendLine();
        sb.AppendLine("[Service]");
        if (oneshot)
        {
            // wg-quick style one-shot setup: the command configures the
            // interface and exits; the interface must stay up afterwards.
            sb.AppendLine("Type=oneshot");
            sb.AppendLine("RemainAfterExit=yes");
            sb.AppendLine("ExecStop=" + execStart.Replace(" up ", " down "));
        }
        else
        {
            sb.AppendLine("Type=simple");
        }
        sb.AppendLine($"ExecStart={execStart}");
        if (!oneshot)
        {
            sb.AppendLine("Restart=on-failure");
            sb.AppendLine("RestartSec=5");
        }
        sb.AppendLine();
        sb.AppendLine("[Install]");
        sb.AppendLine("WantedBy=multi-user.target");
        return sb.ToString();
    }
}
