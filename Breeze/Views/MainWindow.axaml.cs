using Avalonia.Controls;
using Avalonia.Input;
using Breeze.Services;
using Breeze.Utilities;

namespace Breeze.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // The window has a native handle by now, so Windows can be given the icon file and use
        // the frame drawn for each size rather than one rescaled bitmap.
        WindowIcons.Apply(this, Path.Combine(AppContext.BaseDirectory, "Assets", "Brand", "breeze.ico"));
    }

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
