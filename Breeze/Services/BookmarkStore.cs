using Breeze.Models;

namespace Breeze.Services;

/// <summary>Bookmarks and bookmark folders, stored as readable JSON in the local Breeze folder.
/// Follows the same philosophy as the settings and shortcut stores: one cached tree, serialized
/// mutations, atomic writes, and a file that tolerates missing or unknown fields.
///
/// Entries are addressed by id rather than by position or URL, so an edit made from a view that has
/// since changed either finds its entry or is refused, and the same URL may sit in more than one
/// folder. Folders hold their children directly, which keeps ordering and nesting in the one place
/// that is written.</summary>
public static class BookmarkStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static List<Bookmark>? _items;

    /// <summary>Raised after the tree changes, so the bookmark bar can rebuild.</summary>
    public static event EventHandler? Changed;

    /// <summary>Top level entries, in bar order.</summary>
    public static IReadOnlyList<Bookmark> Items => Cache;

    /// <summary>True when there is nothing on the bar at all, folders included.</summary>
    public static bool IsEmpty => Cache.Count == 0;

    /// <summary>Icons these bookmarks still need; registered with the favicon cache for pruning.</summary>
    public static IEnumerable<string> ReferencedIcons =>
        Flatten(Cache).Select(item => item.Icon).OfType<string>();

    private static List<Bookmark> Cache => _items ??= Load();

    /// <summary>Every entry in the tree, parents before their children.</summary>
    public static IEnumerable<Bookmark> Flatten(IEnumerable<Bookmark> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;

            if (node.Children is { } children)
            {
                foreach (var child in Flatten(children))
                {
                    yield return child;
                }
            }
        }
    }

    public static bool Contains(string? url) =>
        WebLinks.SafeUrl(url) is { } target &&
        Flatten(Cache).Any(item => !item.IsFolder &&
                                   string.Equals(item.Url, target, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds a bookmark for a page. It lands in <paramref name="parentId" />, or at the end
    /// of the bar when that is null. Starring a page that is already bookmarked does nothing;
    /// <paramref name="allowDuplicate" /> is for the entries the user asks for by hand, where the
    /// same page may well be wanted in more than one folder.</summary>
    public static async Task AddAsync(string? rawUrl, string? title, string? parentId = null, bool allowDuplicate = false)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url || (!allowDuplicate && Contains(url)))
        {
            return;
        }

        // Reuses the shared favicon cache, so a site already bookmarked or on the homepage is
        // never downloaded twice.
        var icon = await FaviconCache.EnsureAsync(url);

        await Gate.WaitAsync();

        try
        {
            if (!allowDuplicate && Contains(url))
            {
                return;
            }

            var bookmark = new Bookmark
            {
                Id = Bookmark.NewId(),
                Title = Name(title, url),
                Url = url,
                Icon = icon
            };

            if (Add(parentId, bookmark))
            {
                Commit();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Creates an empty folder and returns its id, or null when the parent has gone.</summary>
    public static async Task<string?> AddFolderAsync(string? parentId, string? name)
    {
        await Gate.WaitAsync();

        try
        {
            var folder = new Bookmark
            {
                Id = Bookmark.NewId(),
                Title = FolderName(name),
                Children = []
            };

            if (!Add(parentId, folder))
            {
                Changed?.Invoke(null, EventArgs.Empty);
                return null;
            }

            Commit();
            return folder.Id;
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Renames one entry. A blank name is refused for a bookmark and replaced with the
    /// default for a folder, so neither can end up nameless on the bar.</summary>
    public static async Task RenameAsync(string id, string? title)
    {
        await Gate.WaitAsync();

        try
        {
            if (Update(Cache, id, node => node with
                {
                    Title = node.IsFolder
                        ? FolderName(title)
                        : Name(title, node.Url)
                }) is not { } rebuilt)
            {
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }

            _items = rebuilt;
            Commit();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Removes one entry, and everything inside it when it is a folder.</summary>
    public static async Task RemoveAsync(string id)
    {
        await Gate.WaitAsync();

        try
        {
            if (Take(id) is null)
            {
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }

            Commit();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Removes every bookmark for a page, wherever it sits. This is what unstarring the
    /// open page does, which knows the URL rather than an id.</summary>
    public static async Task RemoveUrlAsync(string? rawUrl)
    {
        if (WebLinks.SafeUrl(rawUrl) is not { } url)
        {
            return;
        }

        await Gate.WaitAsync();

        try
        {
            var removed = false;

            while (Flatten(Cache).FirstOrDefault(item =>
                       !item.IsFolder &&
                       string.Equals(item.Url, url, StringComparison.OrdinalIgnoreCase)) is { } match)
            {
                if (Take(match.Id) is null)
                {
                    break;
                }

                removed = true;
            }

            if (removed)
            {
                Commit();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Moves one entry to a position inside <paramref name="parentId" />, or on the bar
    /// itself when that is null. <paramref name="index" /> counts places in the target list once the
    /// entry has been taken out of its old one, which is how a drop lands where it was released.
    ///
    /// A move whose entry or target has gone, or that would put a folder inside itself, is refused
    /// and the bar is told to reload rather than left showing an order that was never stored.</summary>
    public static async Task MoveAsync(string id, string? parentId, int index)
    {
        await Gate.WaitAsync();

        try
        {
            if (Find(id) is not { } node ||
                (parentId is not null && !IsFolderTarget(node, parentId)))
            {
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }

            if (Take(id) is null || !Add(parentId, node, index))
            {
                // Taking it out succeeded but putting it back did not, which can only happen if the
                // target went with it. Reload from what is on disk rather than drop the entry.
                _items = null;
                Changed?.Invoke(null, EventArgs.Empty);
                return;
            }

            Commit();
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Replaces icons the chrome cannot draw, which is how a bookmark saved when vectors
    /// were preferred picks up the raster icon it should have had. Only entries whose icon is
    /// present but undrawable are looked up again, so nothing already working is fetched twice.</summary>
    public static async Task RepairIconsAsync()
    {
        var stale = Flatten(Cache)
            .Where(item => item.Icon is not null && !FaviconCache.IsRenderable(item.Icon))
            .Select(item => item.Url)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (stale.Count == 0)
        {
            return;
        }

        // The lookups reach the network, so they happen before the gate is taken.
        var found = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var url in stale)
        {
            found[url] = await FaviconCache.EnsureAsync(url);
        }

        await Gate.WaitAsync();

        try
        {
            var changed = false;

            foreach (var item in Flatten(Cache).ToList())
            {
                if (item.IsFolder ||
                    !found.TryGetValue(item.Url, out var icon) ||
                    icon == item.Icon)
                {
                    continue;
                }

                if (Update(Cache, item.Id, node => node with { Icon = icon }) is { } rebuilt)
                {
                    _items = rebuilt;
                    changed = true;
                }
            }

            if (changed)
            {
                Commit();
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    private static Bookmark? Find(string id) => Flatten(Cache).FirstOrDefault(item => item.Id == id);

    /// <summary>True when the id names a folder that can receive <paramref name="node" />: a folder
    /// cannot be dropped into itself or into anything it contains.</summary>
    private static bool IsFolderTarget(Bookmark node, string parentId) =>
        Find(parentId) is { IsFolder: true } &&
        parentId != node.Id &&
        !Flatten(node.Children ?? []).Any(child => child.Id == parentId);

    /// <summary>Takes an entry out of the tree, returning it, or null when the id is unknown.</summary>
    private static Bookmark? Take(string id)
    {
        Bookmark? taken = null;

        if (Remove(Cache, id, ref taken) is not { } rebuilt)
        {
            return null;
        }

        _items = rebuilt;
        return taken;
    }

    /// <summary>Puts an entry into a folder, or on the bar when the parent is null. An index of -1
    /// appends. False means the folder is no longer there.</summary>
    private static bool Add(string? parentId, Bookmark node, int index = -1)
    {
        if (parentId is null)
        {
            var top = Cache.ToList();
            top.Insert(Place(index, top.Count), node);
            _items = top;
            return true;
        }

        if (Insert(Cache, parentId, index, node) is not { } rebuilt)
        {
            return false;
        }

        _items = rebuilt;
        return true;
    }

    private static int Place(int index, int count) => index < 0 ? count : Math.Clamp(index, 0, count);

    /// <summary>Rebuilds the branch without the entry, or returns null when it is not in it. Only
    /// the ancestors of the entry are rebuilt; every other record is reused, so a bar that already
    /// shows the result of this edit can recognise it as such.</summary>
    private static List<Bookmark>? Remove(IReadOnlyList<Bookmark> nodes, string id, ref Bookmark? taken)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Id != id)
            {
                continue;
            }

            taken = nodes[i];
            var copy = nodes.ToList();
            copy.RemoveAt(i);
            return copy;
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Children is { } children && Remove(children, id, ref taken) is { } rebuilt)
            {
                var copy = nodes.ToList();
                copy[i] = nodes[i] with { Children = rebuilt };
                return copy;
            }
        }

        return null;
    }

    /// <summary>Rebuilds the branch with the entry inserted into the named folder, or returns null
    /// when that folder is not in it.</summary>
    private static List<Bookmark>? Insert(IReadOnlyList<Bookmark> nodes, string parentId, int index, Bookmark node)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Children is not { } children)
            {
                continue;
            }

            if (nodes[i].Id == parentId)
            {
                var inner = children.ToList();
                inner.Insert(Place(index, inner.Count), node);
                var copy = nodes.ToList();
                copy[i] = nodes[i] with { Children = inner };
                return copy;
            }

            if (Insert(children, parentId, index, node) is { } rebuilt)
            {
                var copy = nodes.ToList();
                copy[i] = nodes[i] with { Children = rebuilt };
                return copy;
            }
        }

        return null;
    }

    /// <summary>Rebuilds the branch with one entry changed, or returns null when it is not in it.</summary>
    private static List<Bookmark>? Update(IReadOnlyList<Bookmark> nodes, string id, Func<Bookmark, Bookmark> change)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Id == id)
            {
                var copy = nodes.ToList();
                copy[i] = change(nodes[i]);
                return copy;
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Children is { } children && Update(children, id, change) is { } rebuilt)
            {
                var copy = nodes.ToList();
                copy[i] = nodes[i] with { Children = rebuilt };
                return copy;
            }
        }

        return null;
    }

    /// <summary>Falls back to the host when a page has no title yet.</summary>
    private static string Name(string? title, string url) =>
        !string.IsNullOrWhiteSpace(title) && title != "New Tab"
            ? title.Trim()
            : Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Bookmark";

    private static string FolderName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "New folder" : name.Trim();

    private static void Commit()
    {
        WebLinks.Write(AppPaths.BookmarksFile, Cache, "bookmarks.save");
        FaviconCache.Prune();
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static List<Bookmark> Load() =>
        Clean(WebLinks.Read<Bookmark>(AppPaths.BookmarksFile), new HashSet<string>(StringComparer.Ordinal));

    /// <summary>Validates the file, which anything running as the user can edit: a bookmark keeps
    /// only a real web URL and a plain icon file name, a folder keeps neither, and every entry ends
    /// up with an id of its own. A file written before folders existed loads as a flat bar, which is
    /// exactly what it was.</summary>
    private static List<Bookmark> Clean(IEnumerable<Bookmark> nodes, HashSet<string> ids)
    {
        var result = new List<Bookmark>();

        foreach (var node in nodes)
        {
            var id = node.Id.Length is > 0 and <= 64 && ids.Add(node.Id) ? node.Id : NewId(ids);

            if (node.Children is { } children)
            {
                result.Add(new Bookmark
                {
                    Id = id,
                    Title = FolderName(node.Title),
                    Children = Clean(children, ids)
                });

                continue;
            }

            if (WebLinks.SafeUrl(node.Url) is { } url)
            {
                result.Add(new Bookmark
                {
                    Id = id,
                    Title = string.IsNullOrWhiteSpace(node.Title) ? Name(null, url) : node.Title,
                    Url = url,
                    Icon = WebLinks.SafeIcon(node.Icon) ? node.Icon : null
                });
            }
        }

        return result;
    }

    private static string NewId(HashSet<string> ids)
    {
        var id = Bookmark.NewId();

        while (!ids.Add(id))
        {
            id = Bookmark.NewId();
        }

        return id;
    }
}
