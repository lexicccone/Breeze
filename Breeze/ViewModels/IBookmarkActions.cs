namespace Breeze.ViewModels;

/// <summary>What a bookmark row can ask for. Rows are rebuilt whenever the store changes, so they
/// hold no state of their own beyond the entry they show and call back here instead.</summary>
internal interface IBookmarkActions
{
    void Open(BookmarkViewModel bookmark);

    void OpenInNewTab(BookmarkViewModel bookmark);

    void OpenAll(BookmarkFolderViewModel folder);

    void Rename(BookmarkRowViewModel row);

    void Delete(BookmarkRowViewModel row);

    void MoveToFolder(BookmarkRowViewModel row);

    /// <summary>Asks for a new bookmark inside a folder, or on the bar when it is null.</summary>
    void NewBookmark(BookmarkFolderViewModel? folder);

    void NewFolder(BookmarkFolderViewModel? folder);

    /// <summary>Moves an entry into a folder, or onto the bar when the parent is null. An index of
    /// -1 puts it at the end.</summary>
    void Move(string id, string? parentId, int index);
}
