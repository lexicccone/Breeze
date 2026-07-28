using Avalonia.Platform.Storage;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Decides where a download lands: inside the configured folder under a safe file name,
/// or wherever the user picks when they have asked to be prompted.</summary>
public static class Downloads
{
    /// <summary>Where the engine keeps a download while it is still in flight. The engine names a
    /// partial file and starts writing before the destination is known, which it does in its
    /// default download folder, so that folder is pointed at Breeze's own staging area. Nothing
    /// then appears next to the user's files, or anywhere they can see, until a destination is
    /// settled and the engine moves the finished file there.</summary>
    public static string Staging { get; } = Path.Combine(AppPaths.Root, "Incomplete");

    /// <summary>Called as each engine is created. The default download folder lives on the profile
    /// shared by every tab, so this is idempotent.</summary>
    public static void Register(CoreWebView2 webView)
    {
        try
        {
            Directory.CreateDirectory(Staging);
            webView.Profile.DefaultDownloadFolderPath = Staging;
            Sweep();
        }
        catch (Exception error)
        {
            ErrorLog.Write("downloads.register", error);
        }
    }

    /// <summary>Deletes partial files left in the staging folder. A file a download is still
    /// writing is locked, so it survives; anything abandoned by a cancelled download or an earlier
    /// session goes.</summary>
    public static void Sweep()
    {
        try
        {
            if (!Directory.Exists(Staging))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(Staging))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception)
                {
                    // In use by a running download: leave it for the next sweep.
                }
            }
        }
        catch (Exception error)
        {
            ErrorLog.Write("downloads.sweep", error);
        }
    }

    /// <summary>Asks the user where to save a download, offering the sanitized name and the
    /// configured folder. Returns the chosen path, or null when the dialog was cancelled.
    /// The dialog, not the server, decides the destination, so the result is used as given.</summary>
    public static async Task<string?> PromptAsync(IStorageProvider storage, string resolvedPath)
    {
        var folder = Path.GetDirectoryName(resolvedPath);

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save as",
            SuggestedFileName = Path.GetFileName(resolvedPath),
            SuggestedStartLocation = folder is null ? null : await storage.TryGetFolderFromPathAsync(folder),
            DefaultExtension = Path.GetExtension(resolvedPath).TrimStart('.'),
            ShowOverwritePrompt = true
        });

        return file?.TryGetLocalPath();
    }

    /// <summary>Maps the engine's suggested path onto the configured download folder, or returns
    /// null when the result would fall outside it. The suggested name comes from the server, so
    /// it is treated as untrusted.</summary>
    public static string? Resolve(string? suggestedPath)
    {
        try
        {
            var name = Path.GetFileName(suggestedPath ?? string.Empty);
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalid, '_');
            }

            var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(SettingsStore.Current.DownloadFolder));
            var target = Path.GetFullPath(Path.Combine(folder, name));

            if (!target.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            Directory.CreateDirectory(folder);
            return target;
        }
        catch (Exception error)
        {
            ErrorLog.Write("downloads.resolve", error);
            return null;
        }
    }
}
