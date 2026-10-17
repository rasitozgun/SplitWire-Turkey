using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SplitWireTurkey.Linux.Services;

namespace SplitWireTurkey.Linux.Views;

public partial class ZapretPage : UserControl, PagesLocalized
{
    private readonly ZapretService _zapret;

    public ZapretPage()
    {
        InitializeComponent();
        _zapret = new ZapretService(ProcessRunner.Instance, ServiceHub.HttpClient);
        _ = LoadPresetsAsync();
    }

    private async Task LoadPresetsAsync()
    {
        try
        {
            var presets = await _zapret.LoadPresetsAsync().ConfigureAwait(true);
            _presetsCache = presets;
            PresetCombo.ItemsSource = presets.Select(p => p.name).ToList();
            if (presets.Length > 0)
            {
                PresetCombo.SelectedIndex = 0;
                ArgsBox.Text = presets[0].tpws_args;
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Zapret", $"Preset load failed: {ex.Message}");
        }
    }

    private void PresetSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is string name)
        {
            var preset = TryGetPresets().FirstOrDefault(p => string.Equals(p.name, name, StringComparison.Ordinal));
            if (preset is not null)
            {
                ArgsBox.Text = preset.tpws_args;
            }
        }
    }

    private ZapretService.LinuxPreset[]? _presetsCache;

    private ZapretService.LinuxPreset[] TryGetPresets()
    {
        if (_presetsCache is not null) return _presetsCache;
        try
        {
            // Sync-over-async on the UI thread deadlocks the Avalonia dispatcher;
            // presets are cached by LoadPresetsAsync, so this should only run after that.
            _presetsCache = _zapret.LoadPresetsFromDisk();
        }
        catch { _presetsCache = Array.Empty<ZapretService.LinuxPreset>(); }
        return _presetsCache;
    }

    private async void OnInstallClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        Status.Text = "Downloading binaries and installing tpws service ...";
        await _zapret.InstallPresetServiceAsync(ArgsBox.Text?.Trim() ?? "", PersistBox.IsChecked == true).ConfigureAwait(true);
        Status.Text = "zapret service installed.";
    }, "Zapret");

    private async void OnRemoveClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        await _zapret.RemoveAsync().ConfigureAwait(true);
        Status.Text = "zapret removed.";
    }, "Zapret-Remove");

    private async void OnBlockCheckClicked(object? sender, RoutedEventArgs e) => await GuardAsync(async () =>
    {
        Status.Text = "Running blockcheck (takes a while, watch the log pane) ...";
        var result = await _zapret.RunBlockCheckAsync().ConfigureAwait(true);
        Status.Text = result.Success
            ? "blockcheck finished — inspect the log pane for recommended strategies."
            : $"blockcheck exited with code {result.ExitCode}.";
    }, "Zapret-BlockCheck");

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
        Install.Content = LanguageManager.T("buttons", "preset_service");
        Remove.Content = LanguageManager.T("buttons", "remove_zapret");
    }
}
