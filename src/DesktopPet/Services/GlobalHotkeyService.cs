using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DesktopPet.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Action _toggleVisibility;
    private readonly Action _toggleClickThrough;
    private readonly AppLogger _logger;
    private readonly HashSet<int> _registered = [];
    private bool _disposed;
    public string Status { get; private set; } = "快捷键已关闭";

    public GlobalHotkeyService(Action toggleVisibility, Action toggleClickThrough, AppLogger logger)
    {
        _toggleVisibility = toggleVisibility;
        _toggleClickThrough = toggleClickThrough;
        _logger = logger;
        _source = new HwndSource(new HwndSourceParameters("DesktopPet.Hotkeys")
        { ParentWindow = new IntPtr(-3), Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(OnMessage);
    }

    public void SetEnabled(bool enabled)
    {
        if (_disposed) return;
        UnregisterAll();
        if (!enabled) { Status = "快捷键已关闭，可使用系统托盘操作"; return; }
        var unavailable = new List<string>();
        Register(1, 0x50, "Ctrl+Alt+P", unavailable);
        Register(2, 0x4D, "Ctrl+Alt+M", unavailable);
        Status = unavailable.Count == 0 ? "Ctrl+Alt+P 显示/隐藏 · Ctrl+Alt+M 鼠标穿透" :
            string.Join("、", unavailable) + " 注册失败（可能已被占用），请使用系统托盘";
    }

    private void Register(int id, uint key, string label, List<string> unavailable)
    {
        if (RegisterHotKey(_source.Handle, id, 0x4003, key)) _registered.Add(id);
        else
        {
            unavailable.Add(label);
            _logger.Info($"Hotkey {label} registration failed: {Marshal.GetLastPInvokeError()}");
        }
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_disposed && message == 0x0312 && _registered.Contains(wParam.ToInt32()))
        {
            handled = true;
            if (wParam.ToInt32() == 1) _toggleVisibility();
            else _toggleClickThrough();
        }
        return IntPtr.Zero;
    }

    private void UnregisterAll()
    {
        foreach (var id in _registered) UnregisterHotKey(_source.Handle, id);
        _registered.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        UnregisterAll();
        _disposed = true;
        _source.RemoveHook(OnMessage);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
