using System.Reflection;
using System.Windows.Threading;
using DesktopPet.Services;
using Forms = System.Windows.Forms;

internal static class P8TrayChecks
{
    public static void Run(Action<bool, string> check)
    {
        using var tray = new TrayIconService(Dispatcher.CurrentDispatcher, show: () => { }, hide: () => { },
            togglePause: () => { }, settings: () => { }, exit: () => { }, toggleClickThrough: () => { },
            petHead: () => { }, feed: () => { }, showCompanion: () => { }, focusStart: () => { },
            focusPause: () => { }, focusStop: () => { }, menuStateChanged: _ => { });
        var menu = (Forms.ContextMenuStrip)typeof(TrayIconService).GetField("_menu",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(tray)!;
        Forms.ToolStripMenuItem Item(string text) => menu.Items.OfType<Forms.ToolStripMenuItem>().Single(item => item.Text == text);
        var ball = Item("抛小球"); var frisbee = Item("抛飞盘"); var home = Item("回小窝睡觉");
        tray.UpdateState(true, true, false, false, true, false, false);
        check(!ball.Enabled && !frisbee.Enabled, "P8 tray disables both toys when toys are turned off");
        tray.UpdateState(false, true, false, true, true, false, true);
        check(!ball.Enabled && !frisbee.Enabled && !home.Enabled, "P8 hidden/fullscreen pet cannot start tray toy or home actions");
        tray.UpdateState(true, true, false, false, true, false, true);
        check(ball.Enabled && frisbee.Enabled, "P8 showing the pet restores enabled toy actions");
        var parameters = typeof(TrayIconService).GetMethod("UpdateState")!.GetParameters();
        check(parameters.Any(parameter => parameter.Name == "home"), "P8 tray accepts the current home setting");
        typeof(TrayIconService).GetMethod("UpdateState")!.Invoke(tray, [true, true, false, false, true, false, true, false]);
        check(!home.Enabled, "P8 tray disables returning home when the home is not configured");
        typeof(TrayIconService).GetMethod("UpdateState")!.Invoke(tray, [true, true, false, false, true, false, true, true]);
        check(home.Enabled, "P8 tray restores returning home when the home is configured");
    }
}
