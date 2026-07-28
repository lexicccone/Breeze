using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Breeze.Services;
using Breeze.ViewModels;
using Breeze.Views;

namespace Breeze;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Theming.Apply();

            // Pruning deletes any cached icon no registered source claims, so every store that
            // keeps icon references must register before the first save can prune.
            FaviconCache.Track(() => ShortcutStore.ReferencedIcons);
            FaviconCache.Track(() => BookmarkStore.ReferencedIcons);

            var model = new MainWindowViewModel();

            // Bookmarks saved with an icon the chrome cannot draw get a better one, once, in the
            // background. Started after the model exists, so the bar hears the store's change and
            // refreshes itself.
            _ = BookmarkStore.RepairIconsAsync();

            var window = new MainWindow { DataContext = model };
            model.CloseRequested += (_, _) => window.Close();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
