using Breeze.Models;

namespace Breeze.Services;

/// <summary>Available search engines. Extend the list to offer more.</summary>
public static class SearchEngines
{
    public static IReadOnlyList<SearchEngine> All { get; } =
    [
        new("DuckDuckGo", "https://duckduckgo.com/?q="),
        new("Google", "https://www.google.com/search?q="),
        new("Bing", "https://www.bing.com/search?q=")
    ];

    public static SearchEngine Current =>
        All.FirstOrDefault(engine => engine.Name == SettingsStore.Current.SearchEngine) ?? All[0];
}
