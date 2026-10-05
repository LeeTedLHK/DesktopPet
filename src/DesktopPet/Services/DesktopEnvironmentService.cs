using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using DesktopPet.Behaviors;
using Forms = System.Windows.Forms;

namespace DesktopPet.Services;

public sealed class DesktopEnvironmentService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private bool _lastFullscreen;
    private bool _disposed;
    private readonly Func<string?>? _monitor;
    public bool IsFullscreen => _lastFullscreen;
    public event Action<bool>? FullscreenChanged;

    public DesktopEnvironmentService(Dispatcher dispatcher, Func<string?>? monitor = null)
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _monitor = monitor;
        _lastFullscreen = IsForegroundFullscreen(_monitor?.Invoke());
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var current = IsForegroundFullscreen(_monitor?.Invoke());
        if (current == _lastFullscreen) return;
        _lastFullscreen = current;
        FullscreenChanged?.Invoke(current);
    }

    public static bool IsForegroundFullscreen(string? monitorId = null)
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero || handle == GetShellWindow() || IsIconic(handle)) return false;
        if (monitorId is not null && Forms.Screen.FromHandle(handle).DeviceName != monitorId) return false;
        GetWindowThreadProcessId(handle, out var processId);
        if (processId == (uint)Environment.ProcessId) return false;
        var className = new StringBuilder(256);
        GetClassName(handle, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd")
            return false;
        if (!GetWindowRect(handle, out var rect)) return false;
        // Ignore invisible resize borders when testing ordinary maximized windows.
        if (DwmGetWindowAttribute(handle, 9, out var frame, Marshal.SizeOf<NativeRect>()) == 0) rect = frame;
        var monitor = Forms.Screen.FromHandle(handle).Bounds;
        return CompanionPolicy.IsFullscreen(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            new(monitor.Left, monitor.Top, monitor.Width, monitor.Height));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        FullscreenChanged = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int count);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out NativeRect value, int size);
}
