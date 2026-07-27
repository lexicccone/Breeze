using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace Breeze.Services;

/// <summary>Downloads the best favicon a site offers, once, and serves it from a local cache folder.</summary>
public static partial class FaviconCache
{
    private const int MaxIconBytes = 1024 * 1024;

    /// <summary>Assumed edge length for icons that do not declare a size.</summary>
    private const int VectorScore = 1000;
    private const int AppleTouchScore = 180;
    private const int RasterScore = 64;
    private const int LegacyIconScore = 32;

    private static readonly HttpClient Client = CreateClient();

    /// <summary>Returns the cached favicon file name for a site, downloading it only when missing.</summary>
    public static async Task<string?> EnsureAsync(string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var site) ||
            site.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var cached = Find(site.Host);
        if (cached is not null)
        {
            return cached;
        }

        foreach (var candidate in await CandidatesAsync(site))
        {
            var icon = await ReadAsync(candidate);
            if (icon is null)
            {
                continue;
            }

            var name = FileName(site.Host, Extension(candidate, icon.Value.ContentType));

            try
            {
                Directory.CreateDirectory(AppPaths.Favicons);
                await File.WriteAllBytesAsync(Path.Combine(AppPaths.Favicons, name), icon.Value.Data);
            }
            catch (Exception error)
            {
                ErrorLog.Write("favicon.save", error);
                return null;
            }

            return name;
        }

        return null;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Breeze");
        return client;
    }

    /// <summary>True when the named icon is still present in the cache folder.</summary>
    public static bool IsCached(string? fileName) =>
        !string.IsNullOrEmpty(fileName) && File.Exists(Path.Combine(AppPaths.Favicons, fileName));

    private static string? Find(string host)
    {
        if (!Directory.Exists(AppPaths.Favicons))
        {
            return null;
        }

        var match = Directory.EnumerateFiles(AppPaths.Favicons, Safe(host) + ".*").FirstOrDefault();
        return match is null ? null : Path.GetFileName(match);
    }

    private static string FileName(string host, string extension) => Safe(host) + extension;

    /// <summary>Reduces a host to characters that are always valid in a file name and in a
    /// search pattern, so hosts such as the IPv6 literal [::1] cannot break the write.</summary>
    private static string Safe(string host)
    {
        var name = new StringBuilder(host.Length);

        foreach (var character in host)
        {
            name.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' ? character : '_');
        }

        return name.ToString();
    }

    /// <summary>Icons declared by the page, best quality first, with the default path last.</summary>
    private static async Task<List<Uri>> CandidatesAsync(Uri site)
    {
        var declared = new Dictionary<Uri, int>();
        var html = await ReadTextAsync(new Uri(site, "/"));

        if (html is not null)
        {
            foreach (var link in LinkPattern().Matches(html).Cast<Match>())
            {
                // mask-icon is a monochrome silhouette, never a good display icon.
                if (link.Value.Contains("mask-icon", StringComparison.OrdinalIgnoreCase) ||
                    HrefPattern().Match(link.Value) is not { Success: true } href ||
                    !Uri.TryCreate(site, href.Groups[1].Value.Trim(), out var icon) ||
                    icon.Scheme is not ("http" or "https"))
                {
                    continue;
                }

                var score = Score(link.Value, icon);
                if (!declared.TryGetValue(icon, out var known) || score > known)
                {
                    declared[icon] = score;
                }
            }
        }

        var candidates = declared
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToList();

        var fallback = new Uri(site, "/favicon.ico");
        if (!declared.ContainsKey(fallback))
        {
            candidates.Add(fallback);
        }

        return candidates;
    }

    /// <summary>Ranks a declared icon: vectors first, then the largest declared pixel size.</summary>
    private static int Score(string link, Uri url)
    {
        if (Path.GetExtension(url.AbsolutePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            return VectorScore;
        }

        if (SizesPattern().Match(link) is { Success: true } sizes)
        {
            var largest = sizes.Groups[1].Value
                .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries)
                .Select(size => int.TryParse(size.Split('x', 'X')[0], out var edge) ? edge : 0)
                .DefaultIfEmpty(0)
                .Max();

            if (largest > 0)
            {
                return largest;
            }
        }

        if (link.Contains("apple-touch-icon", StringComparison.OrdinalIgnoreCase))
        {
            return AppleTouchScore;
        }

        return Path.GetExtension(url.AbsolutePath).Equals(".ico", StringComparison.OrdinalIgnoreCase)
            ? LegacyIconScore
            : RasterScore;
    }

    private static async Task<(byte[] Data, string? ContentType)?> ReadAsync(Uri url)
    {
        try
        {
            using var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (!response.IsSuccessStatusCode ||
                response.Content.Headers.ContentLength > MaxIconBytes ||
                !IsImage(contentType))
            {
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync();
            return data.Length is > 0 and <= MaxIconBytes ? (data, contentType) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<string?> ReadTextAsync(Uri url)
    {
        try
        {
            return await Client.GetStringAsync(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool IsImage(string? contentType) =>
        contentType is null || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string Extension(Uri url, string? contentType)
    {
        var extension = Path.GetExtension(url.AbsolutePath).ToLowerInvariant();
        if (extension is ".ico" or ".png" or ".svg" or ".jpg" or ".jpeg" or ".gif" or ".webp")
        {
            return extension;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "image/svg+xml" => ".svg",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            _ => ".ico"
        };
    }

    [GeneratedRegex("""<link\b[^>]*rel\s*=\s*["'][^"']*icon[^"']*["'][^>]*>""", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("""href\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex HrefPattern();

    [GeneratedRegex("""sizes\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex SizesPattern();
}
