using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DesktopPet.Animations;
using DesktopPet.Behaviors;
using DesktopPet.Companions;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P14UsabilityChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Fetch(PetWorldService world) =>
        typeof(PetWorldService).GetField("_fetch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world);
    private static AppSettings Edit(AppSettings settings, string name, object value)
    {
        var json = JsonSerializer.SerializeToNode(settings)!.AsObject();
        json[name] = JsonSerializer.SerializeToNode(value);
        return json.Deserialize<AppSettings>()!;
    }

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        { if (condition) check(true, name); else { failures.Add(name); Console.WriteLine("FAIL: " + name); } }
        var original = controller.Settings;
        var world = Field<PetWorldService>(controller, "_world");
        var timer = Field<DispatcherTimer>(world, "_timer");
        var timerInterval = timer.Interval;
        var challengeTimer = Field<DispatcherTimer>(controller, "_challengeTimer");
        var challengeInterval = challengeTimer.Interval;
        var menu = Field<ContextMenu>(pet, "PetMenu");
        var behavior = Field<BehaviorManager>(controller, "_behavior");
        var contextTimer = Field<DispatcherTimer>(behavior, "_contextTimer");
        var state = Field<PetStateMachine>(controller, "_state");
        var advance = typeof(PetWorldService).GetMethod("AdvanceFetch", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            controller.ApplySettings(Edit(original with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                ToysEnabled = true, HomeEnabled = false, ActivityRange = ActivityRange.Full,
                EnableAnimations = true, AnimationSpeedMultiplier = 1 }, "FocusQuietEnabled", false));
            controller.ShowPet();
            timer.Interval = TimeSpan.FromSeconds(30);
            var changes = new (string Name, Func<AppSettings, AppSettings> Edit)[]
            {
                ("topmost", s => s with { Topmost = !s.Topmost }),
                ("speech bubbles", s => s with { SpeechBubblesEnabled = !s.SpeechBubblesEnabled }),
                ("name", s => s with { PetName = "测试团团" }),
                ("care", s => s with { GrowthEnabled = !s.GrowthEnabled }),
                ("reminders", s => s with { WaterReminderEnabled = true }),
                ("focus duration", s => s with { FocusMinutes = 30 }),
                ("power saving", s => s with { PowerSavingEnabled = !s.PowerSavingEnabled }),
                ("next ball size", s => s with { BallScale = 1.25 }),
                ("feeding bowl size", s => s with { FeedingBowlScale = .75 }),
            };
            foreach (var change in changes)
            {
                controller.ThrowToy(ToyKind.Ball);
                var fetch = Fetch(world);
                controller.ApplySettings(change.Edit(controller.Settings));
                Probe(fetch is not null && ReferenceEquals(fetch, Fetch(world)) && controller.IsPlaying && world.IsToyVisible,
                    $"P14 changing {change.Name} preserves the current fetch object and visible toy");
                controller.StopPlay();
            }

            controller.ThrowToy(ToyKind.Frisbee);
            var savedFetch = Fetch(world);
            controller.SetTrayMenuOpen(true);
            var savedPosition = screen.Position(pet);
            var frame = pet.CurrentFrame;
            advance.Invoke(world, [.1]); pump(120);
            Probe(ReferenceEquals(savedFetch, Fetch(world)) && controller.IsPlaying && world.IsToyVisible &&
                !timer.IsEnabled && screen.Position(pet) == savedPosition && ReferenceEquals(frame, pet.CurrentFrame),
                "P14 opening tray menu freezes motion and animation without losing the game");
            menu.IsOpen = true;
            controller.SetTrayMenuOpen(false);
            Probe(!timer.IsEnabled && ReferenceEquals(savedFetch, Fetch(world)),
                "P14 closing one menu cannot resume while the other menu stays open");
            menu.IsOpen = false; pump(80);
            Probe(timer.IsEnabled && ReferenceEquals(savedFetch, Fetch(world)),
                "P14 closing both menus resumes the same fetch");
            controller.StopPlay();

            controller.StartFetchChallenge();
            challengeTimer.Interval = TimeSpan.FromSeconds(30);
            for (var i = 0; i < 2000 && world.IsToyVisible; i++) advance.Invoke(world, [.05]);
            var caught = Field<int>(controller, "_challengeCaught");
            var playCount = controller.Companion.Profile.Journal.PlayCount;
            controller.SetTrayMenuOpen(true); pump(120);
            Probe(caught == 1 && Field<bool>(controller, "_challengeActive") && !challengeTimer.IsEnabled &&
                controller.Companion.Profile.Journal.PlayCount == playCount,
                "P14 challenge round pause preserves one catch without a duplicate journal reward");
            controller.SetTrayMenuOpen(false);
            Probe(challengeTimer.IsEnabled && Field<int>(controller, "_challengeCaught") == 1,
                "P14 challenge resumes its pending next round after menu closure");
            controller.StopPlay();

            controller.FollowCursor();
            var followStart = Field<long>(world, "_followStarted");
            controller.SetTrayMenuOpen(true); pump(120);
            Probe(world.IsFollowingCursor && world.IsPaused && controller.IsPlaying,
                "P14 following remains an active paused session inside menus");
            controller.SetTrayMenuOpen(false);
            Probe(Field<long>(world, "_followStarted") > followStart && world.IsMoving,
                "P14 follow deadline excludes the menu pause duration");
            controller.StopPlay();
            controller.ThrowToy(ToyKind.Ball);
            var currentDiameter = Field<ToySimulation>(world, "_simulation").Diameter;
            controller.ApplySettings(controller.Settings with { BallScale = .6 });
            Probe(Field<ToySimulation>(world, "_simulation").Diameter == currentDiameter,
                "P14 current toy diameter remains consistent with its ongoing physics");
            controller.StopPlay(); controller.ThrowToy(ToyKind.Ball);
            Probe(Field<ToySimulation>(world, "_simulation").Diameter < currentDiameter,
                "P14 updated ball size takes effect on the next throw");
            controller.SetTrayMenuOpen(true); controller.ThrowToy(ToyKind.Frisbee);
            Probe(world.IsPaused && !timer.IsEnabled && world.IsToyVisible,
                "P14 a new game started inside a menu waits for menu closure");
            controller.SetTrayMenuOpen(false); controller.StopPlay();

            controller.ThrowToy(ToyKind.Ball);
            controller.ApplySettings(controller.Settings with { PetScale = controller.Settings.PetScale == 1 ? 1.2 : 1 });
            Probe(!controller.IsPlaying && !world.IsToyVisible, "P14 geometry changes still cancel an obsolete game");
            controller.ThrowToy(ToyKind.Ball); controller.HidePet();
            Probe(!controller.IsPlaying && !world.IsToyVisible, "P14 hiding still cancels a paused or active game");
            controller.ShowPet();

            controller.ApplySettings(Edit(controller.Settings with { AutoBehaviorEnabled = true, FocusMinutes = 1,
                NightSleepEnabled = false, WaterReminderEnabled = true }, "FocusQuietEnabled", true));
            controller.StartFocus();
            Probe(controller.Companion.Focus.Phase == FocusPhase.Focus && !contextTimer.IsEnabled &&
                !Field<DispatcherTimer>(behavior, "_decisionTimer").IsEnabled,
                "P14 quiet focus suspends automatic behavior while its countdown keeps running");
            controller.ThrowToy(ToyKind.Ball);
            var quietFetch = Fetch(world);
            controller.ApplySettings(controller.Settings with { PetName = "专注小伙伴" });
            Probe(ReferenceEquals(quietFetch, Fetch(world)) && world.IsMoving,
                "P14 quiet focus permits explicit play and neutral settings");
            controller.StopPlay();
            Probe(!contextTimer.IsEnabled, "P14 stopping explicit play cannot restart automatic behavior during quiet focus");
            controller.ToggleFocusPause();
            Probe(controller.Companion.Focus.IsPaused && contextTimer.IsEnabled,
                "P14 pausing focus restores normal automatic behavior");
            controller.ToggleFocusPause();
            Probe(!contextTimer.IsEnabled, "P14 resuming focus reapplies quiet behavior");
            controller.SetTrayMenuOpen(true); controller.StopFocus();
            Probe(!contextTimer.IsEnabled, "P14 stopping focus while a menu is open keeps behavior suspended");
            controller.SetTrayMenuOpen(false);
            Probe(contextTimer.IsEnabled, "P14 closing the menu after focus ends restores behavior");

            controller.StartFocus();
            controller.ApplySettings(controller.Settings with { HomeEnabled = true });
            controller.ReturnHome();
            world.Cancel();
            typeof(PetController).GetMethod("OnHomeReached", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, null);
            Probe(!contextTimer.IsEnabled, "P14 quiet focus also suppresses automatic behavior after manual home sleep");
            controller.StopPlay(); controller.StopFocus();

            controller.Companion.SnoozeHealthReminders(30);
            var snoozedUntil = controller.Companion.HealthSnoozedUntil;
            controller.ApplySettings(controller.Settings with { Topmost = !controller.Settings.Topmost });
            Probe(snoozedUntil is not null && controller.Companion.HealthSnoozedUntil == snoozedUntil,
                "P14 unrelated settings preserve the active reminder snooze");
            controller.Companion.ResumeHealthReminders();
            Probe(controller.Companion.HealthSnoozedUntil is null, "P14 companion can resume health reminders early");

            var summon = typeof(PetController).GetMethod("SummonToCursor");
            controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, ClickThrough = true,
                AllowCrossMonitorDrag = false });
            controller.HidePet();
            summon?.Invoke(controller, null);
            var area = screen.ActivityWorkArea(new(pet.Width, pet.Height));
            var position = screen.Position(pet);
            Probe(summon is not null && controller.IsPetVisible && controller.Settings.ClickThrough &&
                !controller.Settings.AutoBehaviorEnabled && position.X >= area.Left - 1 && position.Y >= area.Top - 1 &&
                position.X + pet.Width <= area.Right + 1 && position.Y + pet.Height <= area.Bottom + 1,
                "P14 summon shows the dog within bounds without changing click-through or automatic preferences");
            controller.ShowPet();

            var settings = new SettingsWindow(controller.Settings);
            try
            {
                var quiet = settings.FindName("FocusQuietCheck") as CheckBox;
                var snooze = settings.FindName("Snooze30Button") as Button;
                var resume = settings.FindName("ResumeRemindersButton") as Button;
                var export = settings.FindName("ExportJournalButton") as Button;
                Probe(quiet is not null && snooze is not null && resume is not null && export is not null,
                    "P14 quiet focus, reminder controls and diary export are accessible in settings");
                if (quiet is not null)
                {
                    var edits = new List<AppSettings>();
                    settings.SettingsEdited += value => edits.Add(value);
                    quiet.IsChecked = !quiet.IsChecked;
                    Probe(edits.Count == 1 && JsonSerializer.SerializeToElement(edits[0]).TryGetProperty("FocusQuietEnabled", out _),
                        "P14 quiet focus toggle emits one persisted setting edit");
                }
                var durations = new List<int>(); var resumed = 0; var exported = 0;
                settings.SnoozeRemindersRequested += durations.Add;
                settings.ResumeRemindersRequested += () => resumed++;
                settings.ExportJournalRequested += () => exported++;
                foreach (var name in new[] { "Snooze15Button", "Snooze30Button", "Snooze60Button" })
                    ((Button)settings.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                settings.SetReminderStatus(DateTimeOffset.Now.AddMinutes(30), false);
                resume!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                export!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Probe(durations.SequenceEqual(new[] { 15, 30, 60 }) && resumed == 1 && exported == 1,
                    "P14 snooze, early resume and diary controls emit the requested actions");
                settings.Show(); pump(60);
                Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 1;
                Field<ScrollViewer>(settings, "CompanionScroll").ScrollToBottom(); pump(60);
                render(settings, Path.Combine(evidence, "p14-settings-focus.png"));
                Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 3;
                Field<ScrollViewer>(settings, "WorldScroll").ScrollToBottom(); pump(60);
                render(settings, Path.Combine(evidence, "p14-settings-diary.png"));
            }
            finally { settings.Close(); }

            using var tray = new TrayIconService(pet.Dispatcher, () => {}, () => {}, () => {}, () => {}, () => {},
                () => {}, () => {}, () => {}, () => {}, () => {}, () => {}, () => {}, _ => {});
            tray.UpdatePetName(string.Concat(Enumerable.Repeat("🐶‍🐾", 20)));
            tray.UpdateFocus(FocusPhase.Focus, true, TimeSpan.FromSeconds(91));
            tray.UpdateState(true, true, false, false, true);
            var tooltip = Field<System.Windows.Forms.NotifyIcon>(tray, "_notifyIcon").Text;
            Probe(tooltip.Length <= 63 && tooltip.Contains("专注 01:31（暂停）") && tooltip.Contains("已显示") && tooltip.Contains("可互动"),
                "P14 tray tooltip retains countdown and visibility within native length limits");
            Probe(!tooltip.Contains('\uFFFD') && !char.IsHighSurrogate(tooltip[^1]) &&
                !tooltip.Contains("\u200D…"), "P14 long emoji names truncate only at complete text elements");
            tray.UpdateState(true, true, true, true, true);
            tooltip = Field<System.Windows.Forms.NotifyIcon>(tray, "_notifyIcon").Text;
            Probe(tooltip.Length <= 63 && tooltip.Contains("鼠标穿透") && tooltip.Contains("全屏时暂时隐藏") &&
                tooltip.Contains("专注 01:31（暂停）"), "P14 tray retains interaction and fullscreen status alongside focus countdown");
            tray.UpdatePetName("团团"); tray.UpdateFocus(FocusPhase.Stopped, false, TimeSpan.Zero);
            Probe(Field<System.Windows.Forms.NotifyIcon>(tray, "_notifyIcon").Text.Contains("团团") &&
                !Field<System.Windows.Forms.NotifyIcon>(tray, "_notifyIcon").Text.Contains("01:31"),
                "P14 stopping focus clears an obsolete tray countdown");
        }
        finally
        {
            menu.IsOpen = false; pump(60);
            controller.SetTrayMenuOpen(false);
            controller.StopPlay(); controller.StopFocus();
            timer.Interval = timerInterval; challengeTimer.Interval = challengeInterval;
            controller.ApplySettings(original);
            controller.HidePet(); controller.ShowPet();
        }
        check(failures.Count == 0, "P14 usability checks: " + string.Join("; ", failures));
    }
}
