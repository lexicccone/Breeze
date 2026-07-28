using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Breeze.Models;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.ViewModels;

/// <summary>One row in the downloads popup. Everything it shows comes from an <see cref="IDownload" />,
/// so the popup knows nothing about the browser engine.</summary>
public sealed class DownloadViewModel : ViewModelBase
{
    /// <summary>Speed is averaged over at least this long, so the figure does not flicker.</summary>
    private const long SpeedWindowMilliseconds = 700;

    private readonly IDownload _download;

    private long _speedBytes;
    private long _speedAt = Environment.TickCount64;
    private double _bytesPerSecond;

    public DownloadViewModel(IDownload download)
    {
        _download = download;
        CancelCommand = new RelayCommand(download.Cancel);
        OpenCommand = new RelayCommand(Open);
        OpenFolderCommand = new RelayCommand(OpenFolder);

        _speedBytes = download.BytesReceived;
        download.Changed += OnChanged;
    }

    public ICommand CancelCommand { get; }

    public ICommand OpenCommand { get; }

    public ICommand OpenFolderCommand { get; }

    public string FileName => _download.FileName;

    public bool IsActive => _download.Status == DownloadStatus.Downloading;

    public bool IsCompleted => _download.Status == DownloadStatus.Completed;

    /// <summary>True for a download that ended without producing a file, so the row can say so.</summary>
    public bool IsUnfinished => _download.Status is DownloadStatus.Cancelled or DownloadStatus.Failed;

    /// <summary>Progress cannot be shown as a fraction when the server gave no size.</summary>
    public bool IsIndeterminate => IsActive && _download.TotalBytes <= 0;

    /// <summary>A finished download reads full even when the server never gave a size.</summary>
    public double Progress => _download.Status switch
    {
        DownloadStatus.Completed => 100,
        DownloadStatus.Downloading when _download.TotalBytes > 0 =>
            Math.Clamp(100.0 * _download.BytesReceived / _download.TotalBytes, 0, 100),
        _ => 0
    };

    public string Status => _download.Status switch
    {
        DownloadStatus.Completed => "Completed",
        DownloadStatus.Cancelled => "Cancelled",
        DownloadStatus.Failed => "Failed",
        _ => "Downloading"
    };

    /// <summary>The line under the file name: size and speed while running, the outcome after.</summary>
    public string Detail => _download.Status switch
    {
        DownloadStatus.Completed => $"Completed · {Size(_download.BytesReceived)}",
        // A cancelled row is labelled as such beside this line, so it stays empty.
        DownloadStatus.Cancelled => string.Empty,
        DownloadStatus.Failed => _download.FailureReason ?? "The download failed",
        _ => Running()
    };

    private string Running()
    {
        var received = Size(_download.BytesReceived);
        var progress = _download.TotalBytes > 0 ? $"{received} of {Size(_download.TotalBytes)}" : received;

        return _bytesPerSecond > 0
            ? $"Downloading · {progress} · {Size((long)_bytesPerSecond)}/s"
            : $"Downloading · {progress}";
    }

    private void OnChanged(object? sender, EventArgs args) => Dispatcher.UIThread.Invoke(Refresh);

    private void Refresh()
    {
        MeasureSpeed();

        OnPropertyChanged(nameof(FileName));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsCompleted));
        OnPropertyChanged(nameof(IsUnfinished));
        OnPropertyChanged(nameof(IsIndeterminate));
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(Detail));
    }

    /// <summary>WebView2 reports bytes, not a rate, so the rate comes from the change between
    /// reports. Samples closer together than the window are ignored rather than averaged.</summary>
    private void MeasureSpeed()
    {
        if (!IsActive)
        {
            _bytesPerSecond = 0;
            return;
        }

        var now = Environment.TickCount64;
        var elapsed = now - _speedAt;

        if (elapsed < SpeedWindowMilliseconds)
        {
            return;
        }

        var delta = _download.BytesReceived - _speedBytes;
        _bytesPerSecond = delta > 0 ? delta * 1000.0 / elapsed : 0;
        _speedBytes = _download.BytesReceived;
        _speedAt = now;
    }

    private void Open()
    {
        if (_download.FilePath is not { } path || !File.Exists(path))
        {
            return;
        }

        try
        {
            // Opening the file is the point of the button, so the shell decides what handles it.
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception error)
        {
            ErrorLog.Write("download.open", error);
        }
    }

    private void OpenFolder()
    {
        if (_download.FilePath is not { } path)
        {
            return;
        }

        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is null || !Directory.Exists(folder))
            {
                return;
            }

            // Explorer wants the selection switch and the path as one argument. A Windows path
            // cannot contain a quote, so quoting it is safe.
            var start = new ProcessStartInfo("explorer.exe")
            {
                UseShellExecute = false,
                Arguments = File.Exists(path) ? $"/select,\"{path}\"" : $"\"{folder}\""
            };

            Process.Start(start)?.Dispose();
        }
        catch (Exception error)
        {
            ErrorLog.Write("download.folder", error);
        }
    }

    private static string Size(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
        >= 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes} B"
    };
}
