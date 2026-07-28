namespace Breeze.Models;

/// <summary>How the bookmark bar hands a finished drag back to its view model, mirroring
/// <see cref="ITabReorder" />. The control never sees a concrete view model type.</summary>
public interface IBookmarkReorder
{
    void MoveBookmark(int oldIndex, int newIndex);
}
