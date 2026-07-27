using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>The start page bundled with Breeze, plus the cached favicons it shows,
/// both served from disk through virtual hosts.</summary>
public static class StartPage
{
    private const string HostName = "breeze.start";
    private const string IconHostName = "breeze.icons";
    private const string Prefix = $"https://{HostName}/";

    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Assets", "StartPage");

    public const string Url = $"{Prefix}index.html";

    public static bool IsStartPage(string? source) =>
        source is not null && source.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static void Register(CoreWebView2 webView)
    {
        webView.SetVirtualHostNameToFolderMapping(HostName, Folder, CoreWebView2HostResourceAccessKind.Deny);

        // DenyCors lets the start page load cached icons as images while still blocking
        // scripted cross origin reads of the cache folder.
        Directory.CreateDirectory(AppPaths.Favicons);
        webView.SetVirtualHostNameToFolderMapping(IconHostName, AppPaths.Favicons, CoreWebView2HostResourceAccessKind.DenyCors);
    }
}
