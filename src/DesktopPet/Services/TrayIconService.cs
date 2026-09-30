using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopPet.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly System.Drawing.Icon _icon;
    private readonly Forms.ToolStripMenuItem _show;
    private readonly Forms.ToolStripMenuItem _hide;
    private readonly Forms.ToolStripMenuItem _pause;
    private bool _disposed;

    public TrayIconService(Dispatcher dispatcher, Action show, Action hide, Action togglePause,
        Action settings, Action exit)
    {
        void Dispatch(Action action)
        {
            if (!_disposed && !dispatcher.HasShutdownStarted)
                dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
        }

        _menu = new Forms.ContextMenuStrip();
        _show = new Forms.ToolStripMenuItem("显示宠物", null, (_, _) => Dispatch(show));
        _hide = new Forms.ToolStripMenuItem("隐藏宠物", null, (_, _) => Dispatch(hide));
        _pause = new Forms.ToolStripMenuItem("暂停自动行为", null, (_, _) => Dispatch(togglePause));
        _menu.Items.AddRange([
            _show, _hide, new Forms.ToolStripSeparator(), _pause,
            new Forms.ToolStripMenuItem("设置…", null, (_, _) => Dispatch(settings)),
            new Forms.ToolStripSeparator(),
            new Forms.ToolStripMenuItem("退出", null, (_, _) => Dispatch(exit))
        ]);
        _icon = (System.Drawing.Icon)System.Drawing.SystemIcons.Application.Clone();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon, Text = "DesktopPet", ContextMenuStrip = _menu, Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatch(show);
    }

    public void UpdateState(bool visible, bool autoBehaviorEnabled)
    {
        if (_disposed) return;
        _show.Enabled = !visible;
        _hide.Enabled = visible;
        _pause.Text = autoBehaviorEnabled ? "暂停自动行为" : "恢复自动行为";
        _notifyIcon.Text = $"DesktopPet · {(visible ? "已显示" : "已隐藏")} · {(autoBehaviorEnabled ? "自动行为" : "已暂停")}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
