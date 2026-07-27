using Avalonia;
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

    /// <summary>Last resort logging for failures that escape their own guards. Avalonia's Win32
    /// dispatcher does not support handling exceptions in the main loop, so UI thread work is
    /// guarded where it starts instead: see RelayCommand.</summary>
    private static void GuardAgainstCrashes()
    {
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
