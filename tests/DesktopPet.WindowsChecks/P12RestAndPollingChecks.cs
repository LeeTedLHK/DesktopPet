using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopPet.Behaviors;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Views;

internal static class P12RestAndPollingChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, Action<bool, string> check, Action<int> pump)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }
        var original = controller.Settings;
        var state = Field<PetStateMachine>(controller, "_state");
        var behavior = Field<BehaviorManager>(controller, "_behavior");
        var menu = Field<ContextMenu>(pet, "PetMenu");
        var hitTimer = Field<DispatcherTimer>(pet, "_hitTimer");
        var contextTimer = Field<DispatcherTimer>(behavior, "_contextTimer");
        var sleepTimer = Field<DispatcherTimer>(behavior, "_sleepTimer");
        bool NativePasses() => (GetWindowLongPtr(new WindowInteropHelper(pet).Handle, -20).ToInt64() & 0x20) != 0;
        void Rest(bool night)
        {
            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = true, EnableAnimations = false,
                CursorTrackingEnabled = false, HomeEnabled = true, NightSleepEnabled = night,
                NightSleepStartHour = DateTime.Now.Hour, NightSleepEndHour = (DateTime.Now.Hour + 1) % 24 });
            behavior.StartHomeSleep();
        }
        try
        {
            controller.ShowPet();
            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                ClickThrough = false, PreciseHitTestEnabled = false });
            Probe(!hitTimer.IsEnabled && !NativePasses(), "P12 whole-window interaction avoids cursor polling and accepts clicks");
            controller.ApplySettings(controller.Settings with { PreciseHitTestEnabled = true });
            Probe(hitTimer.IsEnabled, "P12 precise alpha interaction enables cursor polling");
            controller.ToggleClickThrough();
            Probe(!hitTimer.IsEnabled && NativePasses(), "P12 explicit click-through stops polling and immediately passes clicks");
            controller.ToggleClickThrough();
            Probe(hitTimer.IsEnabled, "P12 disabling explicit click-through restores precise cursor polling");
            controller.ApplySettings(controller.Settings with { PreciseHitTestEnabled = false });
            Probe(!hitTimer.IsEnabled && !NativePasses(), "P12 disabling precise interaction clears stale pass-through without a timer");
            controller.HidePet(); controller.ShowPet();
            Probe(!hitTimer.IsEnabled, "P12 showing a whole-window pet does not restart unnecessary polling");
            controller.ApplySettings(controller.Settings with { PreciseHitTestEnabled = true });
            controller.HidePet();
            Probe(!hitTimer.IsEnabled, "P12 hidden pets stop alpha cursor polling");
            controller.ShowPet();
            Probe(hitTimer.IsEnabled, "P12 showing a precise pet restores cursor polling");

            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = true, NightSleepEnabled = false });
            controller.HidePet();
            controller.SetTrayMenuOpen(true);
            controller.ShowPet();
            Probe(!contextTimer.IsEnabled && !Field<DispatcherTimer>(behavior, "_decisionTimer").IsEnabled,
                "P12 showing a pet while its tray menu is open keeps automatic decisions suspended");
            controller.SetTrayMenuOpen(false);

            Rest(true);
            var homeCount = controller.Companion.Profile.Journal.HomeCount;
            menu.IsOpen = true;
            Probe(state.State == PetState.Sleeping && !contextTimer.IsEnabled,
                "P12 opening the dog menu keeps night sleep and pauses automatic decisions");
            menu.IsOpen = false;
            pump(80); // ContextMenu.Closed is dispatched by the Popup lifecycle.
            Probe(state.State == PetState.Sleeping && contextTimer.IsEnabled,
                "P12 closing the dog menu resumes night monitoring without waking the dog");
            pump(650);
            Probe(state.State == PetState.Sleeping && controller.Companion.Profile.Journal.HomeCount == homeCount && !controller.IsPlaying,
                "P12 browsing the dog menu cannot trigger another home journey or journal reward");

            Rest(true);
            homeCount = controller.Companion.Profile.Journal.HomeCount;
            controller.SetTrayMenuOpen(true);
            Probe(state.State == PetState.Sleeping && !contextTimer.IsEnabled,
                "P12 opening the tray menu also preserves rest and pauses automatic decisions");
            typeof(PetController).GetMethod("OnBoundsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
            Probe(state.State == PetState.Sleeping && !contextTimer.IsEnabled,
                "P12 a display bounds refresh keeps tray-menu automatic decisions suspended");
            menu.IsOpen = true;
            menu.IsOpen = false;
            pump(80);
            Probe(!contextTimer.IsEnabled,
                "P12 closing the dog menu cannot resume decisions while the tray menu is still open");
            pump(650);
            Probe(state.State == PetState.Sleeping && !controller.IsPlaying,
                "P12 automatic home movement cannot start while the tray menu stays open");
            controller.SetTrayMenuOpen(false);
            Probe(state.State == PetState.Sleeping && contextTimer.IsEnabled && controller.Companion.Profile.Journal.HomeCount == homeCount,
                "P12 closing the tray menu preserves the current sleep and journal count");
            controller.ApplySettings(controller.Settings with { Topmost = !controller.Settings.Topmost, PetScale = 1.1 });
            Probe(state.State == PetState.Sleeping && contextTimer.IsEnabled,
                "P12 changing size and topmost preferences keeps a sleeping dog asleep");
            controller.ApplySettings(controller.Settings with { NightSleepEnabled = false });
            Probe(state.State == PetState.Idle, "P12 turning off the night schedule still wakes night sleep");

            Rest(false);
            menu.IsOpen = true;
            Probe(state.State == PetState.Sleeping && sleepTimer.IsEnabled,
                "P12 a daytime nap retains its existing deadline while browsing the dog menu");
            sleepTimer.Interval = TimeSpan.FromMilliseconds(50);
            pump(120);
            Probe(state.State == PetState.Idle && !sleepTimer.IsEnabled && !contextTimer.IsEnabled,
                "P12 a nap can finish naturally while the menu remains open");
            menu.IsOpen = false;
            pump(80);
            Probe(state.State == PetState.Idle, "P12 closing a menu does not resurrect a finished nap");

            Rest(true);
            controller.PetHead();
            Probe(state.State == PetState.Petting && !Field<bool>(behavior, "_nightSleep"),
                "P12 petting still wakes the dog and cancels its current night sleep");
            pump(1700);
            Probe(state.State == PetState.Idle && Field<DateTime>(behavior, "_wakeUntil") > DateTime.UtcNow,
                "P12 a woken dog still receives the existing 15-second interaction grace period");
            Rest(true); controller.HidePet();
            Probe(!controller.IsPetVisible && state.State == PetState.Paused && !contextTimer.IsEnabled && !sleepTimer.IsEnabled,
                "P12 hiding a resting dog cancels its background behavior timers");
        }
        finally
        {
            menu.IsOpen = false;
            pump(80);
            controller.SetTrayMenuOpen(false);
            controller.ApplySettings(original);
            controller.HidePet(); controller.ShowPet();
        }
        check(failures.Count == 0, "P12 rest and polling checks: " + string.Join("; ", failures));
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
}
