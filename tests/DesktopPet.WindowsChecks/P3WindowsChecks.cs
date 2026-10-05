using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DesktopPet.Companions;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

internal static class P3WindowsChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, ScreenBoundsService screen,
        Action<bool, string> check, Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
            EnableAnimations = false, ClickThrough = false, GrowthEnabled = true });
        controller.ShowPet();
        var companion = controller.Companion;
        var before = companion.Profile.Fullness;
        typeof(PetController).GetField("_lastFeed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(controller, DateTime.MinValue);
        controller.Feed();
        check(companion.Profile.Fullness >= Math.Min(100, before + 19.9), "feeding is connected to care profile");
        controller.ApplySettings(controller.Settings with { GrowthEnabled = false });
        before = companion.Profile.Fullness;
        typeof(PetController).GetField("_lastFeed", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(controller, DateTime.MinValue);
        controller.Feed();
        check(companion.Profile.Fullness == before, "disabled growth still allows feeding without value changes");
        controller.ApplySettings(controller.Settings with { ActivityRange = ActivityRange.Corner, PetScale = 2 });
        var region = screen.ActivityWorkArea(new(pet.Width, pet.Height));
        check(pet.Left >= region.Left && pet.Top >= region.Top && pet.Left + pet.Width <= region.Right + .01 &&
            pet.Top + pet.Height <= region.Bottom + .01, "settings clamp scaled pet into chosen activity area");
        controller.ApplySettings(controller.Settings with { ActivityRange = ActivityRange.Full, PetScale = 1 });
        var now = DateTimeOffset.UtcNow;
        controller.ApplySettings(controller.Settings with { WaterReminderEnabled = true, WaterReminderMinutes = 15 });
        controller.HidePet();
        companion.Advance(now.AddMinutes(15).AddSeconds(1));
        var bubble = Field<Window>(Field<object>(controller, "_speech"), "_bubble");
        check(!pet.IsVisible && !bubble.IsVisible, "health reminder never reveals hidden pet");
        controller.ShowPet();
        companion.Advance(now.AddMinutes(16));
        check(bubble.IsVisible && Field<TextBlock>(Field<object>(controller, "_speech"), "_text").Text.Contains("喝口水"),
            "fresh deferred reminder reaches pet bubble");
        var reminders = Field<ReminderScheduler>(companion, "_reminders");
        reminders.Enqueue(ReminderKind.FocusFinished, now.AddMinutes(19));
        controller.SetTrayMenuOpen(true);
        companion.Advance(now.AddMinutes(19));
        check(reminders.PendingCount == 1, "tray context menu defers reminders");
        controller.SetTrayMenuOpen(false);
        controller.PetHead();
        companion.Advance(now.AddMinutes(19));
        check(reminders.PendingCount == 1, "reminder waits while pet interaction is active");
        controller.ApplySettings(controller.Settings with { EnableAnimations = false });
        controller.SetFullscreenSuppressed(true);
        companion.Advance(now.AddMinutes(20));
        check(!bubble.IsVisible && reminders.PendingCount == 1, "fullscreen suppression also holds reminder");
        companion.Advance(now.AddMinutes(30));
        controller.SetFullscreenSuppressed(false);
        companion.Advance(now.AddMinutes(30));
        check(!bubble.IsVisible, "expired reminders do not pile up on fullscreen exit");
        controller.ApplySettings(controller.Settings with { WaterReminderEnabled = false });
        companion.Focus.Start(now.AddHours(1), 1, 1);
        controller.ApplySettings(controller.Settings with { PetName = "棉花糖", PetPersonality = PetPersonality.Clingy });
        check(companion.Focus.Phase == FocusPhase.Focus, "unrelated settings do not restart or cancel focus");
        companion.Advance(now.AddHours(1).AddMinutes(1));
        check(companion.Focus.Phase == FocusPhase.Break, "companion service advances focus phase");
        controller.StopFocus();
        check(companion.Focus.Phase == FocusPhase.Stopped && reminders.PendingCount == 0,
            "focus stop cancels phase and queued notices");
        var settings = new SettingsWindow(controller.Settings);
        settings.SettingsEdited += controller.ApplySettings;
        settings.FocusStartRequested += controller.StartFocus;
        settings.FocusPauseRequested += controller.ToggleFocusPause;
        settings.FocusStopRequested += controller.StopFocus;
        controller.CompanionChanged += () => settings.SetCompanionStatus(controller.Companion.Profile,
            controller.Companion.Focus.Phase, controller.Companion.Focus.IsPaused, controller.Companion.Focus.Remaining(DateTimeOffset.UtcNow));
        Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 1;
        settings.Show();
        pump(100);
        Field<TextBox>(settings, "PetNameText").Text = "雪球";
        check(controller.Settings.PetName == "雪球" && controller.Settings.PetPersonality == PetPersonality.Clingy,
            "companion settings edits propagate and preserve other choices");
        Field<Button>(settings, "FocusStartButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(companion.Focus.Phase == FocusPhase.Focus, "focus starts from settings button");
        Field<Button>(settings, "FocusPauseButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(companion.Focus.IsPaused, "focus pauses from settings button");
        render(settings, Path.Combine(evidence, "companion-top.png"));
        Field<ScrollViewer>(settings, "CompanionScroll").ScrollToBottom();
        pump(100);
        render(settings, Path.Combine(evidence, "companion-bottom.png"));
        Field<Button>(settings, "FocusStopButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(!companion.Focus.IsActive, "focus stops from settings button");
        settings.Close();
        controller.ApplySettings(controller.Settings with { PetPersonality = PetPersonality.Default,
            ActivityRange = ActivityRange.Full, WaterReminderEnabled = false, GrowthEnabled = true,
            EnableAnimations = true, AnimationSpeedMultiplier = 1 });
    }
}
