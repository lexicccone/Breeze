using Avalonia.Controls;
using Breeze.ViewModels;

namespace Breeze.Views;

/// <summary>The small dialog behind renaming, adding and moving bookmarks, and behind the question
/// asked before a folder with something in it is deleted. A window rather than an overlay because
/// the engine's native child window draws above anything placed in the page area.</summary>
public sealed partial class BookmarkPromptWindow : Window
{
    public BookmarkPromptWindow() => InitializeComponent();

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        if (DataContext is BookmarkPromptViewModel { HasName: true } && this.FindControl<TextBox>("NameBox") is { } box)
        {
            box.Focus();
            box.SelectAll();
        }
    }
}
