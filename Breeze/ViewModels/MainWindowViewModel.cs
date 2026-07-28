using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Threading;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, ITabReorder, IBookmarkReorder
{
    /// <summary>Rows kept in the popup. Old finished rows drop off rather than accumulating.</summary>
    private const int MaxDownloadRows = 20;

    private TabViewModel? _selectedTab;
    private bool _reordering;
    private bool _isDownloadsOpen;

    public MainWindowViewModel()
    {
        NewTabCommand = new RelayCommand(NewTab);
        SettingsCommand = new RelayCommand(OpenSettings);
        ToggleBookmarkCommand = new RelayCommand(ToggleBookmark);
        KeyboardShortcuts.Register(KeyboardShortcuts.ToggleBookmarkBar, ToggleBookmarkBar);

        ToggleDownloadsCommand = new RelayCommand(() => IsDownloadsOpen = !IsDownloadsOpen);

        // One window lives for the life of the process, so these subscriptions are never removed.
        DownloadCenter.Started += OnDownloadStarted;
        BookmarkStore.Changed += OnBookmarksChanged;
        LoadBookmarks();

        Add(new TabViewModel(CloseTab, SettingsStore.StartupAddress(), OpenTab));
    }

    /// <summary>Raised when the last tab is closed and the window should follow.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Tabs in strip order; dragging reorders this list.</summary>
    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    /// <summary>Same tabs in creation order. Hosting the web views here keeps their
    /// containers, and therefore the WebView2 instances, untouched while tabs are reordered.</summary>
    public ObservableCollection<TabViewModel> HostedTabs { get; } = [];

    /// <summary>Bookmarks in stored order, rebuilt whenever the store changes.</summary>
    public ObservableCollection<BookmarkViewModel> Bookmarks { get; } = [];

    public ICommand NewTabCommand { get; }

    public ICommand SettingsCommand { get; }

    public ICommand ToggleBookmarkCommand { get; }

    public ICommand ToggleDownloadsCommand { get; }

    /// <summary>Downloads of this session, newest first. The foundation for a downloads manager:
    /// the popup only renders this list, and the entries know nothing about where they came from.</summary>
    public ObservableCollection<DownloadViewModel> Downloads { get; } = [];

    public bool HasDownloads => Downloads.Count > 0;

    public bool IsDownloadsOpen
    {
        get => _isDownloadsOpen;
        set => SetProperty(ref _isDownloadsOpen, value);
    }

    public bool IsBookmarkBarVisible => SettingsStore.Current.ShowBookmarkBar;

    /// <summary>True when the selected tab shows a page that can be bookmarked. Internal pages,
    /// blank tabs and the settings tab are excluded.</summary>
    public bool CanBookmark =>
        SelectedTab is { IsWebPage: true } tab &&
        WebLinks.SafeUrl(tab.Address) is { } url &&
        !StartPage.IsInternal(url);

    public bool IsCurrentPageBookmarked => CanBookmark && BookmarkStore.Contains(SelectedTab!.Address);

    public TabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_reordering)
            {
                return;
            }

            var previous = _selectedTab;
            if (!SetProperty(ref _selectedTab, value))
            {
                return;
            }

            if (previous is not null)
            {
                previous.IsSelected = false;
                previous.PropertyChanged -= OnSelectedTabPropertyChanged;
            }

            if (value is not null)
            {
                value.IsSelected = true;
                value.PropertyChanged += OnSelectedTabPropertyChanged;
            }

            RefreshBookmarkState();
        }
    }

    public void MoveTab(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex ||
            (uint)oldIndex >= (uint)Tabs.Count ||
            (uint)newIndex >= (uint)Tabs.Count)
        {
            return;
        }

        // The list box clears its selection while items move. Ignoring that write keeps the
        // selected tab untouched, so its web view is never hidden and shown again mid drag.
        _reordering = true;
        Tabs.Move(oldIndex, newIndex);
        _reordering = false;
        OnPropertyChanged(nameof(SelectedTab));
    }

    /// <summary>Applies a finished bookmark drag. The row moves in this list first, so the bar keeps
    /// the order the drop left on screen, and the store is told which entry moved so it can refuse a
    /// move made from a stale view.</summary>
    public void MoveBookmark(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex ||
            (uint)oldIndex >= (uint)Bookmarks.Count ||
            (uint)newIndex >= (uint)Bookmarks.Count)
        {
            return;
        }

        var url = Bookmarks[oldIndex].Url;
        Bookmarks.Move(oldIndex, newIndex);
        _ = BookmarkStore.MoveAsync(url, oldIndex, newIndex);
    }

    private void NewTab() => Add(new TabViewModel(CloseTab, StartPage.Url, OpenTab));

    /// <summary>Shows a URL a page asked to open in a new window as a tab instead.</summary>
    private void OpenTab(string url) => Add(new TabViewModel(CloseTab, url, OpenTab));

    /// <summary>Focuses the open settings tab, or opens the single allowed one.</summary>
    private void OpenSettings()
    {
        if (Tabs.OfType<SettingsTabViewModel>().FirstOrDefault() is { } existing)
        {
            SelectedTab = existing;
            return;
        }

        Add(new SettingsTabViewModel(CloseTab, () => OnPropertyChanged(nameof(IsBookmarkBarVisible))));
    }

    /// <summary>Opens a bookmark in the current tab, or in a new one when the selected tab cannot
    /// navigate, which is the case for the settings tab.</summary>
    private void OpenBookmark(string url)
    {
        if (SelectedTab is { IsWebPage: true } tab)
        {
            tab.Address = url;
            return;
        }

        OpenTab(url);
    }

    private void ToggleBookmark()
    {
        if (!CanBookmark || SelectedTab is not { } tab || WebLinks.SafeUrl(tab.Address) is not { } url)
        {
            return;
        }

        // Both paths raise Changed when they finish, which refreshes the bar and the star.
        _ = BookmarkStore.Contains(url)
            ? RemoveBookmarkAsync(url)
            : AddBookmarkAsync(url, tab.Title);
    }

    private void RemoveBookmark(string url) => _ = RemoveBookmarkAsync(url);

    /// <summary>Adds a bookmark, and reveals the bar for the very first one so that starring a page
    /// has a visible result. Only the step from no bookmarks to one does this: once there is
    /// something to show, the setting is the user's to decide, including hiding the bar again.</summary>
    private async Task AddBookmarkAsync(string url, string title)
    {
        var wasEmpty = BookmarkStore.Items.Count == 0;
        await BookmarkStore.AddAsync(url, title);

        if (wasEmpty && BookmarkStore.Items.Count > 0 && !SettingsStore.Current.ShowBookmarkBar)
        {
            SetBookmarkBar(true);
        }
    }

    /// <summary>Removes a bookmark, and hides the bar once the last one is gone: the mirror of
    /// revealing it for the first, so an empty bar never takes up room.</summary>
    private async Task RemoveBookmarkAsync(string url)
    {
        var wasLast = BookmarkStore.Items.Count == 1;
        await BookmarkStore.RemoveAsync(url);

        if (wasLast && BookmarkStore.Items.Count == 0 && SettingsStore.Current.ShowBookmarkBar)
        {
            SetBookmarkBar(false);
        }
    }

    private void ToggleBookmarkBar() => SetBookmarkBar(!SettingsStore.Current.ShowBookmarkBar);

    private void SetBookmarkBar(bool visible)
    {
        SettingsStore.Current.ShowBookmarkBar = visible;
        SettingsStore.Save();
        OnPropertyChanged(nameof(IsBookmarkBarVisible));

        // Keep an open settings page showing the value that was just written.
        foreach (var tab in Tabs.OfType<SettingsTabViewModel>())
        {
            tab.Settings.RefreshBookmarkBar();
        }
    }

    /// <summary>Shows a started download and opens the popup, which is how the user learns the
    /// download began now that the engine's own popup is suppressed.</summary>
    private void OnDownloadStarted(object? sender, IDownload download) =>
        Dispatcher.UIThread.Invoke(() =>
        {
            Downloads.Insert(0, new DownloadViewModel(download));

            while (Downloads.Count > MaxDownloadRows &&
                   Downloads.LastOrDefault(row => !row.IsActive) is { } finished)
            {
                Downloads.Remove(finished);
            }

            OnPropertyChanged(nameof(HasDownloads));
            IsDownloadsOpen = true;
        });

    /// <summary>The store completes its work off the UI thread, so the rebuild is marshalled back.</summary>
    private void OnBookmarksChanged(object? sender, EventArgs args) =>
        Dispatcher.UIThread.Invoke(LoadBookmarks);

    private void LoadBookmarks()
    {
        // A change this window already applied, a drag for instance, needs no rebuild: replacing the
        // rows mid animation would drop the entry the pointer just released. Bookmarks compare by
        // value, so a change to a title or an icon still rebuilds.
        if (Bookmarks.Select(row => row.Source).SequenceEqual(BookmarkStore.Items))
        {
            return;
        }

        Bookmarks.Clear();

        foreach (var bookmark in BookmarkStore.Items)
        {
            Bookmarks.Add(new BookmarkViewModel(bookmark, OpenBookmark, OpenTab, RemoveBookmark));
        }

        RefreshBookmarkState();
    }

    private void OnSelectedTabPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(TabViewModel.Address))
        {
            RefreshBookmarkState();
        }
    }

    private void RefreshBookmarkState()
    {
        OnPropertyChanged(nameof(CanBookmark));
        OnPropertyChanged(nameof(IsCurrentPageBookmarked));
    }

    private void Add(TabViewModel tab)
    {
        Tabs.Add(tab);
        HostedTabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>Closes a tab, releasing its WebView2 instance, and closes the window with the last one.</summary>
    private void CloseTab(TabViewModel tab)
    {
        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Tabs.RemoveAt(index);
        HostedTabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            SelectedTab = null;
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
    }
}
