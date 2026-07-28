namespace Breeze.Models;

/// <summary>A bookmarked page. Missing or unknown fields in the JSON file are tolerated, so the
/// format can gain fields later (folders, ordering, tags) without breaking older files.</summary>
public sealed record Bookmark
{
    public string Title { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    /// <summary>File name of the cached favicon inside the favicon folder, if one was found.</summary>
    public string? Icon { get; init; }
}
