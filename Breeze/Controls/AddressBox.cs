using Avalonia.Controls;
using Avalonia.Input;

namespace Breeze.Controls;

/// <summary>Address bar text box that selects its whole content when it gains focus.</summary>
public sealed class AddressBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);

    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);
        SelectAll();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!IsFocused)
        {
            Focus(NavigationMethod.Pointer);
            e.Handled = true;
            return;
        }

        base.OnPointerPressed(e);
    }
}
