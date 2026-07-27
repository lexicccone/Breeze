using Avalonia;
using Avalonia.Styling;
using Breeze.Models;
using Microsoft.Web.WebView2.Core;

namespace Breeze.Services;

/// <summary>Applies the stored theme choice to the running application and to the browser engine.
/// The engine's preferred colour scheme lives on the profile shared by every tab, so it is written
/// from here alone: one writer per theme change, driven by the application's actual variant, which
/// also covers the operating system changing while the theme is set to System.</summary>
public static class Theming
{
    private static CoreWebView2Profile? _profile;
    private static bool _watching;

    public static bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public static void Apply() => Apply(SettingsStore.Current.Theme);

    public static void Apply(AppTheme theme)
    {
        if (Application.Current is { } application)
        {
            Watch(application);
            application.RequestedThemeVariant = Variant(theme);
        }

        ApplyToEngine();
    }

    /// <summary>Called as each web view is created. The profile is shared, so this both records it
    /// and brings a newly created view in line with the current theme.</summary>
    public static void Register(CoreWebView2 webView)
    {
        _profile = webView.Profile;
        ApplyToEngine();
    }

    private static void Watch(Application application)
    {
        if (_watching)
        {
            return;
        }

        _watching = true;
        application.ActualThemeVariantChanged += (_, _) => ApplyToEngine();
    }

    private static void ApplyToEngine()
    {
        if (_profile is null)
        {
            return;
        }

        try
        {
            _profile.PreferredColorScheme = IsDark
                ? CoreWebView2PreferredColorScheme.Dark
                : CoreWebView2PreferredColorScheme.Light;
        }
        catch (Exception error)
        {
            ErrorLog.Write("theme.engine", error);
        }
    }

    private static ThemeVariant Variant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.Dark => ThemeVariant.Dark,
        _ => ThemeVariant.Default
    };
}
