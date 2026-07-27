using System.Drawing;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Breeze.Models;
using Breeze.Services;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Controls;

/// <summary>Hosts a WebView2 instance inside the Avalonia visual tree and publishes its navigation state.</summary>
public sealed class WebView : NativeControlHost, IWebNavigator
{
    private const int MaxHistoryEntries = 100;

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<WebView, string?>(nameof(Source), defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<WebView, string?> DocumentTitleProperty =
        AvaloniaProperty.RegisterDirect<WebView, string?>(
            nameof(DocumentTitle), o => o.DocumentTitle, (o, v) => o.DocumentTitle = v, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<WebView, IImage?> FaviconProperty =
        AvaloniaProperty.RegisterDirect<WebView, IImage?>(
            nameof(Favicon), o => o.Favicon, (o, v) => o.Favicon = v, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<WebView, bool> CanGoBackProperty =
        AvaloniaProperty.RegisterDirect<WebView, bool>(
            nameof(CanGoBack), o => o.CanGoBack, (o, v) => o.CanGoBack = v, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<WebView, bool> CanGoForwardProperty =
        AvaloniaProperty.RegisterDirect<WebView, bool>(
            nameof(CanGoForward), o => o.CanGoForward, (o, v) => o.CanGoForward = v, defaultBindingMode: BindingMode.TwoWay);

    public static readonly DirectProperty<WebView, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<WebView, bool>(
            nameof(IsLoading), o => o.IsLoading, (o, v) => o.IsLoading = v, defaultBindingMode: BindingMode.TwoWay);

    private CoreWebView2Controller? _controller;
    private bool _detached;
    private bool _syncingSource;
    private bool _painted;
    private bool _internalRequested;
    private Bitmap? _ownedFavicon;

    /// <summary>WebView2 exposes no navigation history list, so the visited entries are mirrored
    /// here. Only used to answer "what is one step back or forward", never to navigate.</summary>
    private readonly List<string> _entries = [];
    private int _position = -1;
    private string? _documentTitle;
    private IImage? _favicon;
    private bool _canGoBack;
    private bool _canGoForward;
    private bool _isLoading;

    public WebView() => ActualThemeVariantChanged += (_, _) => ApplyTheme();

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? DocumentTitle
    {
        get => _documentTitle;
        set => SetAndRaise(DocumentTitleProperty, ref _documentTitle, value);
    }

    public IImage? Favicon
    {
        get => _favicon;
        set => SetAndRaise(FaviconProperty, ref _favicon, value);
    }

    public bool CanGoBack
    {
        get => _canGoBack;
        set => SetAndRaise(CanGoBackProperty, ref _canGoBack, value);
    }

    public bool CanGoForward
    {
        get => _canGoForward;
        set => SetAndRaise(CanGoForwardProperty, ref _canGoForward, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetAndRaise(IsLoadingProperty, ref _isLoading, value);
    }

    public string? CurrentUrl => _position >= 0 ? _entries[_position] : null;

    public string? PreviousUrl => _position > 0 ? _entries[_position - 1] : null;

    public string? NextUrl => _position >= 0 && _position + 1 < _entries.Count ? _entries[_position + 1] : null;

    public void GoBack()
    {
        if (_controller?.CoreWebView2 is { CanGoBack: true } webView)
        {
            // History entries were vetted when they were first navigated to.
            _internalRequested = true;
            webView.GoBack();
        }
    }

    public void GoForward()
    {
        if (_controller?.CoreWebView2 is { CanGoForward: true } webView)
        {
            _internalRequested = true;
            webView.GoForward();
        }
    }

    public void Reload()
    {
        if (_controller?.CoreWebView2 is { } webView)
        {
            _internalRequested = StartPage.IsInternal(webView.Source);
            webView.Reload();
        }
    }

    public void Stop() => _controller?.CoreWebView2.Stop();

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var handle = base.CreateNativeControlCore(parent);
        _detached = false;
        _ = AttachAsync(handle.Handle);
        return handle;
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        _detached = true;
        _controller?.Close();
        _controller = null;

        // Drop the binding before releasing the bitmap so nothing can render a disposed image.
        var icon = _ownedFavicon;
        _ownedFavicon = null;
        Favicon = null;

        if (icon is not null)
        {
            Dispatcher.UIThread.Post(icon.Dispose, DispatcherPriority.Background);
        }

        base.DestroyNativeControlCore(control);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is IWebNavigatorHost host)
        {
            host.Navigator = this;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            if (!_syncingSource)
            {
                Navigate(Source);
            }
        }
        else if (change.Property == BoundsProperty)
        {
            UpdateBounds();
        }
        else if (change.Property == IsVisibleProperty)
        {
            UpdateVisibility();
        }
    }

    private async Task AttachAsync(IntPtr parent)
    {
        var environment = await WebViewEnvironment.GetAsync();
        var controller = await environment.CreateCoreWebView2ControllerAsync(parent);

        if (_detached)
        {
            controller.Close();
            return;
        }

        _controller = controller;
        Configure(controller.CoreWebView2);

        // Paint the engine's empty document in the page colour and keep the native window
        // hidden until the first document is in, so a new tab never flashes white.
        ApplyTheme();
        UpdateBounds();
        UpdateVisibility();
        Navigate(Source);
    }

    /// <summary>Keeps this view's empty page colour in step with the Breeze theme. The colour
    /// scheme sites observe is profile wide and is applied by <see cref="Theming" />.</summary>
    private void ApplyTheme()
    {
        if (_controller is null)
        {
            return;
        }

        _controller.DefaultBackgroundColor = ActualThemeVariant == ThemeVariant.Dark
            ? System.Drawing.Color.FromArgb(0x17, 0x17, 0x1A)
            : System.Drawing.Color.FromArgb(0xFB, 0xFB, 0xFD);
    }

    private void UpdateVisibility()
    {
        if (_controller is not null)
        {
            _controller.IsVisible = _painted && IsVisible;
        }
    }

    private void Configure(CoreWebView2 webView)
    {
        var settings = webView.Settings;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsSwipeNavigationEnabled = false;

        // Reputation checking sends visited URLs to a Microsoft service. Breeze promises no
        // remote endpoint ever sees the user's browsing, so it is off.
        settings.IsReputationCheckingRequired = false;

        webView.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;

        StartPage.Register(webView);
        StartPageBridge.Attach(webView);
        BrowsingData.Register(webView);
        Theming.Register(webView);

        webView.DOMContentLoaded += (_, _) =>
        {
            _painted = true;
            UpdateVisibility();
        };

        // Privacy first: camera, microphone, location, notifications and the rest are refused
        // rather than falling back to the engine's own prompt.
        webView.PermissionRequested += (_, e) =>
        {
            e.State = CoreWebView2PermissionState.Deny;
            e.Handled = true;
        };

        // Popups become tabs, so no page can show a window without Breeze's address bar.
        webView.NewWindowRequested += (_, e) =>
        {
            e.Handled = true;

            if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var target) &&
                target.Scheme is "http" or "https" &&
                !StartPage.IsInternal(e.Uri) &&
                DataContext is IWebNavigatorHost host)
            {
                host.RequestTab(target.AbsoluteUri);
            }
        };

        // Downloads land in the configured folder under a sanitized name, never wherever the
        // server's suggested path points.
        webView.DownloadStarting += (_, e) =>
        {
            if (Downloads.Resolve(e.ResultFilePath) is { } path)
            {
                e.ResultFilePath = path;
            }
            else
            {
                e.Cancel = true;
            }
        };

        webView.ProcessFailed += (_, e) => ErrorLog.Write("engine", new InvalidOperationException(e.ProcessFailedKind.ToString()));

        webView.SourceChanged += (_, _) => PublishSource(webView.Source);
        webView.DocumentTitleChanged += (_, _) => DocumentTitle = webView.DocumentTitle;
        webView.FaviconChanged += async (_, _) => await UpdateFaviconAsync(webView);

        webView.NavigationStarting += (_, e) =>
        {
            var wasRequested = _internalRequested;
            _internalRequested = false;

            // Only Breeze may put its own origins on screen; a remote page must not be able to
            // navigate a tab to the privileged start page.
            if (StartPage.IsInternal(e.Uri) && !wasRequested)
            {
                e.Cancel = true;
                return;
            }

            // Host messaging is the bridge's transport, so keep it off everywhere else.
            webView.Settings.IsWebMessageEnabled = StartPage.IsStartPage(e.Uri);
            IsLoading = true;
        };

        webView.NavigationCompleted += (_, _) =>
        {
            IsLoading = false;
            UpdateHistory(webView);
        };
        webView.HistoryChanged += (_, _) => UpdateHistory(webView);
    }

    private void PublishSource(string source)
    {
        TrackHistory(source);

        _syncingSource = true;
        Source = source;
        _syncingSource = false;
    }

    /// <summary>Mirrors the engine's history: a document URL matching the neighbouring entry means
    /// the user moved through history, anything else is a new entry that truncates what was ahead.</summary>
    private void TrackHistory(string url)
    {
        if (string.IsNullOrEmpty(url) || url == CurrentUrl)
        {
            return;
        }

        if (_position > 0 && _entries[_position - 1] == url)
        {
            _position--;
            return;
        }

        if (_position + 1 < _entries.Count && _entries[_position + 1] == url)
        {
            _position++;
            return;
        }

        if (_position + 1 < _entries.Count)
        {
            _entries.RemoveRange(_position + 1, _entries.Count - _position - 1);
        }

        _entries.Add(url);

        if (_entries.Count > MaxHistoryEntries)
        {
            _entries.RemoveAt(0);
        }

        _position = _entries.Count - 1;
    }

    private void UpdateHistory(CoreWebView2 webView)
    {
        CanGoBack = webView.CanGoBack;
        CanGoForward = webView.CanGoForward;
    }

    private async Task UpdateFaviconAsync(CoreWebView2 webView)
    {
        if (string.IsNullOrEmpty(webView.FaviconUri))
        {
            ShowFavicon(null);
            return;
        }

        try
        {
            await using var icon = await webView.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            ShowFavicon(new Bitmap(icon));
        }
        catch (Exception)
        {
            ShowFavicon(null);
        }
    }

    /// <summary>Publishes a favicon and releases the one it replaces. Disposal is deferred by one
    /// dispatcher turn so the bindings have already moved to the new bitmap.</summary>
    private void ShowFavicon(Bitmap? icon)
    {
        var previous = _ownedFavicon;
        _ownedFavicon = icon;
        Favicon = icon;

        if (previous is not null)
        {
            Dispatcher.UIThread.Post(previous.Dispose, DispatcherPriority.Background);
        }
    }

    private void Navigate(string? source)
    {
        if (_controller is null || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            _internalRequested = StartPage.IsInternal(source);
            _controller.CoreWebView2.Navigate(source);
        }
        catch (Exception error)
        {
            // The engine rejects malformed URIs; a bad address must not end the process.
            ErrorLog.Write("navigate", error);
        }
    }

    private void UpdateBounds()
    {
        if (_controller is null)
        {
            return;
        }

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var width = (int)Math.Round(Bounds.Width * scaling);
        var height = (int)Math.Round(Bounds.Height * scaling);

        _controller.Bounds = new Rectangle(0, 0, Math.Max(width, 0), Math.Max(height, 0));
    }
}
