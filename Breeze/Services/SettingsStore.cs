using System.Text.Json;
using System.Text.Json.Serialization;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>User settings, kept in a readable JSON file in the local Breeze folder.</summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static AppSettings? _current;

    public static AppSettings Current => _current ??= Load();

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Root);
            File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, Options));
        }
        catch (Exception error)
        {
            ErrorLog.Write("settings.save", error);
        }
    }

    /// <summary>Address the first tab opens with.</summary>
    public static string StartupAddress() => Current.StartupPage switch
    {
        StartupPage.Blank => "about:blank",
        StartupPage.Custom => UrlResolver.Resolve(Current.StartupUrl) ?? StartPage.Url,
        _ => StartPage.Url
    };

    private static AppSettings Load()
    {
        try
        {
            return File.Exists(AppPaths.SettingsFile)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }
}
