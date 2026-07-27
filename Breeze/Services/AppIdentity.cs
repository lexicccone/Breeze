using System.Runtime.InteropServices;

namespace Breeze.Services;

/// <summary>Gives Breeze an explicit shell identity so Windows groups its child processes,
/// including the WebView2 browser process, under the Breeze entry.</summary>
public static class AppIdentity
{
    private const string AppUserModelId = "Breeze.Browser";

    /// <summary>Public repository, included in the User-Agent so site owners can identify the
    /// requests. Set this once the repository is published.</summary>
    private const string RepositoryUrl = "";

    public static string Version { get; } =
        typeof(AppIdentity).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>Identity Breeze sends on its own HTTP requests. It never imitates another
    /// browser: requests made by Breeze itself are declared as such.</summary>
    public static string UserAgent { get; } = RepositoryUrl.Length == 0
        ? $"Breeze/{Version}"
        : $"Breeze/{Version} (+{RepositoryUrl})";

    public static void Apply() => SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
