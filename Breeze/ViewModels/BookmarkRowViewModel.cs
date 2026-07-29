using Breeze.Models;

namespace Breeze.ViewModels;

/// <summary>One entry on the bookmark bar or inside a folder menu. Titles and icons never change in
/// place: the store raises its change and the row is built again, which keeps a row and the entry it
/// shows in step without every field needing to be observable.</summary>
public abstract class BookmarkRowViewModel(Bookmark source)
{
    /// <summary>The stored entry this row was built from.</summary>
    public Bookmark Source { get; } = source;

    public string Id => Source.Id;

    public string Title => Source.Title;

    /// <summary>Right click actions, in the order they are shown.</summary>
    public IReadOnlyList<BookmarkAction> Actions { get; protected init; } = [];
}
