using System.Windows.Threading;
using Forms = System.Windows.Forms;
using DesktopPet.Companions;
using System.Globalization;

namespace DesktopPet.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly System.Drawing.Icon _icon;
    private readonly Forms.ToolStripMenuItem _show;
    private readonly Forms.ToolStripMenuItem _hide;
    private readonly Forms.ToolStripMenuItem _pause;
    private readonly Forms.ToolStripMenuItem _clickThrough;
    private readonly Forms.ToolStripMenuItem _petHead;
    private readonly Forms.ToolStripMenuItem _feed;
    private readonly Forms.ToolStripMenuItem _focusStart;
    private readonly Forms.ToolStripMenuItem _focusPause;
    private readonly Forms.ToolStripMenuItem _focusStop;
    private readonly Forms.ToolStripMenuItem _focusStatus;
    private readonly Forms.ToolStripMenuItem _follow;
    private readonly Forms.ToolStripMenuItem _challenge;
    private readonly Forms.ToolStripMenuItem _stopPlay;
    private readonly Forms.ToolStripMenuItem _ball;
    private readonly Forms.ToolStripMenuItem _frisbee;
    private readonly Forms.ToolStripMenuItem _home;
    private bool _disposed;
    private string _petName = "团团";
    private string _visibilityStatus = "已隐藏";
    private string _interactionStatus = "可互动";
    private string _focusTooltip = "";

    public TrayIconService(Dispatcher dispatcher, Action show, Action hide, Action togglePause,
        Action settings, Action exit, Action toggleClickThrough, Action petHead, Action feed,
        Action showCompanion, Action focusStart, Action focusPause, Action focusStop, Action<bool> menuStateChanged,
        Action? resetPosition = null, Action? ball = null, Action? frisbee = null, Action? home = null,
        Action? follow = null, Action? challenge = null, Action? stopPlay = null, Action? photo = null,
        Action? summon = null)
    {
        void Dispatch(Action action)
        {
            if (!_disposed && !dispatcher.HasShutdownStarted)
                dispatcher.BeginInvoke(new Action(() => { if (!_disposed) action(); }));
        }

        _menu = new Forms.ContextMenuStrip();
        _menu.Opened += (_, _) => Dispatch(() => menuStateChanged(true));
        _menu.Closed += (_, _) => Dispatch(() => menuStateChanged(false));
        _show = new Forms.ToolStripMenuItem("显示宠物", null, (_, _) => Dispatch(show));
        _hide = new Forms.ToolStripMenuItem("隐藏宠物", null, (_, _) => Dispatch(hide));
        _pause = new Forms.ToolStripMenuItem("暂停自动行为", null, (_, _) => Dispatch(togglePause));
        _clickThrough = new Forms.ToolStripMenuItem("鼠标穿透（Ctrl+Alt+M）", null, (_, _) => Dispatch(toggleClickThrough));
        _petHead = new Forms.ToolStripMenuItem("摸摸头", null, (_, _) => Dispatch(petHead));
        _feed = new Forms.ToolStripMenuItem("喂食", null, (_, _) => Dispatch(feed));
        _focusStart = new Forms.ToolStripMenuItem("开始一轮专注", null, (_, _) => Dispatch(focusStart));
        _focusPause = new Forms.ToolStripMenuItem("暂停", null, (_, _) => Dispatch(focusPause));
        _focusStop = new Forms.ToolStripMenuItem("停止番茄钟", null, (_, _) => Dispatch(focusStop));
        _focusStatus = new Forms.ToolStripMenuItem("番茄钟未开始") { Enabled = false };
        _follow = new Forms.ToolStripMenuItem("跟我散步（30秒）", null, (_, _) => Dispatch(follow ?? (() => {})));
        _challenge = new Forms.ToolStripMenuItem("三回合接球挑战", null, (_, _) => Dispatch(challenge ?? (() => {})));
        _stopPlay = new Forms.ToolStripMenuItem("结束当前玩法", null, (_, _) => Dispatch(stopPlay ?? (() => {})));
        _ball = new Forms.ToolStripMenuItem("抛小球", null, (_, _) => Dispatch(ball ?? (() => { })));
        _frisbee = new Forms.ToolStripMenuItem("抛飞盘", null, (_, _) => Dispatch(frisbee ?? (() => { })));
        _home = new Forms.ToolStripMenuItem("回小窝睡觉", null, (_, _) => Dispatch(home ?? (() => { })));
        var focusMenu = new Forms.ToolStripMenuItem("番茄钟");
        focusMenu.DropDownItems.AddRange([_focusStatus, _focusStart, _focusPause, _focusStop]);
        _menu.Items.AddRange([
            _show, _hide, new Forms.ToolStripSeparator(), _petHead, _feed,
            new Forms.ToolStripMenuItem("找回宠物 / 重置位置", null, (_, _) => Dispatch(resetPosition ?? show)),
            new Forms.ToolStripMenuItem("召唤到鼠标附近", null, (_, _) => Dispatch(summon ?? show)),
            _ball, _frisbee,
            _follow, _challenge, _stopPlay,
            new Forms.ToolStripMenuItem("保存宠物拍立得", null, (_, _) => Dispatch(photo ?? (() => {}))),
            _home,
            new Forms.ToolStripMenuItem("宠物状态与专注…", null, (_, _) => Dispatch(showCompanion)), focusMenu,
            new Forms.ToolStripSeparator(), _pause, _clickThrough,
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

    public void UpdateState(bool visible, bool autoBehaviorEnabled, bool clickThrough,
        bool fullscreenSuppressed, bool requestedVisible, bool playing = false, bool toys = true, bool home = false)
    {
        if (_disposed) return;
        _show.Enabled = !requestedVisible;
        _hide.Enabled = requestedVisible;
        _petHead.Enabled = visible;
        _feed.Enabled = visible;
        _follow.Enabled = visible;
        _challenge.Enabled = visible && toys;
        _ball.Enabled = visible && toys;
        _frisbee.Enabled = visible && toys;
        _home.Enabled = visible && home;
        _stopPlay.Enabled = playing;
        _clickThrough.Checked = clickThrough;
        _pause.Text = autoBehaviorEnabled ? "暂停自动行为" : "恢复自动行为";
        _visibilityStatus = fullscreenSuppressed && requestedVisible ? "全屏时暂时隐藏" : visible ? "已显示" : "已隐藏";
        _interactionStatus = clickThrough ? "鼠标穿透" : "可互动";
        RefreshTooltip();
    }

    public void UpdatePetName(string name)
    {
        if (_disposed) return;
        _petName = name;
        RefreshTooltip();
    }
    private void RefreshTooltip()
    {
        var suffix = " · " + _visibilityStatus + " · " + _interactionStatus + _focusTooltip;
        var prefix = "DesktopPet · ";
        var budget = Math.Max(0, 63 - prefix.Length - suffix.Length);
        var name = _petName;
        if (name.Length > budget)
        {
            var boundary = 0;
            foreach (var start in StringInfo.ParseCombiningCharacters(name))
            {
                var length = StringInfo.GetNextTextElement(name, start).Length;
                if (start + length > budget - 1) break;
                boundary = start + length;
            }
            name = name[..boundary] + "…";
        }
        _notifyIcon.Text = prefix + name + suffix;
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

    public void UpdateFocus(FocusPhase phase, bool paused, TimeSpan remaining)
    {
        if (_disposed) return;
        var active = phase is FocusPhase.Focus or FocusPhase.Break;
        _focusStart.Enabled = !active;
        _focusPause.Enabled = active;
        _focusStop.Enabled = active;
        _focusPause.Text = paused ? "继续" : "暂停";
        var seconds = Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
        _focusStatus.Text = active ? $"{(phase == FocusPhase.Focus ? "专注" : "休息")} {seconds / 60:00}:{seconds % 60:00}{(paused ? "（暂停）" : "")}" :
            phase == FocusPhase.Completed ? "本轮已完成" : "番茄钟未开始";
        _focusTooltip = active ? " · " + _focusStatus.Text : phase == FocusPhase.Completed ? " · 本轮已完成" : "";
        RefreshTooltip();
    }
}
