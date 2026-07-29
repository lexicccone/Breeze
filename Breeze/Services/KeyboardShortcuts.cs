using Avalonia.Input;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>Catalog of keyboard shortcuts, the gestures in force, and what each one does. Key
/// presses reach Breeze from two places, the window and the web engine, and both resolve them
/// here, so a shortcut is defined once. Adding one means adding a definition and registering a
/// handler for it: the settings page is driven by this catalog, so it needs no change.</summary>
public static class KeyboardShortcuts
{
    public const string ToggleBookmarkBar = "toggleBookmarkBar";

    /// <summary>Stored for a shortcut the user has deliberately left unbound. It resolves to a
    /// gesture no key press can match, rather than to the default.</summary>
    private const string Unbound = "None";

    public static IReadOnlyList<ShortcutDefinition> All { get; } =
    [
        new()
        {
            Id = ToggleBookmarkBar,
            Label = "Toggle bookmark bar",
            DefaultGesture = "Ctrl+Shift+B"
        }
    ];

    private static readonly Dictionary<string, ShortcutDefinition> Definitions =
        All.ToDictionary(definition => definition.Id, StringComparer.Ordinal);

    private static readonly Dictionary<string, KeyGesture> Resolved = new(StringComparer.Ordinal);

    private static readonly Dictionary<string, Action> Handlers = new(StringComparer.Ordinal);

    /// <summary>Binds a shortcut to the action it performs. Breeze has a single main window, which
    /// registers every handler while it is built.</summary>
    public static void Register(string id, Action action) => Handlers[id] = action;

    /// <summary>What a key press should do, or null when no shortcut claims it.</summary>
    public static Action? Lookup(Key key, KeyModifiers modifiers)
    {
        if (key == Key.None)
        {
            return null;
        }

        foreach (var (id, action) in Handlers)
        {
            var gesture = Gesture(id);

            if (gesture.Key == key && gesture.KeyModifiers == modifiers)
            {
                return action;
            }
        }

        return null;
    }

    /// <summary>Gesture for a shortcut: the user's override when it parses, otherwise the default.
    /// The settings file is editable by hand, so an unusable value must not break key handling.
    /// Results are cached, so nothing is parsed or allocated while keys are being pressed.</summary>
    public static KeyGesture Gesture(string id)
    {
        if (Resolved.TryGetValue(id, out var known))
        {
            return known;
        }

        var stored = SettingsStore.Current.Shortcuts.GetValueOrDefault(id);

        // The unbound marker is recognised rather than parsed, so a shortcut the user cleared can
        // never fall back to the default it was cleared from.
        var gesture = stored == Unbound
            ? new KeyGesture(Key.None)
            : Parse(stored) ?? Parse(Definitions.GetValueOrDefault(id)?.DefaultGesture) ?? new KeyGesture(Key.None);

        Resolved[id] = gesture;
        return gesture;
    }

    /// <summary>Gesture in force, formatted for display.</summary>
    public static string Text(string id) => IsBound(id) ? Gesture(id).ToString() : "Not set";

    /// <summary>False when the shortcut has been cleared and no key press performs it.</summary>
    public static bool IsBound(string id) => Gesture(id).Key != Key.None;

    /// <summary>True when the shortcut still has the gesture Breeze ships with.</summary>
    public static bool IsDefault(string id) => !SettingsStore.Current.Shortcuts.ContainsKey(id);

    /// <summary>Gives a shortcut a new gesture, saving at once, or explains why it cannot have it.
    /// A combination another action already answers to is refused rather than taken from it, and the
    /// action that has it is named through <paramref name="usedBy" />.</summary>
    public static ShortcutProblem Assign(string id, string gesture, out string? usedBy)
    {
        usedBy = null;

        if (!Definitions.TryGetValue(id, out var definition) ||
            Parse(gesture) is not { } wanted ||
            wanted.Key == Key.None)
        {
            return ShortcutProblem.Unusable;
        }

        if (wanted.KeyModifiers == KeyModifiers.None)
        {
            return ShortcutProblem.NeedsModifier;
        }

        foreach (var other in All)
        {
            if (other.Id == id)
            {
                continue;
            }

            var taken = Gesture(other.Id);

            if (taken.Key == wanted.Key && taken.KeyModifiers == wanted.KeyModifiers)
            {
                usedBy = other.Label;
                return ShortcutProblem.AlreadyUsed;
            }
        }

        // Choosing the shipped gesture is the same as never having changed it, so no override is
        // kept and the row reports itself as default again.
        if (Parse(definition.DefaultGesture) is { } shipped &&
            shipped.Key == wanted.Key &&
            shipped.KeyModifiers == wanted.KeyModifiers)
        {
            Reset(id);
            return ShortcutProblem.None;
        }

        Store(id, wanted.ToString());
        return ShortcutProblem.None;
    }

    /// <summary>Leaves a shortcut with no gesture at all.</summary>
    public static void Clear(string id)
    {
        if (Definitions.ContainsKey(id))
        {
            Store(id, Unbound);
        }
    }

    /// <summary>Puts a shortcut back to the gesture Breeze ships with.</summary>
    public static void Reset(string id)
    {
        if (SettingsStore.Current.Shortcuts.Remove(id))
        {
            SettingsStore.Save();
        }

        Resolved.Remove(id);
    }

    private static void Store(string id, string gesture)
    {
        SettingsStore.Current.Shortcuts[id] = gesture;
        SettingsStore.Save();

        // Dropping the cached gesture is what makes the change take effect on the next key press.
        Resolved.Remove(id);
    }

    private static KeyGesture? Parse(string? gesture)
    {
        if (string.IsNullOrWhiteSpace(gesture))
        {
            return null;
        }

        try
        {
            return KeyGesture.Parse(gesture);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
