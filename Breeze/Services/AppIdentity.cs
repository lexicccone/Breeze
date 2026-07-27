using System.Runtime.InteropServices;

namespace Breeze.Services;

/// <summary>Gives Breeze an explicit shell identity so Windows groups its child processes,
/// including the WebView2 browser process, under the Breeze entry.</summary>
public static class AppIdentity
{
    private const string AppUserModelId = "Breeze.Browser";

    public static void Apply() => SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
