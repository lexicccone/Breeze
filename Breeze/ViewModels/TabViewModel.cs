using System.Windows.Input;
using Avalonia.Media;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>State of a single browser tab; the hosted web view attaches itself as the navigator.</summary>
public class TabViewModel : ViewModelBase, IWebNavigatorHost
{
    private string? _address;
    private string _addressText = string.Empty;
    private string? _documentTitle;
    private IImage? _favicon;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isLoading;
    private bool _isSelected;

    private readonly Action<string>? _openTab;

    public TabViewModel(Action<TabViewModel> close, string? address = null, Action<string>? openTab = null)
    {
        _address = address;
        _openTab = openTab;
        SubmitCommand = new RelayCommand(Submit);
        BackCommand = new RelayCommand(() => Navigator?.GoBack());
        ForwardCommand = new RelayCommand(() => Navigator?.GoForward());
        ReloadCommand = new RelayCommand(() => Navigator?.Reload());
        StopCommand = new RelayCommand(() => Navigator?.Stop());
        CloseCommand = new RelayCommand(() => close(this));

        // Middle click equivalents: act in a new tab and leave this tab where it is.
        BackInNewTabCommand = new RelayCommand(() => OpenInNewTab(Navigator?.PreviousUrl));
        ForwardInNewTabCommand = new RelayCommand(() => OpenInNewTab(Navigator?.NextUrl));
        ReloadInNewTabCommand = new RelayCommand(() => OpenInNewTab(Navigator?.CurrentUrl ?? Address));
    }

    public IWebNavigator? Navigator { get; set; }

    public void RequestTab(string url) => _openTab?.Invoke(url);

    public ICommand SubmitCommand { get; }

    public ICommand BackCommand { get; }

    public ICommand ForwardCommand { get; }

    public ICommand ReloadCommand { get; }

    public ICommand StopCommand { get; }

    public ICommand CloseCommand { get; }

    public ICommand BackInNewTabCommand { get; }

    public ICommand ForwardInNewTabCommand { get; }

    public ICommand ReloadInNewTabCommand { get; }

    /// <summary>Current document URL; also the navigation request when set from the address bar.</summary>
    public string? Address
    {
        get => _address;
        set
        {
            if (SetProperty(ref _address, value))
            {
                AddressText = value == StartPage.Url ? string.Empty : value ?? string.Empty;
            }
        }
    }

    public string AddressText
    {
        get => _addressText;
        set => SetProperty(ref _addressText, value);
    }

    public string? DocumentTitle
    {
        get => _documentTitle;
        set
        {
            if (SetProperty(ref _documentTitle, value))
            {
                OnPropertyChanged(nameof(Title));
            }
        }
    }

    public virtual string Title => string.IsNullOrWhiteSpace(DocumentTitle) ? "New Tab" : DocumentTitle;

    /// <summary>False for tabs backed by a native view instead of a web page.</summary>
    public virtual bool IsWebPage => true;

    /// <summary>Shown when a web page has no favicon of its own.</summary>
    public bool ShowGlobe => IsWebPage && !HasFavicon;

    public IImage? Favicon
    {
        get => _favicon;
        set
        {
            if (SetProperty(ref _favicon, value))
            {
                OnPropertyChanged(nameof(HasFavicon));
                OnPropertyChanged(nameof(ShowGlobe));
            }
        }
    }

    public bool HasFavicon => _favicon is not null;

    public bool CanGoBack
    {
        get => _canGoBack;
        set => SetProperty(ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        set => SetProperty(ref _canGoForward, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>Opens a URL in a new tab. Nothing happens when there is no entry to open, which is
    /// how a middle click on Back or Forward behaves at the ends of the history.</summary>
    private void OpenInNewTab(string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            RequestTab(url);
        }
    }

    private void Submit()
    {
        if (UrlResolver.Resolve(AddressText) is { } target)
        {
            Address = target;
        }
    }
}
