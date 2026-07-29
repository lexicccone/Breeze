using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Breeze.Models;

namespace Breeze.Controls;

/// <summary>The button on the keyboard shortcuts page that shows a gesture and records a new one.
/// While it is recording it swallows every key press, so the combination being chosen is captured
/// rather than performed: the window resolves shortcuts on the way up from the focused element, and
/// a handled press never reaches it.</summary>
public sealed class ShortcutRecorder : Button
{
    /// <summary>Bound to the row. Recording can be started from here or from the Edit button, and
    /// either way the control takes focus so the keys arrive.</summary>
    public static readonly StyledProperty<bool> IsRecordingProperty =
        AvaloniaProperty.Register<ShortcutRecorder, bool>(nameof(IsRecording));

    public bool IsRecording
    {
        get => GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    private IShortcutRecorder? Row => DataContext as IShortcutRecorder;

    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnClick() => Row?.Begin();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsRecordingProperty && change.GetNewValue<bool>())
        {
            Focus();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Row is not { IsRecording: true } row)
        {
            base.OnKeyDown(e);
            return;
        }

        // Handled first and unconditionally: while recording, no press is allowed to reach the
        // shortcut dispatcher, not even the one being replaced.
        e.Handled = true;

        switch (e.Key)
        {
            case Key.Escape:
                row.Cancel();
                return;

            case Key.Back or Key.Delete:
                row.Record(null);
                return;
        }

        // A modifier on its own is the user part way through a combination, so recording waits.
        if (!IsModifier(e.Key))
        {
            row.Record(new KeyGesture(e.Key, e.KeyModifiers).ToString());
        }
    }

    /// <summary>Recording ends when the row loses focus, so a half finished edit is never left
    /// waiting on a page the user has moved away from.</summary>
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);
        Row?.Cancel();
    }

    private static bool IsModifier(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or
        Key.LeftShift or Key.RightShift or
        Key.LeftAlt or Key.RightAlt or
        Key.LWin or Key.RWin or
        Key.System or Key.None;
}
