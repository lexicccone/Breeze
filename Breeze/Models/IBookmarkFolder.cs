using System.Collections;

namespace Breeze.Models;

/// <summary>A bookmark folder as the chrome sees it: what to show inside it, and what a drag inside
/// it means. Keeps the menu control clear of view model types, the way the tab strip is.</summary>
public interface IBookmarkFolder : IBookmarkSurface
{
    string Title { get; }

    /// <summary>Rows to show, in order. Observable, so a menu that is open follows changes.</summary>
    IEnumerable Entries { get; }

    bool IsEmpty { get; }
}
