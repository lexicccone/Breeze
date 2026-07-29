using Avalonia.Controls;
using Avalonia.Input;
using Breeze.Services;
using Breeze.Utilities;
using Breeze.ViewModels;

namespace Breeze.Views;

public sealed partial class MainWindow : Window
{
    private BookmarksViewModel? _bookmarks;

    public MainWindow() => InitializeComponent();

    /// <summary>Shows the dialogs the bookmark actions ask for. The view models describe what they
    /// need and never create a window themselves.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_bookmarks is not null)
        {
            _bookmarks.PromptRequested -= OnPromptRequested;
        }

        _bookmarks = (DataContext as MainWindowViewModel)?.Bookmarks;

        if (_bookmarks is not null)
        {
            _bookmarks.PromptRequested += OnPromptRequested;
        }
    }

    private void OnPromptRequested(object? sender, BookmarkPromptViewModel prompt)
    {
        var dialog = new BookmarkPromptWindow { DataContext = prompt };
        prompt.CloseRequested += (_, _) => dialog.Close();
        _ = dialog.ShowDialog(this);
    }

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
