using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>The start page bundled with Breeze, plus the cached favicons it shows,
/// both served from disk through virtual hosts.</summary>
public static class StartPage
{
    private const string HostName = "breeze.start";
    private const string IconHostName = "breeze.icons";
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Assets", "StartPage");

    public const string Url = $"https://{HostName}/index.html";

    /// <summary>True when the URL is the bundled start page. Compares the parsed origin rather
    /// than a string prefix: this is the only origin the host bridge trusts.</summary>
    public static bool IsStartPage(string? source) => HasHost(source, HostName);

    /// <summary>True for any origin Breeze serves from disk.</summary>
    public static bool IsInternal(string? source) =>
        HasHost(source, HostName) || HasHost(source, IconHostName);

    private static bool HasHost(string? source, string host) =>
        Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsDefaultPort &&
        string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase);

    public static void Register(CoreWebView2 webView)
    {
        webView.SetVirtualHostNameToFolderMapping(HostName, Folder, CoreWebView2HostResourceAccessKind.Deny);

        // DenyCors lets the start page load cached icons as images while still blocking
        // scripted cross origin reads of the cache folder.
        Directory.CreateDirectory(AppPaths.Favicons);
        webView.SetVirtualHostNameToFolderMapping(IconHostName, AppPaths.Favicons, CoreWebView2HostResourceAccessKind.DenyCors);
    }
}
