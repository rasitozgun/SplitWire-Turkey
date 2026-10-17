using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace SplitWireTurkey.Linux;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Keep the classic Windows behaviour: refuse to start a second instance.
        bool createdNew;
        using var mutex = new System.Threading.Mutex(true, "Global\\SplitWire-Turkey-SingleInstance", out createdNew);
        if (!createdNew)
        {
            Console.Error.WriteLine("SplitWire-Turkey is already running.");
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    // Avalonia entry that also works with 'dotnet run' and XIL previewer tooling.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
