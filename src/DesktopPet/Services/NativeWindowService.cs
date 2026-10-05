using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using DesktopPet.Models;

namespace DesktopPet.Services;

internal static class NativeWindowService
{
    public static PetPoint PixelPosition(Window window)
    {
        if (!GetWindowRect(new WindowInteropHelper(window).EnsureHandle(), out var rect))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        return new PetPoint(rect.Left, rect.Top);
    }

    public static void Place(Window window, PetPoint point, double scale)
    {
        if (!SetWindowPos(new WindowInteropHelper(window).EnsureHandle(), IntPtr.Zero,
            (int)Math.Round(point.X), (int)Math.Round(point.Y),
            PixelExtent(window.Width, scale), PixelExtent(window.Height, scale), 0x0014))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
    }

    // Multiplying an exact pixel-aligned DIP size can land a few ulps above
    // its integer (29 / 1.75 * 1.75). Do not grow that window by another pixel.
    private static int PixelExtent(double size, double scale) =>
        Math.Max(1, (int)Math.Ceiling(size * scale - 1e-9));

    public static double MonitorScale(Point point)
    {
        var monitor = MonitorFromPoint(new NativePoint { X = (int)point.X, Y = (int)point.Y }, 2);
        return GetDpiForMonitor(monitor, 0, out var x, out _) == 0 && x > 0 ? x / 96d : 1d;
    }

    public static void HideFromAltTab(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var style = GetWindowLongPtr(handle, -20).ToInt64();
        SetWindowLongPtr(handle, -20, new IntPtr((style | 0x80) & ~0x40000));
    }

    public static void SetClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).EnsureHandle();
        var style = GetWindowLongPtr(handle, -20).ToInt64();
        // WS_EX_TRANSPARENT passes mouse events through a layered WPF window.
        style = enabled ? style | 0x20 : style & ~0x20L;
        Marshal.SetLastPInvokeError(0);
        if (SetWindowLongPtr(handle, -20, new IntPtr((style | 0x08000080) & ~0x40000L)) == IntPtr.Zero &&
            Marshal.GetLastPInvokeError() != 0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
    }

    public static bool TryGetCursorPosition(out Point point)
    {
        var success = GetCursorPos(out var native);
        point = new Point(native.X, native.Y);
        return success;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
