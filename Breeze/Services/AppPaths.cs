namespace Breeze.Services;

/// <summary>Local-only storage locations used by Breeze.</summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Breeze");

    public static string WebViewData { get; } = Path.Combine(Root, "WebView2");

    public static string Favicons { get; } = Path.Combine(Root, "Favicons");

    public static string ShortcutsFile { get; } = Path.Combine(Root, "shortcuts.json");

    public static string SettingsFile { get; } = Path.Combine(Root, "settings.json");
}
