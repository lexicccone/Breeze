namespace Breeze.ViewModels;

/// <summary>A tab that hosts the native settings page; it never creates a web view.</summary>
public sealed class SettingsTabViewModel(Action<TabViewModel> close) : TabViewModel(close)
{
    public SettingsViewModel Settings { get; } = new();

    public override string Title => "Settings";

    public override bool IsWebPage => false;
}
