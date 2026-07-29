using System.Windows.Input;
using Avalonia.Media;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>A bookmarked page, on the bar or inside a folder. The icon comes from the shared favicon
/// image cache, so a site already shown elsewhere is neither downloaded nor decoded again.</summary>
public sealed class BookmarkViewModel : BookmarkRowViewModel
{
    internal BookmarkViewModel(Bookmark bookmark, IBookmarkActions actions)
        : base(bookmark)
    {
        Url = bookmark.Url;
        Icon = FaviconImages.Load(bookmark.Icon);
        OpenCommand = new RelayCommand(() => actions.Open(this));
        OpenInNewTabCommand = new RelayCommand(() => actions.OpenInNewTab(this));

        Actions =
        [
            new BookmarkAction("Open", OpenCommand),
            new BookmarkAction("Open in New Tab", OpenInNewTabCommand),
            new BookmarkAction("Rename", new RelayCommand(() => actions.Rename(this))),
            new BookmarkAction("Delete", new RelayCommand(() => actions.Delete(this))),
            new BookmarkAction("Move to Folder...", new RelayCommand(() => actions.MoveToFolder(this)))
        ];
    }

    public string Url { get; }

    public IImage? Icon { get; }

    /// <summary>False when the site has no usable icon and the default glyph is shown instead.</summary>
    public bool HasIcon => Icon is not null;

    public ICommand OpenCommand { get; }

    public ICommand OpenInNewTabCommand { get; }
}
