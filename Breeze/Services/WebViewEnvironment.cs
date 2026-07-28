using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Single WebView2 environment shared by every web view, stored locally only.</summary>
public static class WebViewEnvironment
{
    private static Task<CoreWebView2Environment>? _environment;

    /// <summary>Version of the installed WebView2 runtime, or null when it is absent. Breeze renders
    /// every page through it, including its own start page, so this is checked before a window is
    /// shown rather than left to fail per tab.</summary>
    public static string? RuntimeVersion
    {
        get
        {
            try
            {
                var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
                return string.IsNullOrWhiteSpace(version) ? null : version;
            }
            catch (Exception)
            {
                // WebView2RuntimeNotFoundException, or a runtime too broken to report itself.
                return null;
            }
        }
    }

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
