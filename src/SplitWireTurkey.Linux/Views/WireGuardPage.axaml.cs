using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SplitWireTurkey.Linux.Services;

namespace SplitWireTurkey.Linux.Views;

public partial class WireGuardPage : UserControl, PagesLocalized
{
    private readonly WgcfService _wgcf;

    public WireGuardPage()
    {
        InitializeComponent();
        _wgcf = new WgcfService(ProcessRunner.Instance, ServiceHub.HttpClient);
        RefreshStatus();
    }

    private async void OnRegisterClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        Status.Text = "Creating WARP account ...";
        await _wgcf.RegisterAsync().ConfigureAwait(true);
        RefreshStatus();
    }, "Register");

    private async void OnInstallClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        Status.Text = "Installing systemd service ...";
        await _wgcf.InstallServiceAsync().ConfigureAwait(true);
        Status.Text = "Tunnel installed and enabled.";
        LogService.Info("WG", "WireGuard tunnel service installed.");
    }, "InstallService");

    private async void OnRemoveClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        await _wgcf.RemoveAsync().ConfigureAwait(true);
        Status.Text = "Tunnel removed.";
    }, "RemoveTunnel");

    private void RefreshStatus()
    {
        Profile.Text = WgcfService.ReadProfileSummary();
        Status.Text = _wgcf.ProfileExists ? "Profile exists." : "No profile yet. Register first.";
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
        // The shared Windows language files have no "wireguard" section yet;
        // keep the English labels until localizations are added.
    }
}
