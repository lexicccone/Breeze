namespace Breeze.Models;

/// <summary>One keyboard shortcut Breeze can perform. The catalog of definitions is the only
/// place a gesture is written down, so adding a shortcut means adding an entry and a command.</summary>
public sealed record ShortcutDefinition
{
    /// <summary>Stable key used in the settings file; never shown to the user.</summary>
    public required string Id { get; init; }

    public required string Label { get; init; }

    /// <summary>Gesture used when the user has not chosen one, in Avalonia gesture notation.</summary>
    public required string DefaultGesture { get; init; }
}
