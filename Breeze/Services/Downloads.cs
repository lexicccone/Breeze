using Avalonia.Platform.Storage;

namespace Breeze.Services;

/// <summary>Decides where a download lands: inside the configured folder under a safe file name,
/// or wherever the user picks when they have asked to be prompted.</summary>
public static class Downloads
{
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
