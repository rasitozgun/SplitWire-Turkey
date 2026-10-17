using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using SplitWireTurkey.Linux.Services;

namespace SplitWireTurkey.Linux;

public partial class MainWindow : Window
{
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();
        Paths.EnsureDirectories();

        _settings = SettingsStore.Load();
        LanguageManager.LoadLanguage(_settings.Language);
        ApplyLanguage();
        HighlightLanguageButtons();

        LogService.LineAdded += OnLogLine;
        LogService.Info("App", $"SplitWire-Turkey Linux edition started. Config: {Paths.SettingsFile}");
        LogTextBox.Text = $"SplitWire-Turkey Linux edition\nLog file: {LogService.CurrentLogFile}\n\n";
    }

    protected override void OnClosed(EventArgs e)
    {
        LogService.LineAdded -= OnLogLine;
        base.OnClosed(e);
    }

    private void OnLogLine(object? sender, string line)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (LogTextBox is not null)
            {
                LogTextBox.Text = LogTextBox.Text?.TrimEnd() + Environment.NewLine + line;
                // keep the latest lines visible
                LogTextBox.CaretIndex = LogTextBox.Text?.Length ?? 0;
            }
        });
    }

    private void OnLanguageClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var code = button.Content?.ToString()?.ToLowerInvariant() ?? "en";
        _settings.Language = code;
        SettingsStore.Save(_settings);
        LanguageManager.LoadLanguage(code);
        ApplyLanguage();
        HighlightLanguageButtons();
    }

    private void HighlightLanguageButtons()
    {
        void Mark(Button b, bool active)
        {
            b.FontWeight = active ? FontWeight.Bold : FontWeight.Normal;
        }
        Mark(LangTrButton, LanguageManager.CurrentLanguage == "tr");
        Mark(LangEnButton, LanguageManager.CurrentLanguage == "en");
        Mark(LangRuButton, LanguageManager.CurrentLanguage == "ru");
        Mark(LangEsButton, LanguageManager.CurrentLanguage == "es");
    }

    /// <summary>Language codes -> categories map directly to the Windows JSON files.</summary>
    private void ApplyLanguage()
    {
        Title = "SplitWire-Turkey — " + LanguageManager.T("tabs", "main");
        TabWG.Header = LanguageManager.T("tabs", "main");
        TabByeDpi.Header = LanguageManager.T("tabs", "byedpi");
        TabZapret.Header = LanguageManager.T("tabs", "zapret");
        TabAdvanced.Header = LanguageManager.T("tabs", "advanced");

        foreach (var item in MainTabs.Items)
        {
            if (item is TabItem { Content: PagesLocalized localizable })
            {
                localizable.ApplyTranslations();
            }
        }
    }
}
