using Breeze.Models;

namespace Breeze.Services;

/// <summary>Start page shortcuts, stored as readable JSON in the local Breeze folder.
/// Mutations are serialized and carry the revision the caller last saw, so two open start pages
/// cannot lose an update or act on a stale index. Storage and validation are shared with the
/// bookmark store through <see cref="WebLinks" />.</summary>
public static class ShortcutStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static List<Shortcut>? _items;

    public static IReadOnlyList<Shortcut> Items => Cache;

    /// <summary>Icons these shortcuts still need; registered with the favicon cache for pruning.</summary>
    public static IEnumerable<string> ReferencedIcons => Cache.Select(item => item.Icon).OfType<string>();

    /// <summary>Increments on every accepted change. A caller passing an older revision is
    /// working from a stale view of the list and is refused.</summary>
    public static int Revision { get; private set; }

    private static List<Shortcut> Cache => _items ??= Load();

    /// <summary>Adds a shortcut, or replaces the one at <paramref name="index" /> when editing.</summary>
    public static async Task SaveAsync(int revision, int index, string name, string rawUrl)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // The favicon lookup can take seconds, so it runs before the gate is taken.
        var icon = await IconAsync(revision, index, url);

        await Gate.WaitAsync();

        try
        {
            var items = Cache;
            if (!Current(revision))
            {
                return;
            }

            var shortcut = new Shortcut { Name = name, Url = url, Icon = icon };

            if (index >= 0 && index < items.Count)
            {
                items[index] = shortcut;
            }
            else
            {
                items.Add(shortcut);
            }

            Commit(items);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task RemoveAsync(int revision, int index)
    {
        await Gate.WaitAsync();

        try
        {
            var items = Cache;
            if (!Current(revision) || (uint)index >= (uint)items.Count)
            {
                return;
            }

            items.RemoveAt(index);
            Commit(items);
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task MoveAsync(int revision, int from, int to)
    {
        await Gate.WaitAsync();

        try
        {
            var items = Cache;
            if (!Current(revision) || from == to ||
                (uint)from >= (uint)items.Count || (uint)to >= (uint)items.Count)
            {
                return;
            }

            var shortcut = items[from];
            items.RemoveAt(from);
            items.Insert(to, shortcut);
            Commit(items);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>A revision below zero means the caller does not track revisions.</summary>
    private static bool Current(int revision) => revision < 0 || revision == Revision;

    private static async Task<string?> IconAsync(int revision, int index, string url)
    {
        var items = Cache;
        var existing = Current(revision) && index >= 0 && index < items.Count ? items[index] : null;

        return existing?.Url == url && FaviconCache.IsCached(existing.Icon)
            ? existing.Icon
            : await FaviconCache.EnsureAsync(url);
    }

    private static void Commit(List<Shortcut> items)
    {
        Revision++;
        WebLinks.Write(AppPaths.ShortcutsFile, items, "shortcuts.save");
        FaviconCache.Prune();
    }

    private static List<Shortcut> Load() =>
        WebLinks.Read<Shortcut>(AppPaths.ShortcutsFile).Where(Valid).Select(Sanitize).ToList();

    private static bool Valid(Shortcut shortcut) =>
        !string.IsNullOrWhiteSpace(shortcut.Name) && WebLinks.SafeUrl(shortcut.Url) is not null;

    /// <summary>Drops an icon reference that does not name a plain file in the cache folder.</summary>
    private static Shortcut Sanitize(Shortcut shortcut) =>
        shortcut.Icon is null || WebLinks.SafeIcon(shortcut.Icon)
            ? shortcut
            : shortcut with { Icon = null };
}
