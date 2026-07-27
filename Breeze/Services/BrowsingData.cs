using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Clears local browsing data through the WebView2 profile shared by all tabs.</summary>
public static class BrowsingData
{
    private static CoreWebView2Profile? _profile;

    public static void Register(CoreWebView2 webView) => _profile = webView.Profile;

    public static Task ClearHistoryAsync() =>
        ClearAsync(CoreWebView2BrowsingDataKinds.BrowsingHistory);

    public static Task ClearCookiesAsync() =>
        ClearAsync(CoreWebView2BrowsingDataKinds.Cookies);

    public static Task ClearCacheAsync() =>
        ClearAsync(CoreWebView2BrowsingDataKinds.DiskCache | CoreWebView2BrowsingDataKinds.CacheStorage);

    private static async Task ClearAsync(CoreWebView2BrowsingDataKinds kinds)
    {
        if (_profile is null)
        {
            return;
        }

        try
        {
            await _profile.ClearBrowsingDataAsync(kinds);
        }
        catch (Exception)
        {
            // The owning web view was closed; nothing to clear through.
        }
    }
}
