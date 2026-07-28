using Breeze.Models;

namespace Breeze.Services;

/// <summary>Bookmarks, stored as readable JSON in the local Breeze folder. Follows the same
/// philosophy as the settings and shortcut stores: one cached list, serialized mutations, atomic
/// writes, and a file that tolerates missing or unknown fields.</summary>
public static class BookmarkStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static List<Bookmark>? _items;

    /// <summary>Raised after the list changes, so the bookmark bar can rebuild.</summary>
    public static event EventHandler? Changed;

    public static IReadOnlyList<Bookmark> Items => Cache;

    /// <summary>Icons these bookmarks still need; registered with the favicon cache for pruning.</summary>
    public static IEnumerable<string> ReferencedIcons => Cache.Select(item => item.Icon).OfType<string>();

    private static List<Bookmark> Cache => _items ??= Load();

    public static bool Contains(string? url) =>
        WebLinks.SafeUrl(url) is { } target &&
        Cache.Any(item => string.Equals(item.Url, target, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a bookmark for a page, ignoring pages that are already bookmarked.</summary>
    public static async Task AddAsync(string? rawUrl, string? title)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url || Contains(url))
        {
            return;
        }

        // Reuses the shared favicon cache, so a site already bookmarked or on the homepage is
        // never downloaded twice.
        var icon = await FaviconCache.EnsureAsync(url);

        await Gate.WaitAsync();

        try
        {
            if (Contains(url))
            {
                return;
            }

            Cache.Add(new Bookmark { Title = Name(title, url), Url = url, Icon = icon });
            Commit();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Moves a bookmark. The caller passes the URL it believes sits at
    /// <paramref name="from" />, which is how a bar working from a list another one has already
    /// changed is caught: the move is refused and the caller is told to reload, so a drop can never
    /// land on the wrong entry.</summary>
    public static async Task MoveAsync(string? rawUrl, int from, int to)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url)
        {
            return;
        }

        await Gate.WaitAsync();

        try
        {
            var items = Cache;

            if (from == to || (uint)from >= (uint)items.Count || (uint)to >= (uint)items.Count)
            {
                return;
            }

            if (!string.Equals(items[from].Url, url, StringComparison.OrdinalIgnoreCase))
            {
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }

            var bookmark = items[from];
            items.RemoveAt(from);
            items.Insert(to, bookmark);
            Commit();
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task RemoveAsync(string? rawUrl)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url)
        {
            return;
        }

        await Gate.WaitAsync();

        try
        {
            if (Cache.RemoveAll(item => string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                Commit();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Falls back to the host when a page has no title yet.</summary>
    private static string Name(string? title, string url) =>
        !string.IsNullOrWhiteSpace(title) && title != "New Tab"
            ? title.Trim()
            : new Uri(url).Host;

    private static void Commit()
    {
        WebLinks.Write(AppPaths.BookmarksFile, Cache, "bookmarks.save");
        FaviconCache.Prune();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static List<Bookmark> Load() =>
        WebLinks.Read<Bookmark>(AppPaths.BookmarksFile)
            .Where(item => WebLinks.SafeUrl(item.Url) is not null)
            .Select(item => item.Icon is null || WebLinks.SafeIcon(item.Icon) ? item : item with { Icon = null })
            .ToList();
}
