namespace Breeze.Models;

/// <summary>One download, as the download UI sees it. The engine's own download operation is the
/// only implementation today; keeping the UI behind this interface means a later downloads manager
/// can serve entries from elsewhere, a persisted history for instance, without touching the view.
/// Implementations report their own failures and never throw at the UI.</summary>
public interface IDownload
{
    /// <summary>Raised whenever any value below changes, on the thread the source runs on.</summary>
    event EventHandler? Changed;

    string FileName { get; }

    /// <summary>Where the file is, or will be, on disk. Null when that is not known.</summary>
    string? FilePath { get; }

    long BytesReceived { get; }

    /// <summary>Total size, or zero when the server did not say.</summary>
    long TotalBytes { get; }

    DownloadStatus Status { get; }

    /// <summary>Short reason a download failed, or null.</summary>
    string? FailureReason { get; }

    void Cancel();
}
