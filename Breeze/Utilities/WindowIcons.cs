using System.Runtime.InteropServices;
using Avalonia.Controls;
using Breeze.Services;

namespace Breeze.Utilities;

/// <summary>Hands a window the icon frames from a Windows icon file. Avalonia takes a single
/// bitmap and rescales it for every size, which throws away the simplified small frames the file
/// carries, so Windows is given the file itself and picks the right frame for the title bar, the
/// taskbar and Alt+Tab.</summary>
public static class WindowIcons
{
    private const uint WmSetIcon = 0x0080;
    private const nint IconSmall = 0;
    private const nint IconBig = 1;

    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x0010;

    private const int SmallIconWidth = 49;
    private const int SmallIconHeight = 50;
    private const int LargeIconWidth = 11;
    private const int LargeIconHeight = 12;

    /// <summary>Applies an icon file to a window that already has a native handle. A failure is
    /// logged and leaves the icon Avalonia set in place.</summary>
    public static void Apply(Window window, string file)
    {
        try
        {
            if (window.TryGetPlatformHandle()?.Handle is not { } handle ||
                handle == IntPtr.Zero ||
                !File.Exists(file))
            {
                return;
            }

            Set(handle, file, IconSmall, SmallIconWidth, SmallIconHeight);
            Set(handle, file, IconBig, LargeIconWidth, LargeIconHeight);
        }
        catch (Exception error)
        {
            ErrorLog.Write("window.icon", error);
        }
    }

    private static void Set(IntPtr window, string file, nint which, int widthMetric, int heightMetric)
    {
        var icon = LoadImage(
            IntPtr.Zero,
            file,
            ImageIcon,
            GetSystemMetrics(widthMetric),
            GetSystemMetrics(heightMetric),
            LoadFromFile);

        if (icon != IntPtr.Zero)
        {
            SendMessage(window, WmSetIcon, which, icon);
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr instance, string name, uint type, int width, int height, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr window, uint message, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
