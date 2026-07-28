using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Breeze.Services;

/// <summary>Downloads the best favicon a site offers, once, and serves it from a local cache folder.</summary>
public static partial class FaviconCache
{
    private const int MaxIconBytes = 1024 * 1024;

    /// <summary>Only the head of a page is needed to find its icon links.</summary>
    private const int MaxHtmlBytes = 512 * 1024;

    private const int RegexTimeoutMilliseconds = 1000;

    private const int MaxRedirects = 5;

    /// <summary>Vectors rank last. Breeze's own chrome draws these icons itself and decodes raster
    /// formats only, so choosing an SVG leaves the bookmark bar on its fallback glyph. One is still
    /// kept when a site offers nothing else, since the start page can display it.</summary>
    private const int VectorScore = 0;

    private const int AppleTouchScore = 180;
    private const int RasterScore = 64;
    private const int LegacyIconScore = 32;

    private static readonly HttpClient Client = CreateClient();

    private static readonly List<Func<IEnumerable<string>>> References = [];

    /// <summary>Returns the cached favicon file name for a site, downloading it only when missing.</summary>
    public static async Task<string?> EnsureAsync(string siteUrl)
    {
        if (!Uri.TryCreate(siteUrl, UriKind.Absolute, out var site) ||
            site.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // An icon already cached is reused, unless it turns out to be one the chrome cannot draw:
        // then the site is asked again, in case it offers something better.
        var cached = Find(site.Host);
        if (cached is not null && IsRenderable(cached))
        {
            return cached;
        }

        (string Name, byte[] Data)? undrawable = null;

        foreach (var candidate in await CandidatesAsync(site))
        {
            var icon = await ReadAsync(candidate);
            if (icon is null)
            {
                continue;
            }

            var name = FileName(site.Host, Extension(candidate, icon.Value.ContentType));

            // An icon Breeze cannot decode is no better than none, so the next candidate gets a
            // turn. The first one is kept in case the site offers nothing this build can draw.
            if (!FaviconImages.CanDecode(icon.Value.Data))
            {
                undrawable ??= (name, icon.Value.Data);
                continue;
            }

            return await SaveAsync(name, icon.Value.Data);
        }

        return undrawable is { } spare ? await SaveAsync(spare.Name, spare.Data) : cached;
    }

    private static async Task<string?> SaveAsync(string name, byte[] data)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Favicons);
            await File.WriteAllBytesAsync(Path.Combine(AppPaths.Favicons, name), data);
            return name;
        }
        catch (Exception error)
        {
            ErrorLog.Write("favicon.save", error);
            return null;
        }
    }

    private static HttpClient CreateClient()
    {
        // Redirects are followed by hand so every hop can be validated before it is requested.
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppIdentity.UserAgent);
        return client;
    }

    /// <summary>Requests a URL, validating it and every redirect target. A blocked destination
    /// ends the attempt: the caller falls back to the next candidate or the default globe icon.</summary>
    private static async Task<HttpResponseMessage?> FetchAsync(Uri url)
    {
        var target = url;

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            if (!await IsAllowedAsync(target))
            {
                ErrorLog.Write("favicon.blocked", new InvalidOperationException(target.GetLeftPart(UriPartial.Authority)));
                return null;
            }

            var response = await Client.GetAsync(target, HttpCompletionOption.ResponseHeadersRead);

            if (!IsRedirect(response.StatusCode) || response.Headers.Location is null)
            {
                return response;
            }

            var next = new Uri(target, response.Headers.Location);
            response.Dispose();
            target = next;
        }

        return null;
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// <summary>A favicon may only be fetched over http or https from a public address. Host names
    /// are resolved and every address they map to must be public, so a site cannot point Breeze at
    /// a machine on the user's own network.</summary>
    private static async Task<bool> IsAllowedAsync(Uri url)
    {
        if (url.Scheme is not ("http" or "https"))
        {
            return false;
        }

        if (IPAddress.TryParse(url.DnsSafeHost, out var literal))
        {
            return !IsBlocked(literal);
        }

        var host = url.DnsSafeHost;
        if (host.Length == 0 ||
            host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(host);
            return addresses.Length > 0 && addresses.All(address => !IsBlocked(address));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            return IsBlocked(address.MapToIPv4());
        }

        if (IPAddress.IsLoopback(address) ||
            address.Equals(IPAddress.Any) ||
            address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal ||
                   address.IsIPv6UniqueLocal || address.IsIPv6Multicast;
        }

        var octets = address.GetAddressBytes();

        return octets[0] switch
        {
            0 => true,                                              // 0.0.0.0/8 unspecified
            10 => true,                                             // 10.0.0.0/8 private
            127 => true,                                            // loopback
            100 => octets[1] is >= 64 and <= 127,                   // 100.64.0.0/10 carrier grade NAT
            169 => octets[1] == 254,                                // 169.254.0.0/16 link local
            172 => octets[1] is >= 16 and <= 31,                    // 172.16.0.0/12 private
            192 => octets[1] == 168 || (octets[1] == 0 && octets[2] == 0), // 192.168/16 and 192.0.0/24
            198 => octets[1] is 18 or 19,                           // 198.18.0.0/15 benchmarking
            >= 224 => true,                                         // multicast, reserved, broadcast
            _ => false
        };
    }

    /// <summary>True when the named icon is still present in the cache folder.</summary>
    public static bool IsCached(string? fileName) =>
        WebLinks.SafeIcon(fileName) && File.Exists(Path.Combine(AppPaths.Favicons, fileName!));

    /// <summary>True when the cached icon is one Breeze's own chrome can draw. A vector, or a file
    /// the decoder rejects, is cached but not drawable, and the caller shows its fallback glyph.</summary>
    public static bool IsRenderable(string? fileName) => FaviconImages.Load(fileName) is not null;

    /// <summary>Full path of a cached icon, or null when it is missing or not a plain file name.</summary>
    public static string? FullPath(string? fileName) =>
        IsCached(fileName) ? Path.Combine(AppPaths.Favicons, fileName!) : null;

    /// <summary>Registers a source of still referenced icon file names. Every store that keeps
    /// icons must register, because pruning deletes anything no registered source claims.</summary>
    public static void Track(Func<IEnumerable<string>> referenced) => References.Add(referenced);

    /// <summary>Deletes cached icons no registered source refers to any more. A file that is still
    /// referenced is always kept, and a failed delete is ignored.</summary>
    public static void Prune()
    {
        try
        {
            if (!Directory.Exists(AppPaths.Favicons))
            {
                return;
            }

            var keep = new HashSet<string>(References.SelectMany(source => source()), StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.EnumerateFiles(AppPaths.Favicons))
            {
                if (keep.Contains(Path.GetFileName(file)))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // In use or locked: leave it for the next prune.
                }
            }
        }
        catch (Exception error)
        {
            ErrorLog.Write("favicon.prune", error);
        }
    }

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

        try
        {
            foreach (var link in LinkPattern().Matches(html ?? string.Empty).Cast<Match>())
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
        catch (RegexMatchTimeoutException error)
        {
            // Pathological markup: fall back to the default icon path below.
            ErrorLog.Write("favicon.parse", error);
        }

        // The default path joins the ranking rather than being appended after it, so a site that
        // declares only a vector still has its plain icon tried first.
        var fallback = new Uri(site, "/favicon.ico");
        if (!declared.ContainsKey(fallback))
        {
            declared[fallback] = LegacyIconScore;
        }

        return declared
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToList();
    }

    /// <summary>Ranks a declared icon: the largest declared pixel size first, vectors last.</summary>
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
            using var response = await FetchAsync(url);
            if (response is null)
            {
                return null;
            }

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

    /// <summary>Reads at most <see cref="MaxHtmlBytes" /> of markup. The response body is
    /// attacker controlled, so it is never buffered without a limit.</summary>
    private static async Task<string?> ReadTextAsync(Uri url)
    {
        try
        {
            using var response = await FetchAsync(url);
            if (response is null)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;

            if (!response.IsSuccessStatusCode ||
                contentType?.Contains("html", StringComparison.OrdinalIgnoreCase) != true)
            {
                return null;
            }

            await using var body = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[MaxHtmlBytes];
            var read = 0;

            while (read < buffer.Length)
            {
                var chunk = await body.ReadAsync(buffer.AsMemory(read));
                if (chunk == 0)
                {
                    break;
                }

                read += chunk;
            }

            return Encoding.UTF8.GetString(buffer, 0, read);
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

    [GeneratedRegex("""<link\b[^>]*rel\s*=\s*["'][^"']*icon[^"']*["'][^>]*>""", RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex LinkPattern();

    [GeneratedRegex("""href\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex HrefPattern();

    [GeneratedRegex("""sizes\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase, RegexTimeoutMilliseconds)]
    private static partial Regex SizesPattern();
}
