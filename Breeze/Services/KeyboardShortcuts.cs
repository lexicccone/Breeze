using Avalonia.Input;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>Catalog of keyboard shortcuts, the gestures in force, and what each one does. Key
/// presses reach Breeze from two places, the window and the web engine, and both resolve them
/// here, so a shortcut is defined once. Adding one means adding a definition and registering a
/// handler for it.</summary>
public static class KeyboardShortcuts
{
    public const string ToggleBookmarkBar = "toggleBookmarkBar";

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

        var gesture =
            Parse(SettingsStore.Current.Shortcuts.GetValueOrDefault(id)) ??
            Parse(Definitions.GetValueOrDefault(id)?.DefaultGesture) ??
            new KeyGesture(Key.None);

        Resolved[id] = gesture;
        return gesture;
    }

    /// <summary>Gesture in force, formatted for display.</summary>
    public static string Text(string id) => Gesture(id).ToString();

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
