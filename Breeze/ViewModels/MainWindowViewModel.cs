using System.Collections.ObjectModel;
using System.Windows.Input;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase, ITabReorder
{
    private TabViewModel? _selectedTab;
    private bool _reordering;

    public MainWindowViewModel()
    {
        NewTabCommand = new RelayCommand(NewTab);
        SettingsCommand = new RelayCommand(OpenSettings);
        Add(new TabViewModel(CloseTab, SettingsStore.StartupAddress()));
    }

    /// <summary>Raised when the last tab is closed and the window should follow.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Tabs in strip order; dragging reorders this list.</summary>
    public ObservableCollection<TabViewModel> Tabs { get; } = [];

    /// <summary>Same tabs in creation order. Hosting the web views here keeps their
    /// containers, and therefore the WebView2 instances, untouched while tabs are reordered.</summary>
    public ObservableCollection<TabViewModel> HostedTabs { get; } = [];

    public ICommand NewTabCommand { get; }

    public ICommand SettingsCommand { get; }

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
            }

            if (value is not null)
            {
                value.IsSelected = true;
            }
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

    private void NewTab() => Add(new TabViewModel(CloseTab, StartPage.Url));

    /// <summary>Focuses the open settings tab, or opens the single allowed one.</summary>
    private void OpenSettings()
    {
        if (Tabs.OfType<SettingsTabViewModel>().FirstOrDefault() is { } existing)
        {
            SelectedTab = existing;
            return;
        }

        Add(new SettingsTabViewModel(CloseTab));
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
