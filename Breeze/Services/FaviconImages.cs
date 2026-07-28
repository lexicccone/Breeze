using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Breeze.Services;

/// <summary>Decoded favicons, keyed by cache file name. Decoding is the expensive part of showing
/// an icon, so each file is decoded at most once and the result is shared by every place that
/// displays it. A file that cannot be decoded, such as an SVG, is remembered as a failure so the
/// attempt is not repeated; callers fall back to the default globe glyph.</summary>
public static class FaviconImages
{
    private static readonly Dictionary<string, IImage?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads a cached favicon, or null when there is none Breeze can display.</summary>
    public static IImage? Load(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        if (Cache.TryGetValue(fileName, out var known))
        {
            return known;
        }

        var image = Decode(fileName);
        Cache[fileName] = image;
        return image;
    }

    private static IImage? Decode(string fileName)
    {
        try
        {
            return FaviconCache.FullPath(fileName) is { } path ? new Bitmap(path) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
