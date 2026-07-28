namespace Breeze.ViewModels;

/// <summary>One row on the keyboard shortcuts page: what the shortcut does and the gesture in force.</summary>
public sealed record ShortcutRowViewModel(string Label, string Gesture);
