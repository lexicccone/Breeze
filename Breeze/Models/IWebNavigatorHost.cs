namespace Breeze.Models;

/// <summary>Implemented by view models that drive a web view; the view assigns itself on attach.</summary>
public interface IWebNavigatorHost
{
    IWebNavigator? Navigator { get; set; }

    /// <summary>Asks for a URL the page wanted to open in a new window to be shown in a tab,
    /// so it always appears inside Breeze's chrome.</summary>
    void RequestTab(string url);
}
