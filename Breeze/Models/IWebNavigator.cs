namespace Breeze.Models;

/// <summary>Navigation surface a hosted web view exposes to the view model that owns it.</summary>
public interface IWebNavigator
{
    void GoBack();

    void GoForward();

    void Reload();

    void Stop();
}
