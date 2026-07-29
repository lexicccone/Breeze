using System.Text.Json;
using System.Text.Json.Serialization;

namespace Breeze.Services;

/// <summary>Storage and validation shared by the stores that keep lists of web links, currently
/// homepage shortcuts and bookmarks. Both files are untrusted input: anything running as the user
/// can edit them, so values are validated on the way in and on the way out.</summary>
public static class WebLinks
{
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,

        // Absent rather than null: a bookmark writes no children, a folder no url, so the file
        // stays readable and a reader tells the two apart by what is there.
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Normalizes a link target, or null when it is not a web page. A scheme such as
    /// javascript: must never reach a privileged internal page or a native list.</summary>
    public static string? SafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;

    /// <summary>True when the value names a plain file, so it cannot escape the icon cache folder.</summary>
    public static bool SafeIcon(string? icon) =>
        icon is not null &&
        icon.Length <= 128 &&
        icon == Path.GetFileName(icon) &&
        icon.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !icon.Contains("..", StringComparison.Ordinal);

    /// <summary>Reads a list, returning an empty one for a missing, unreadable or corrupt file.</summary>
    public static List<T> Read<T>(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), Json) ?? []
                : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Writes a list atomically. Failures are logged, never thrown at the caller.</summary>
    public static void Write<T>(string path, List<T> items, string context)
    {
        try
        {
            AppPaths.WriteAtomic(path, JsonSerializer.Serialize(items, Json));
        }
        catch (Exception error)
        {
            ErrorLog.Write(context, error);
        }
    }
}
