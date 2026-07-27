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

    public static string ErrorLogFile { get; } = Path.Combine(Root, "errors.log");

    /// <summary>Writes the file through a temporary sibling so a failure part way through cannot
    /// leave a truncated file behind. Callers handle their own failures.</summary>
    public static void WriteAtomic(string path, string contents)
    {
        Directory.CreateDirectory(Root);

        var temporary = path + ".tmp";
        File.WriteAllText(temporary, contents);
        File.Move(temporary, path, overwrite: true);
    }
}
