using System.Windows.Input;
using Avalonia.Media;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>One entry on the bookmark bar. The icon comes from the shared favicon image cache, so
/// a site already shown elsewhere is neither downloaded nor decoded again.</summary>
public sealed class BookmarkViewModel
{
    public BookmarkViewModel(Bookmark bookmark, Action<string> open, Action<string> openInNewTab, Action<string> delete)
    {
        Source = bookmark;
        Title = bookmark.Title;
        Url = bookmark.Url;
        Icon = FaviconImages.Load(bookmark.Icon);
        OpenCommand = new RelayCommand(() => open(Url));
        OpenInNewTabCommand = new RelayCommand(() => openInNewTab(Url));
        DeleteCommand = new RelayCommand(() => delete(Url));
    }

    /// <summary>The stored bookmark this row was built from. Lets the window tell a change it has
    /// already applied, a drag for instance, from one that needs the rows rebuilt.</summary>
    public Bookmark Source { get; }

    public string Title { get; }

    public string Url { get; }

    public IImage? Icon { get; }

    /// <summary>False when the site has no usable icon and the default glyph is shown instead.</summary>
    public bool HasIcon => Icon is not null;

    public ICommand OpenCommand { get; }

    public ICommand OpenInNewTabCommand { get; }

    public ICommand DeleteCommand { get; }
}
