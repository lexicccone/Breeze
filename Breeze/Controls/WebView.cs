using System.Drawing;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Breeze.Models;
using Breeze.Services;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Controls;

/// <summary>Hosts a WebView2 instance inside the Avalonia visual tree and publishes its navigation state.</summary>
public sealed class WebView : NativeControlHost, IWebNavigator
{
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

    /// <summary>Keeps the engine's empty page colour and the color scheme sites see in step
    /// with the Breeze theme, so pages that support dark mode follow along.</summary>
    private void ApplyTheme()
    {
        if (_controller is null)
        {
            return;
        }

        var dark = ActualThemeVariant == ThemeVariant.Dark;

        _controller.DefaultBackgroundColor = dark
            ? System.Drawing.Color.FromArgb(0x17, 0x17, 0x1A)
            : System.Drawing.Color.FromArgb(0xFB, 0xFB, 0xFD);

        _controller.CoreWebView2.Profile.PreferredColorScheme = dark
            ? CoreWebView2PreferredColorScheme.Dark
            : CoreWebView2PreferredColorScheme.Light;
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

        webView.Profile.PreferredTrackingPreventionLevel = CoreWebView2TrackingPreventionLevel.Strict;

        StartPage.Register(webView);
        StartPageBridge.Attach(webView);
        BrowsingData.Register(webView);

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
        _syncingSource = true;
        Source = source;
        _syncingSource = false;
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
            Favicon = null;
            return;
        }

        try
        {
            await using var icon = await webView.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png);
            Favicon = new Bitmap(icon);
        }
        catch (Exception)
        {
            Favicon = null;
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
