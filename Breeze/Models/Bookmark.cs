using System.Text.Json.Serialization;

namespace Breeze.Models;

/// <summary>A bookmark bar entry: either a bookmarked page or a folder of further entries. Missing
/// or unknown fields in the JSON file are tolerated, so a file written by an older build still
/// loads, and one written by a newer build never loses the fields this one does not know.</summary>
public sealed record Bookmark
{
    /// <summary>Stable identity, so a rename, a move or a delete addresses one exact entry even
    /// when several share a title or a URL. Entries in a file written before folders existed have
    /// none and are given one on load.</summary>
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    /// <summary>Empty for a folder.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>File name of the cached favicon inside the favicon folder, if one was found.</summary>
    public string? Icon { get; init; }

    /// <summary>Entries inside a folder, in bar order. Null for a bookmark, which is what tells the
    /// two apart: an empty folder keeps an empty list rather than becoming a bookmark.</summary>
    public IReadOnlyList<Bookmark>? Children { get; init; }

    [JsonIgnore]
    public bool IsFolder => Children is not null;

    public static string NewId() => Guid.NewGuid().ToString("n");
}
