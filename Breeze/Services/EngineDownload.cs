using Breeze.Models;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>An engine download operation seen as an <see cref="IDownload" />. Values are cached
/// rather than read on demand, because the operation belongs to a tab's engine and every member
/// throws once that engine is closed.</summary>
public sealed class EngineDownload : IDownload
{
    private readonly CoreWebView2DownloadOperation _operation;

    public EngineDownload(CoreWebView2DownloadOperation operation)
    {
        _operation = operation;
        Read();

        operation.BytesReceivedChanged += (_, _) => Update();
        operation.StateChanged += (_, _) => Update();
    }

    public event EventHandler? Changed;

    public string FileName { get; private set; } = string.Empty;

    public string? FilePath { get; private set; }

    public long BytesReceived { get; private set; }

    public long TotalBytes { get; private set; }

    public DownloadStatus Status { get; private set; } = DownloadStatus.Downloading;

    public string? FailureReason { get; private set; }

    public void Cancel()
    {
        try
        {
            _operation.Cancel();
        }
        catch (Exception error)
        {
            ErrorLog.Write("download.cancel", error);
        }
    }

    private void Update()
    {
        Read();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Read()
    {
        try
        {
            FilePath = _operation.ResultFilePath;
            FileName = Path.GetFileName(FilePath);
            BytesReceived = _operation.BytesReceived;
            TotalBytes = (long)(_operation.TotalBytesToReceive ?? 0);

            Status = _operation.State switch
            {
                CoreWebView2DownloadState.Completed => DownloadStatus.Completed,
                CoreWebView2DownloadState.Interrupted =>
                    _operation.InterruptReason == CoreWebView2DownloadInterruptReason.UserCanceled
                        ? DownloadStatus.Cancelled
                        : DownloadStatus.Failed,
                _ => DownloadStatus.Downloading
            };

            FailureReason = Status == DownloadStatus.Failed ? Describe(_operation.InterruptReason) : null;
        }
        catch (Exception)
        {
            // The owning tab was closed: keep the last values that were read.
        }
    }

    /// <summary>Turns the engine's reason into something readable, without inventing detail.</summary>
    private static string Describe(CoreWebView2DownloadInterruptReason reason) => reason switch
    {
        CoreWebView2DownloadInterruptReason.NetworkTimeout => "The connection timed out",
        CoreWebView2DownloadInterruptReason.NetworkFailed or
        CoreWebView2DownloadInterruptReason.NetworkDisconnected or
        CoreWebView2DownloadInterruptReason.NetworkServerDown or
        CoreWebView2DownloadInterruptReason.NetworkInvalidRequest => "The connection failed",
        CoreWebView2DownloadInterruptReason.ServerFailed or
        CoreWebView2DownloadInterruptReason.ServerNoRange or
        CoreWebView2DownloadInterruptReason.ServerBadContent or
        CoreWebView2DownloadInterruptReason.ServerUnauthorized or
        CoreWebView2DownloadInterruptReason.ServerForbidden or
        CoreWebView2DownloadInterruptReason.ServerCertificateProblem or
        CoreWebView2DownloadInterruptReason.ServerUnexpectedResponse or
        CoreWebView2DownloadInterruptReason.ServerContentLengthMismatch or
        CoreWebView2DownloadInterruptReason.ServerCrossOriginRedirect => "The server refused the download",
        CoreWebView2DownloadInterruptReason.FileNoSpace => "Not enough disk space",
        CoreWebView2DownloadInterruptReason.FileAccessDenied or
        CoreWebView2DownloadInterruptReason.FileNameTooLong or
        CoreWebView2DownloadInterruptReason.FileTooLarge or
        CoreWebView2DownloadInterruptReason.FileTransientError or
        CoreWebView2DownloadInterruptReason.FileTooShort or
        CoreWebView2DownloadInterruptReason.FileHashMismatch or
        CoreWebView2DownloadInterruptReason.FileFailed => "The file could not be written",
        CoreWebView2DownloadInterruptReason.FileBlockedByPolicy or
        CoreWebView2DownloadInterruptReason.FileMalicious or
        CoreWebView2DownloadInterruptReason.FileSecurityCheckFailed => "Blocked for safety",
        CoreWebView2DownloadInterruptReason.DownloadProcessCrashed => "The download process stopped",
        _ => "The download failed"
    };
}
