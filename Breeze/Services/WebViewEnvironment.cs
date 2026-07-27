using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Single WebView2 environment shared by every web view, stored locally only.</summary>
public static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    public static Task<CoreWebView2Environment> GetAsync() => _environment ??= CreateAsync();

    private static Task<CoreWebView2Environment> CreateAsync()
    {
        Directory.CreateDirectory(AppPaths.WebViewData);

        var options = new CoreWebView2EnvironmentOptions
        {
            EnableTrackingPrevention = true,
            AreBrowserExtensionsEnabled = false
        };

        return CoreWebView2Environment.CreateAsync(userDataFolder: AppPaths.WebViewData, options: options);
    }
}
