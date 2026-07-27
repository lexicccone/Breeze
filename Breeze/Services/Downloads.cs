namespace Breeze.Services;

/// <summary>Keeps downloads inside the configured folder with a safe file name.</summary>
public static class Downloads
{
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
