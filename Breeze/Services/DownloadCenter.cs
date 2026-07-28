using Breeze.Models;

namespace Breeze.Services;

/// <summary>Where downloads are announced. Tabs report a started download here and the window
/// picks it up, so nothing in the chrome needs a reference to a web view, and a later downloads
/// manager has one place to attach a persisted list to.</summary>
public static class DownloadCenter
{
    /// <summary>Raised when a download has been accepted and is under way.</summary>
    public static event EventHandler<IDownload>? Started;

    public static void Track(IDownload download) => Started?.Invoke(null, download);
}
