namespace Breeze.Models;

/// <summary>User settings, stored as readable JSON. New properties can be added freely:
/// missing values fall back to these defaults and unknown ones are ignored.</summary>
public sealed class AppSettings
{
    public StartupPage StartupPage { get; set; } = StartupPage.Home;

    public string StartupUrl { get; set; } = string.Empty;

    public string DownloadFolder { get; set; } = DefaultDownloadFolder();

    public bool AskWhereToSave { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.System;

    public double UiScale { get; set; } = 1;

    public bool CompactMode { get; set; }

    public string SearchEngine { get; set; } = "DuckDuckGo";

    private static string DefaultDownloadFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
}
