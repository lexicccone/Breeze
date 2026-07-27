namespace Breeze.Services;

/// <summary>Local-only, size capped error log. Never leaves the machine.</summary>
public static class ErrorLog
{
    private const long MaxBytes = 256 * 1024;

    private static readonly object Gate = new();

    public static void Write(string context, Exception error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.Root);

                if (new FileInfo(AppPaths.ErrorLogFile) is { Exists: true, Length: > MaxBytes })
                {
                    File.Delete(AppPaths.ErrorLogFile);
                }

                File.AppendAllText(
                    AppPaths.ErrorLogFile,
                    $"{DateTime.UtcNow:u} {context}: {error.GetType().Name}: {error.Message}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Logging must never take the application down.
        }
    }
}
