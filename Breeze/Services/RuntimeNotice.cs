using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Breeze.Services;

/// <summary>Tells the user when Breeze cannot run, and why. Used before any window exists, which is
/// why it is a native dialog rather than an Avalonia one: at that point there is nothing to host a
/// window, and a browser with no engine has nothing useful to show anyway.</summary>
public static class RuntimeNotice
{
    private const string DownloadUrl = "https://developer.microsoft.com/microsoft-edge/webview2/";

    private const uint YesNo = 0x00000004;
    private const uint IconWarning = 0x00000030;
    private const uint TopMost = 0x00040000;
    private const int Yes = 6;

    /// <summary>Explains that the WebView2 runtime is missing and offers to open its download page.</summary>
    public static void ShowMissingRuntime()
    {
        const string message =
            "Breeze needs the Microsoft Edge WebView2 Runtime to display web pages, and it is not " +
            "installed on this computer.\n\n" +
            "Breeze cannot start without it. Would you like to open the download page now?";

        try
        {
            if (MessageBoxW(IntPtr.Zero, message, "Breeze", YesNo | IconWarning | TopMost) == Yes)
            {
                Process.Start(new ProcessStartInfo(DownloadUrl) { UseShellExecute = true })?.Dispose();
            }
        }
        catch (Exception error)
        {
            ErrorLog.Write("runtime.notice", error);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
