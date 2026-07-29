using System.Collections;
using System.Collections.ObjectModel;
using Breeze.Models;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>A bookmark folder. It is both a row, shown on the bar or inside another folder's menu,
/// and a surface of its own: whatever its menu shows can be rearranged, and entries can be dropped
/// into it or taken out of it.</summary>
public sealed class BookmarkFolderViewModel : BookmarkRowViewModel, IBookmarkFolder
{
    private readonly IBookmarkActions _actions;

    internal BookmarkFolderViewModel(Bookmark folder, IBookmarkActions actions)
        : base(folder)
    {
        _actions = actions;

        Actions =
        [
            new BookmarkAction("Open All", new RelayCommand(() => actions.OpenAll(this))),
            new BookmarkAction("New Bookmark", new RelayCommand(() => actions.NewBookmark(this))),
            new BookmarkAction("New Folder", new RelayCommand(() => actions.NewFolder(this))),
            new BookmarkAction("Rename", new RelayCommand(() => actions.Rename(this))),
            new BookmarkAction("Delete", new RelayCommand(() => actions.Delete(this)))
        ];
    }

    /// <summary>What the folder's menu shows, in order. Kept in place across store changes so a menu
    /// that is open stays open and keeps working.</summary>
    public ObservableCollection<BookmarkRowViewModel> Children { get; } = [];

    public bool IsEmpty => Children.Count == 0;

    IEnumerable IBookmarkFolder.Entries => Children;

    public string? IdAt(int index) => (uint)index < (uint)Children.Count ? Children[index].Id : null;

    public bool IsFolder(int index) =>
        (uint)index < (uint)Children.Count && Children[index] is BookmarkFolderViewModel;

    public void Reorder(int from, int to)
    {
        if (IdAt(from) is not { } id ||
            from == to ||
            (uint)to >= (uint)Children.Count)
        {
            return;
        }

        // Shown first, stored second: the menu keeps the order the drop left on screen, and the
        // store refuses the move if the entry has since gone, which reloads the rows.
        Children.Move(from, to);
        _actions.Move(id, Id, to);
    }

    public void DropInto(int from, int index)
    {
        if (IdAt(from) is { } id && IdAt(index) is { } folder && IsFolder(index))
        {
            _actions.Move(id, folder, -1);
        }
    }

    public void Adopt(string id, int index) => _actions.Move(id, Id, index);
}
