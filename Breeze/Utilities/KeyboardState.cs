using System.Runtime.InteropServices;
using Avalonia.Input;

namespace Breeze.Utilities;

/// <summary>Win32 keyboard facts needed for key presses that arrive from the web engine: its event
/// reports a virtual key code and no modifier flags, so both have to be translated by hand.</summary>
public static class KeyboardState
{
    private const int VirtualShift = 0x10;
    private const int VirtualControl = 0x11;
    private const int VirtualAlt = 0x12;

    private const int PressedMask = 0x8000;

    /// <summary>Modifier keys held down right now.</summary>
    public static KeyModifiers Modifiers
    {
        get
        {
            var modifiers = KeyModifiers.None;

            if (IsDown(VirtualControl))
            {
                modifiers |= KeyModifiers.Control;
            }

            if (IsDown(VirtualShift))
            {
                modifiers |= KeyModifiers.Shift;
            }

            if (IsDown(VirtualAlt))
            {
                modifiers |= KeyModifiers.Alt;
            }

            return modifiers;
        }
    }

    /// <summary>Translates a virtual key code into the key Avalonia gestures are written in, or
    /// null for keys no shortcut can use. Letters, digits and function keys are contiguous in both
    /// numberings, so they are mapped by offset.</summary>
    public static Key? ToKey(uint virtualKey) => virtualKey switch
    {
        >= 0x30 and <= 0x39 => Key.D0 + (int)(virtualKey - 0x30),
        >= 0x41 and <= 0x5A => Key.A + (int)(virtualKey - 0x41),
        >= 0x70 and <= 0x87 => Key.F1 + (int)(virtualKey - 0x70),
        0x08 => Key.Back,
        0x09 => Key.Tab,
        0x0D => Key.Enter,
        0x1B => Key.Escape,
        0x20 => Key.Space,
        0x21 => Key.PageUp,
        0x22 => Key.PageDown,
        0x23 => Key.End,
        0x24 => Key.Home,
        0x25 => Key.Left,
        0x26 => Key.Up,
        0x27 => Key.Right,
        0x28 => Key.Down,
        0x2D => Key.Insert,
        0x2E => Key.Delete,
        0xBB => Key.OemPlus,
        0xBD => Key.OemMinus,
        _ => null
    };

    private static bool IsDown(int virtualKey) => (GetKeyState(virtualKey) & PressedMask) != 0;

    // DllImport rather than LibraryImport: the source generated form would require the project to
    // allow unsafe code, and this signature needs no marshalling.
    [DllImport("user32.dll")]
    private static extern short GetKeyState(int virtualKey);
}
