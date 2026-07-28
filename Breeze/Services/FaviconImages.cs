using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Breeze.Services;

/// <summary>Decoded favicons, shared by everything that displays one. Decoding is the expensive
/// part of showing an icon, so each source is decoded at most once and the resulting bitmap is
/// handed out to every caller. Anything that cannot be decoded is remembered as a failure so the
/// attempt is not repeated; callers fall back to the default globe glyph.</summary>
public static class FaviconImages
{
    /// <summary>Icons the engine renders are small, but the size is worth bounding all the same.</summary>
    private const int MaxIconBytes = 1024 * 1024;

    /// <summary>A long session visits many sites; past this many distinct icons the cache stops
    /// growing and later icons are simply decoded per tab.</summary>
    private const int MaxEngineIcons = 512;

    private static readonly Dictionary<string, IImage?> Files = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, IImage?> Engine = new(StringComparer.Ordinal);

    /// <summary>Loads an icon from the favicon cache folder, or null when there is none Breeze can
    /// display. Used by the lists that persist their icons: bookmarks and start page shortcuts.</summary>
    public static IImage? Load(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        if (Files.TryGetValue(fileName, out var known))
        {
            return known;
        }

        var image = Decode(FaviconCache.FullPath(fileName));
        Files[fileName] = image;
        return image;
    }

    /// <summary>True when the icon at this favicon URL has already been decoded, whether it
    /// produced an image or turned out to be undecodable. Lets a tab show a site's icon without
    /// asking the engine for the bytes again.</summary>
    public static bool TryGetEngineIcon(string url, out IImage? icon) => Engine.TryGetValue(url, out icon);

    /// <summary>Decodes an icon the engine has already fetched and rendered for a page, and keeps
    /// it for every other tab on that site. The engine hands over a forward only stream, which the
    /// decoder cannot work from, so the bytes are buffered first.</summary>
    public static async Task<IImage?> StoreEngineIconAsync(string url, Stream? data)
    {
        var icon = data is null ? null : await DecodeAsync(data);

        if (Engine.Count < MaxEngineIcons)
        {
            Engine[url] = icon;
        }

        return icon;
    }

    private static async Task<IImage?> DecodeAsync(Stream data)
    {
        try
        {
            using var buffer = new MemoryStream();
            await data.CopyToAsync(buffer);

            if (buffer.Length is 0 or > MaxIconBytes)
            {
                return null;
            }

            buffer.Position = 0;
            return new Bitmap(buffer);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static IImage? Decode(string? path)
    {
        try
        {
            return path is null ? null : new Bitmap(path);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
