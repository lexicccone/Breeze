using System.Text.Json;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>Start page shortcuts, stored as readable JSON in the local Breeze folder.
/// Mutations are serialized and carry the revision the caller last saw, so two open start pages
/// cannot lose an update or act on a stale index.</summary>
public static class ShortcutStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static List<Shortcut>? _items;

    public static IReadOnlyList<Shortcut> Items => Cache;

    /// <summary>Increments on every accepted change. A caller passing an older revision is
    /// working from a stale view of the list and is refused.</summary>
    public static int Revision { get; private set; }

    private static List<Shortcut> Cache => _items ??= Load();

    /// <summary>Adds a shortcut, or replaces the one at <paramref name="index" /> when editing.</summary>
    public static async Task SaveAsync(int revision, int index, string name, string rawUrl)
    {
        if (SafeUrl(rawUrl) is not { } url || string.IsNullOrWhiteSpace(name))
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
        Persist(items);
        FaviconCache.Prune(items.Select(item => item.Icon).OfType<string>());
    }

    private static List<Shortcut> Load()
    {
        try
        {
            if (!File.Exists(AppPaths.ShortcutsFile))
            {
                return [];
            }

            var items = JsonSerializer.Deserialize<List<Shortcut>>(File.ReadAllText(AppPaths.ShortcutsFile), Options);
            return items?.Where(Valid).Select(Sanitize).ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool Valid(Shortcut shortcut) =>
        !string.IsNullOrWhiteSpace(shortcut.Name) && SafeUrl(shortcut.Url) is not null;

    /// <summary>Drops an icon reference that does not name a plain file in the cache folder.</summary>
    private static Shortcut Sanitize(Shortcut shortcut) =>
        shortcut.Icon is null || SafeIconName(shortcut.Icon)
            ? shortcut
            : shortcut with { Icon = null };

    private static bool SafeIconName(string icon) =>
        icon.Length <= 128 &&
        icon == Path.GetFileName(icon) &&
        icon.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        !icon.Contains("..", StringComparison.Ordinal);

    /// <summary>Shortcuts may only point at web pages. The stored file is untrusted input, so
    /// a scheme such as javascript: must never reach the privileged start page origin.</summary>
    private static string? SafeUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.AbsoluteUri
            : null;

    private static void Persist(List<Shortcut> items)
    {
        try
        {
            AppPaths.WriteAtomic(AppPaths.ShortcutsFile, JsonSerializer.Serialize(items, Options));
        }
        catch (Exception error)
        {
            ErrorLog.Write("shortcuts.save", error);
        }
    }
}
