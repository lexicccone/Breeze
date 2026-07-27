using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Breeze.Controls;

/// <summary>Container that moves the window when its empty space is dragged, for the tab strip title bar.</summary>
public sealed class DragArea : Border
{
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (e.Handled || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (e.Source == this && this.GetVisualRoot() is Window window)
        {
            window.BeginMoveDrag(e);
        }
    }
}
