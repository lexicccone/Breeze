namespace Breeze.ViewModels;

/// <summary>A tab that hosts the native settings page; it never creates a web view. Settings that
/// the window must react to immediately report back through <paramref name="bookmarkBarChanged" />
/// instead of the window observing the store, so no settings page outlives its tab.</summary>
public sealed class SettingsTabViewModel(Action<TabViewModel> close, Action? bookmarkBarChanged = null)
    : TabViewModel(close)
{
    public SettingsViewModel Settings { get; } = new(bookmarkBarChanged);

    public override string Title => "Settings";

    public override bool IsWebPage => false;
}
