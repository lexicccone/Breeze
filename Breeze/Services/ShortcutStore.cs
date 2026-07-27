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
    public static async Task<IReadOnlyList<Shortcut>> SaveAsync(int index, string name, string url)
    {
        var items = Cache;
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
            return items?.Where(Valid).ToList() ?? [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static bool Valid(Shortcut shortcut) =>
        !string.IsNullOrWhiteSpace(shortcut.Name) && !string.IsNullOrWhiteSpace(shortcut.Url);

    private static void Persist(List<Shortcut> items)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(AppPaths.ShortcutsFile, JsonSerializer.Serialize(items, Options));
        }
        catch (Exception error)
        {
            ErrorLog.Write("shortcuts.save", error);
        }
    }
}
