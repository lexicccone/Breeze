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

            var model = new MainWindowViewModel();
            var window = new MainWindow { DataContext = model };
            model.CloseRequested += (_, _) => window.Close();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
