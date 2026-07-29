using Avalonia.Controls;
using Avalonia.Input;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>A folder on the bookmark bar. Clicking it shows what is inside; while a folder menu is
/// already open, moving the pointer onto another folder hands the menu straight over, which is how
/// a menu bar behaves and what makes moving between folders feel immediate.</summary>
public sealed class BookmarkFolderButton : Button
{
    protected override Type StyleKeyOverride => typeof(Button);

    private IBookmarkFolder? Folder => DataContext as IBookmarkFolder;

    protected override void OnClick() => BookmarkMenu.Toggle(this, Folder);

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);

        if (BookmarkMenu.IsOpen && !BookmarkMenu.IsOpenFor(this))
        {
            BookmarkMenu.Show(this, Folder);
        }
    }
}
