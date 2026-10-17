using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SplitWireTurkey.Linux.Services;

namespace SplitWireTurkey.Linux.Views;

public partial class AdvancedPage : UserControl, PagesLocalized
{
    private readonly DnsService _dns;
    private readonly SystemdService _systemd;
    private readonly ProcessRunner _runner;

    public AdvancedPage()
    {
        InitializeComponent();
        _runner = ProcessRunner.Instance;
        _dns = new DnsService(_runner);
        _systemd = new SystemdService(_runner);
        RefreshServices();
    }

    private void RefreshServices()
    {
        try
        {
            var units = new[]
            {
                WgcfService.UnitName,
                ByeDpiService.UnitName,
                ZapretService.UnitName,
            };

            var lines = units
                .Select(async u => (Unit: u, Installed: await _systemd.IsInstalledAsync(u)))
                .Select(t => t.GetAwaiter().GetResult())
                .Select(x => $"{x.Unit}: {(x.Installed ? "installed" : "not installed")}");
            ServicesList.Text = string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            ServicesList.Text = $"(cannot query services: {ex.Message})";
        }
    }

    private void OnApplyDnsClicked(object? sender, RoutedEventArgs e)
    {
        _ = GuardAsync(async () =>
        {
            Status.Text = "Setting DNS on active connections ...";
            var primary = PrimaryBox.Text?.Trim() is { Length: > 0 } p ? p : DnsService.GoogleDns;
            var secondary = SecondaryBox.Text?.Trim() is { Length: > 0 } s ? s : DnsService.Quad9Dns;
            await _dns.SetDnsAsync(primary, secondary).ConfigureAwait(true);
            Status.Text = $"DNS set to {primary} / {secondary} (IPv6: Google + Quad9).";
        }, "DNS");
    }

    private void OnResetDnsClicked(object? sender, RoutedEventArgs e)
    {
        _ = GuardAsync(async () =>
        {
            await _dns.ResetDnsAsync().ConfigureAwait(true);
            Status.Text = "DNS restored to automatic (DHCP).";
        }, "DNS-Reset");
    }

    private void OnRemoveAllClicked(object? sender, RoutedEventArgs e)
    {
        _ = GuardAsync(async () =>
        {
        var wgcf = new WgcfService(_runner, ServiceHub.HttpClient);
        var byedpi = new ByeDpiService(_runner, ServiceHub.HttpClient);
        var zapret = new ZapretService(_runner, ServiceHub.HttpClient);

        await wgcf.RemoveAsync().ConfigureAwait(true);
        await byedpi.RemoveAsync().ConfigureAwait(true);
        await zapret.RemoveAsync().ConfigureAwait(true);

        Status.Text = "All SplitWire services removed.";
        RefreshServices();
        }, "RemoveAll");
    }

    private async Task GuardAsync(Func<Task> action, string what)
    {
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            LogService.Error(what, ex.Message);
            Status.Text = $"Error: {ex.Message}";
        }
    }

    public void ApplyTranslations()
    {
        // The registry-mirroring "buttons" entries of the shared language files
        // don't map cleanly to these Linux-only actions; keep English labels.
    }
}
