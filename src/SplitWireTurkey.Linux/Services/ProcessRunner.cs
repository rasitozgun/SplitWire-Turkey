using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

public record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Wraps Process execution for the Linux toolchain (wgcf, ciadpi, tpws, systemctl...).
/// Elevation uses pkexec (default on Fedora GNOME) with sudo as fallback.
/// </summary>
public class ProcessRunner
{
    public static ProcessRunner Instance { get; } = new();

    public event EventHandler<string>? OutputReceived;

    public async Task<CommandResult> RunAsync(string program, string arguments, CancellationToken ct = default)
    {
        return await RunCoreAsync(program, arguments, elevated: false, ct).ConfigureAwait(false);
    }

    /// <summary>Runs a command with root privileges (pkexec first, sudo fallback).</summary>
    public async Task<CommandResult> RunRootAsync(string program, string arguments, CancellationToken ct = default)
    {
        return await RunCoreAsync(program, arguments, elevated: true, ct).ConfigureAwait(false);
    }

    private async Task<CommandResult> RunCoreAsync(string program, string arguments, bool elevated, CancellationToken ct)
    {
        var (exe, args) = elevated ? ResolveElevation(program, arguments) : (program, arguments);

        // Chmod +x for files inside res/ that may lose the exec bit after extraction.
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        var stdout = new System.Text.StringBuilder();
        var stderr = new System.Text.StringBuilder();

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stdout) stdout.AppendLine(e.Data);
                OutputReceived?.Invoke(this, e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                lock (stderr) stderr.AppendLine(e.Data);
                OutputReceived?.Invoke(this, e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { /* already dead */ }
            throw;
        }

        LogService.Info("Process", $"{exe} {Truncate(args)} -> exit {process.ExitCode}");
        return new CommandResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    private static (string Exe, string Args) ResolveElevation(string program, string arguments)
    {
        // Already root? run directly.
        if (geteuid() == 0)
        {
            return (program, arguments);
        }

        // Prefer pkexec (Wayland/GNOME default), fall back to sudo for TTY-only environments.
        if (File.Exists("/usr/bin/pkexec"))
        {
            string? resolved = TryResolve(program);
            if (resolved is null)
            {
                // program may already be absolute or rely on PATH; pass through shell for robustness
                return ("/usr/bin/pkexec", $"/bin/sh -c \"{QuoteShell(program)} {QuoteShell(arguments)}\"");
            }
            return ("/usr/bin/pkexec", $"{QuoteShell(resolved)} {arguments}");
        }

        if (File.Exists("/usr/bin/sudo"))
        {
            string? resolved = TryResolve(program);
            return ("/usr/bin/sudo", resolved is null ? $"{program} {arguments}" : $"{QuoteShell(resolved)} {arguments}");
        }

        throw new InvalidOperationException("No privilege escalation tool found (pkexec or sudo required).");
    }

    private static string? TryResolve(string program)
    {
        if (Path.IsPathRooted(program) || program.StartsWith("./", StringComparison.Ordinal))
        {
            return File.Exists(program) ? program : null;
        }
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, program);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    public static string QuoteShell(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string Truncate(string value) => value.Length <= 200 ? value : value[..200] + "...";

    [System.Runtime.InteropServices.DllImport("libc")]
    private static extern uint geteuid();

    public static void EnsureExecutable(params string[] files)
    {
        foreach (var file in files)
        {
            try
            {
                if (!File.Exists(file)) continue;
                // NOTE: ProcessStartInfo does NOT spawn a shell, so the path must be
                // passed unquoted via ArgumentList (quoting made chmod look for a
                // path wrapped in literal quote characters -> silent fail).
                var psi = new ProcessStartInfo("chmod") { CreateNoWindow = true };
                psi.ArgumentList.Add("+x");
                psi.ArgumentList.Add(file);
                using var p = Process.Start(psi);
                p?.WaitForExit(5000);
            }
            catch { /* best effort */ }
        }
    }
}
