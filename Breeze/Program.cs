using Avalonia;
using Avalonia.Threading;
using Breeze.Services;

namespace Breeze;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppIdentity.Apply();
        GuardAgainstCrashes();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();

    /// <summary>Last resort handlers so a single failed operation cannot take the browser,
    /// and every open tab, down with it.</summary>
    private static void GuardAgainstCrashes()
    {
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            ErrorLog.Write("ui", e.Exception);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLog.Write("task", e.Exception);
            e.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception error)
            {
                ErrorLog.Write("domain", error);
            }
        };
    }
}
