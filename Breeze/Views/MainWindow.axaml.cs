using Avalonia.Controls;
using Avalonia.Input;
using Breeze.Services;

namespace Breeze.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>Runs a shortcut when the chrome has focus. Presses that land in a web page arrive
    /// through the engine instead and are forwarded to the same catalog.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (KeyboardShortcuts.Lookup(e.Key, e.KeyModifiers) is { } action)
        {
            action();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
