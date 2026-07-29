using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>Everything the bookmark bar shows and everything its entries can be asked to do. The
/// rows mirror the stored tree and are reconciled in place rather than rebuilt wholesale, so a
/// folder menu that is open stays open, and a drag that has already moved a row on screen does not
/// see it jump when the store confirms the move.</summary>
public sealed class BookmarksViewModel : ViewModelBase, IBookmarkSurface, IBookmarkActions
{
    private readonly Action<string> _open;
    private readonly Action<string> _openInNewTab;

    public BookmarksViewModel(Action<string> open, Action<string> openInNewTab)
    {
        _open = open;
        _openInNewTab = openInNewTab;

        NewBookmarkCommand = new RelayCommand(() => NewBookmark(null));
        NewFolderCommand = new RelayCommand(() => NewFolder(null));

        // One window lives for the life of the process, so this subscription is never removed.
        BookmarkStore.Changed += OnStoreChanged;
        Reload();
    }

    /// <summary>Raised when an action needs an answer from the user. The window shows the dialog;
    /// the view model neither creates nor knows about windows.</summary>
    public event EventHandler<BookmarkPromptViewModel>? PromptRequested;

    /// <summary>Top level entries, in bar order.</summary>
    public ObservableCollection<BookmarkRowViewModel> Rows { get; } = [];

    /// <summary>Adds a bookmark to the bar itself, from the bar's own context menu.</summary>
    public ICommand NewBookmarkCommand { get; }

    /// <summary>Adds a folder to the bar itself, from the bar's own context menu.</summary>
    public ICommand NewFolderCommand { get; }

    public string? IdAt(int index) => (uint)index < (uint)Rows.Count ? Rows[index].Id : null;

    public bool IsFolder(int index) =>
        (uint)index < (uint)Rows.Count && Rows[index] is BookmarkFolderViewModel;

    public void Reorder(int from, int to)
    {
        if (IdAt(from) is not { } id || from == to || (uint)to >= (uint)Rows.Count)
        {
            return;
        }

        // Shown first, stored second, so the bar keeps the order the drop left on screen.
        Rows.Move(from, to);
        Move(id, null, to);
    }

    public void DropInto(int from, int index)
    {
        if (IdAt(from) is { } id && IsFolder(index) && IdAt(index) is { } folder)
        {
            Move(id, folder, -1);
        }
    }

    public void Adopt(string id, int index) => Move(id, null, index);

    public void Move(string id, string? parentId, int index) =>
        _ = BookmarkStore.MoveAsync(id, parentId, index);

    void IBookmarkActions.Open(BookmarkViewModel bookmark) => _open(bookmark.Url);

    void IBookmarkActions.OpenInNewTab(BookmarkViewModel bookmark) => _openInNewTab(bookmark.Url);

    /// <summary>Opens every bookmark in the folder, and in the folders inside it, each in a tab of
    /// its own and in the order they are shown.</summary>
    void IBookmarkActions.OpenAll(BookmarkFolderViewModel folder)
    {
        foreach (var bookmark in Descendants(folder.Children).OfType<BookmarkViewModel>())
        {
            _openInNewTab(bookmark.Url);
        }
    }

    void IBookmarkActions.Rename(BookmarkRowViewModel row)
    {
        var folder = row is BookmarkFolderViewModel;

        var prompt = new BookmarkPromptViewModel(folder ? "Rename folder" : "Rename bookmark", "Save", entered =>
        {
            if (string.IsNullOrWhiteSpace(entered.Name))
            {
                return "Enter a name.";
            }

            _ = BookmarkStore.RenameAsync(row.Id, entered.Name);
            return null;
        })
        {
            HasName = true,
            Name = row.Title
        };

        PromptRequested?.Invoke(this, prompt);
    }

    void IBookmarkActions.Delete(BookmarkRowViewModel row)
    {
        // A folder with something in it is worth asking about; anything else goes at once, as
        // deleting a single bookmark always has.
        if (row is not BookmarkFolderViewModel { IsEmpty: false } folder)
        {
            _ = BookmarkStore.RemoveAsync(row.Id);
            return;
        }

        var count = Descendants(folder.Children).Count();

        var prompt = new BookmarkPromptViewModel("Delete folder", "Delete", answered =>
        {
            _ = BookmarkStore.RemoveAsync(row.Id);
            return null;
        })
        {
            Message = $"Delete \"{folder.Title}\" and the {(count == 1 ? "one entry" : $"{count} entries")} inside it?"
        };

        PromptRequested?.Invoke(this, prompt);
    }

    void IBookmarkActions.MoveToFolder(BookmarkRowViewModel row)
    {
        var choices = new List<BookmarkFolderChoice> { new("Bookmark bar", null) };
        Collect(Rows, string.Empty, row, choices);

        var prompt = new BookmarkPromptViewModel("Move to folder", "Move", entered =>
        {
            Move(row.Id, entered.SelectedFolder?.Id, -1);
            return null;
        })
        {
            Folders = choices
        };

        prompt.SelectedFolder = choices[0];
        PromptRequested?.Invoke(this, prompt);
    }

    void IBookmarkActions.NewBookmark(BookmarkFolderViewModel? folder) => NewBookmark(folder);

    void IBookmarkActions.NewFolder(BookmarkFolderViewModel? folder) => NewFolder(folder);

    private void NewBookmark(BookmarkFolderViewModel? folder)
    {
        var prompt = new BookmarkPromptViewModel("New bookmark", "Add", entered =>
        {
            if (string.IsNullOrWhiteSpace(entered.Name))
            {
                return "Enter a name.";
            }

            if (Address(entered.Url) is not { } url)
            {
                return "Enter a web address, such as example.com.";
            }

            _ = BookmarkStore.AddAsync(url, entered.Name, folder?.Id, allowDuplicate: true);
            return null;
        })
        {
            HasName = true,
            HasUrl = true
        };

        PromptRequested?.Invoke(this, prompt);
    }

    private void NewFolder(BookmarkFolderViewModel? folder)
    {
        var prompt = new BookmarkPromptViewModel("New folder", "Create", entered =>
        {
            if (string.IsNullOrWhiteSpace(entered.Name))
            {
                return "Enter a name.";
            }

            _ = BookmarkStore.AddFolderAsync(folder?.Id, entered.Name);
            return null;
        })
        {
            HasName = true,
            Name = "New folder"
        };

        PromptRequested?.Invoke(this, prompt);
    }

    /// <summary>Accepts an address with or without a scheme, and nothing that is not a web page.</summary>
    private static string? Address(string? text)
    {
        var typed = text?.Trim();

        if (string.IsNullOrEmpty(typed))
        {
            return null;
        }

        return WebLinks.SafeUrl(typed) ?? WebLinks.SafeUrl("https://" + typed);
    }

    /// <summary>Every folder that could receive <paramref name="moved" />, labelled with its path. A
    /// folder cannot be moved into itself or into anything it contains, so those are left out.</summary>
    private static void Collect(
        IEnumerable<BookmarkRowViewModel> rows,
        string path,
        BookmarkRowViewModel moved,
        List<BookmarkFolderChoice> choices)
    {
        foreach (var row in rows.OfType<BookmarkFolderViewModel>())
        {
            if (row.Id == moved.Id)
            {
                continue;
            }

            var label = path.Length == 0 ? row.Title : path + " / " + row.Title;
            choices.Add(new BookmarkFolderChoice(label, row.Id));
            Collect(row.Children, label, moved, choices);
        }
    }

    private static IEnumerable<BookmarkRowViewModel> Descendants(IEnumerable<BookmarkRowViewModel> rows)
    {
        foreach (var row in rows)
        {
            yield return row;

            if (row is BookmarkFolderViewModel folder)
            {
                foreach (var child in Descendants(folder.Children))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>The store completes its work off the UI thread, so the update is marshalled back.</summary>
    private void OnStoreChanged(object? sender, EventArgs args) => Dispatcher.UIThread.Invoke(Reload);

    private void Reload() => Sync(Rows, BookmarkStore.Items);

    /// <summary>Brings one list of rows in line with what is stored, reusing every row whose entry
    /// has not changed. A folder keeps its identity, and therefore its open menu, when only its
    /// contents moved.</summary>
    private void Sync(ObservableCollection<BookmarkRowViewModel> rows, IReadOnlyList<Bookmark> nodes)
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (!nodes.Any(node => node.Id == rows[i].Id))
            {
                rows.RemoveAt(i);
            }
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var at = IndexOf(rows, node.Id);

            if (at < 0)
            {
                rows.Insert(Math.Min(index, rows.Count), Build(node));
                continue;
            }

            if (rows[at] is BookmarkFolderViewModel folder && node.IsFolder && Same(folder.Source, node))
            {
                Sync(folder.Children, node.Children ?? []);
            }
            else if (!Same(rows[at].Source, node) || rows[at].Source.IsFolder != node.IsFolder)
            {
                rows[at] = Build(node);
            }

            if (at != index)
            {
                rows.Move(at, index);
            }
        }
    }

    private BookmarkRowViewModel Build(Bookmark node)
    {
        if (!node.IsFolder)
        {
            return new BookmarkViewModel(node, this);
        }

        var folder = new BookmarkFolderViewModel(node, this);
        Sync(folder.Children, node.Children ?? []);
        return folder;
    }

    /// <summary>True when two records describe the same entry, ignoring what is inside a folder.</summary>
    private static bool Same(Bookmark row, Bookmark node) =>
        row.Id == node.Id && row.Title == node.Title && row.Url == node.Url && row.Icon == node.Icon;

    private static int IndexOf(ObservableCollection<BookmarkRowViewModel> rows, string id)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}

