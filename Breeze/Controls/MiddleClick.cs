using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Breeze.Controls;

/// <summary>Runs a command when a control is middle clicked, which buttons do not report on their
/// own. Used by the navigation buttons to open their action in a new tab.</summary>
public static class MiddleClick
{
    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<Control, ICommand?>("Command", typeof(MiddleClick));

    static MiddleClick() =>
        CommandProperty.Changed.AddClassHandler<Control, ICommand?>((control, e) => Track(control, e));

    public static ICommand? GetCommand(Control control) => control.GetValue(CommandProperty);

    public static void SetCommand(Control control, ICommand? value) => control.SetValue(CommandProperty, value);

    private static void Track(Control control, AvaloniaPropertyChangedEventArgs<ICommand?> change)
    {
        control.PointerReleased -= OnPointerReleased;

        if (change.NewValue.GetValueOrDefault() is not null)
        {
            control.PointerReleased += OnPointerReleased;
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control || e.InitialPressMouseButton != MouseButton.Middle)
        {
            return;
        }

        // Only count a release that is still over the control, as a click would.
        var point = e.GetPosition(control);
        if (point.X < 0 || point.Y < 0 || point.X > control.Bounds.Width || point.Y > control.Bounds.Height)
        {
            return;
        }

        if (GetCommand(control) is { } command && command.CanExecute(null))
        {
            command.Execute(null);
            e.Handled = true;
        }
    }
}
