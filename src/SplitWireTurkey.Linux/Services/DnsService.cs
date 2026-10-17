using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SplitWireTurkey.Linux.Services;

/// <summary>
/// Linux replacement for the Windows netsh DNS logic: changes the system DNS
/// through NetworkManager (the default network stack on Fedora), never touching resolv.conf
/// by hand. DoH is delegated to dnscrypt-proxy or quad9/google plain DNS.
/// </summary>
public class DnsService
{
    private readonly ProcessRunner _runner;

    public DnsService(ProcessRunner runner) => _runner = runner;

    public const string GoogleDns = "8.8.8.8";
    public const string GoogleDnsV6 = "2001:4860:4860::8888";
    public const string Quad9Dns = "9.9.9.9";
    public const string Quad9DnsV6 = "2620:fe::fe";

    /// <summary>Active NetworkManager connection names (via nmcli -t).</summary>
    public async Task<string[]> GetActiveConnectionsAsync(CancellationToken ct = default)
    {
        var result = await _runner.RunAsync("nmcli", "-t --fields NAME,STATE connection show --active", ct).ConfigureAwait(false);
        if (!result.Success)
        {
            throw new InvalidOperationException($"nmcli failed: {result.StdErr}");
        }
        return result.StdOut
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => l.EndsWith(":activated", StringComparison.OrdinalIgnoreCase))
            .Select(l => l[..^":activated".Length])
            .ToArray();
    }

    /// <summary>Sets DNS on all active connections (mirrors the Windows all-adapter behaviour).</summary>
    public async Task SetDnsAsync(string primary, string secondary, CancellationToken ct = default)
    {
        var connections = await GetActiveConnectionsAsync(ct).ConfigureAwait(false);
        foreach (var con in connections)
        {
            var args = $"con mod {ProcessRunner.QuoteShell(con)} " +
                       $"ipv4.dns \"{primary} {secondary}\" " +
                       $"ipv6.dns \"{GoogleDnsV6} {Quad9DnsV6}\" " +
                       "ipv4.ignore-auto-dns no ipv4.dns-search ''";
            var result = await _runner.RunRootAsync("nmcli", args, ct).ConfigureAwait(false);
            if (!result.Success)
            {
                throw new InvalidOperationException($"DNS change on '{con}' failed: {result.StdErr}");
            }
            await _runner.RunRootAsync("nmcli", $"con up {ProcessRunner.QuoteShell(con)}", ct).ConfigureAwait(false);
        }
    }

    /// <summary>Back to DHCP (Windows "Auto (DHCP)" equivalent).</summary>
    public async Task ResetDnsAsync(CancellationToken ct = default)
    {
        var connections = await GetActiveConnectionsAsync(ct).ConfigureAwait(false);
        foreach (var con in connections)
        {
            await _runner.RunRootAsync("nmcli",
                $"con mod {ProcessRunner.QuoteShell(con)} ipv4.dns '' ipv6.dns ''", ct).ConfigureAwait(false);
            await _runner.RunRootAsync("nmcli", $"con up {ProcessRunner.QuoteShell(con)}", ct).ConfigureAwait(false);
        }
    }
}
