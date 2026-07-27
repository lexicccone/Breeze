namespace Breeze.Models;

/// <summary>Navigation surface a hosted web view exposes to the view model that owns it.</summary>
public interface IWebNavigator
{
    /// <summary>URL of the current entry, or null when nothing has loaded.</summary>
    string? CurrentUrl { get; }

    /// <summary>URL one step back in this tab's history, or null when there is none.</summary>
    string? PreviousUrl { get; }

    /// <summary>URL one step forward in this tab's history, or null when there is none.</summary>
    string? NextUrl { get; }

    void GoBack();

    void GoForward();

    void Reload();

    void Stop();
}
