using System.Text.Json;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>Start page shortcuts, stored as readable JSON in the local Breeze folder.</summary>
public static class ShortcutStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private static List<Shortcut>? _items;

    public static IReadOnlyList<Shortcut> Items => Cache;

    private static List<Shortcut> Cache => _items ??= Load();

    /// <summary>Adds a shortcut, or replaces the one at <paramref name="index" /> when editing.</summary>
    public static async Task<IReadOnlyList<Shortcut>> SaveAsync(int index, string name, string rawUrl)
    {
        var items = Cache;

        if (SafeUrl(rawUrl) is not { } url || string.IsNullOrWhiteSpace(name))
        {
            return items;
        }

        var existing = index >= 0 && index < items.Count ? items[index] : null;
        var reusable = existing?.Url == url && FaviconCache.IsCached(existing.Icon);
        var icon = reusable ? existing!.Icon : await FaviconCache.EnsureAsync(url);
        var shortcut = new Shortcut { Name = name, Url = url, Icon = icon };

        if (existing is null)
        {
            items.Add(shortcut);
        }
        else
        {
            items[index] = shortcut;
        }

        Persist(items);
        return items;
    }

    public static IReadOnlyList<Shortcut> Remove(int index)
    {
        var items = Cache;
        if (index >= 0 && index < items.Count)
        {
            items.RemoveAt(index);
            Persist(items);
        }

        return items;
    }

    public static IReadOnlyList<Shortcut> Move(int from, int to)
    {
        var items = Cache;
        if (from == to || (uint)from >= (uint)items.Count || (uint)to >= (uint)items.Count)
        {
            return items;
        }

        var shortcut = items[from];
        items.RemoveAt(from);
        items.Insert(to, shortcut);
        Persist(items);
        return items;
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
