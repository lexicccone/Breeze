namespace Breeze.Models;

/// <summary>Where a download stands, reduced to the states Breeze shows.</summary>
public enum DownloadStatus
{
    Downloading,
    Completed,
    Cancelled,
    Failed
}
