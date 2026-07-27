using Avalonia;
using Avalonia.Styling;
using Breeze.Models;

namespace Breeze.Services;

/// <summary>Applies the stored theme choice to the running application.</summary>
public static class Theming
{
    public static void Apply() => Apply(SettingsStore.Current.Theme);

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = theme switch
            {
                AppTheme.Light => ThemeVariant.Light,
                AppTheme.Dark => ThemeVariant.Dark,
                _ => ThemeVariant.Default
            };
        }
    }
}
