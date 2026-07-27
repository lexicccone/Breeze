using Avalonia;
using Breeze.Services;

namespace Breeze;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppIdentity.Apply();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect();
}
