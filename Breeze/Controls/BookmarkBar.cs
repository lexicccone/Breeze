using Avalonia.Controls;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>Bookmark bar. Reordering by dragging works exactly as it does in the tab strip, both
/// going through <see cref="DragReorder" />; entries differ in width, which that handles.</summary>
public sealed class BookmarkBar : ItemsControl
{
    public BookmarkBar() =>
        DragReorder.Attach(this, (from, to) => (DataContext as IBookmarkReorder)?.MoveBookmark(from, to));

    protected override Type StyleKeyOverride => typeof(ItemsControl);
}
