using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SplitWireTurkey.Linux.Services;

namespace SplitWireTurkey.Linux.Views;

public partial class ByeDpiPage : UserControl, PagesLocalized
{
    private readonly ByeDpiService _byedpi;

    public ByeDpiPage()
    {
        InitializeComponent();
        _byedpi = new ByeDpiService(ProcessRunner.Instance, ServiceHub.HttpClient);
        if (_byedpi.IsInstalled)
        {
            // Show last used arguments so users can tweak and reinstall.
            try { ArgsBox.Text = File.ReadAllText(_byedpi.ArgsFilePath); } catch { }
        }
        else
        {
            // A sensible default strategy (Discord voice/text works behind this on many ISPs).
            ArgsBox.Text = "-p 1080 -i 127.0.0.1 -d'blog.php' -e2 -q/1 -Y";
        }
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        Status.Text = "Downloading binaries and installing service ...";
        await _byedpi.InstallServiceAsync(ArgsBox.Text?.Trim() ?? "", PersistBox.IsChecked == true).ConfigureAwait(true);
        Status.Text = "ByeDPI installed. Proxy listening at 127.0.0.1:1080";
    }, "ByeDPI");

    private async void OnRemoveClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        await _byedpi.RemoveAsync().ConfigureAwait(true);
        Status.Text = "ByeDPI removed.";
    }, "ByeDPI-Remove");

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
        // Reuse existing entries from the shared Windows language files;
        // Linux-specific UI text stays English until the maintainer adds keys.
        Install.Content = LanguageManager.T("buttons", "byedpi_split");
        Remove.Content = LanguageManager.T("buttons", "remove_byedpi");
    }
}
