namespace Breeze.Models;

/// <summary>Implemented by view models that drive a web view; the view assigns itself on attach.</summary>
public interface IWebNavigatorHost
{
    IWebNavigator? Navigator { get; set; }
}
