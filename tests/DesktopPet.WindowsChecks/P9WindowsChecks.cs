using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopPet.Controllers;
using DesktopPet.Models;
using DesktopPet.Services;
using DesktopPet.Views;

public static class P9WindowsChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, Action<bool, string> check,
        Action<int> pump, Action<FrameworkElement, string> render, string evidence, bool saveOnly = false)
    {
        var failures = new List<string>();
        void Probe(bool condition, string name)
        {
            if (condition) check(true, name);
            else { failures.Add(name); Console.WriteLine("FAIL: " + name); }
        }

        if (!saveOnly) CheckAvailability(controller, check: Probe, pump, render, evidence);
        CheckSaving(controller, pet, Probe);
        check(failures.Count == 0, "P9 native checks: " + string.Join("; ", failures));
    }

    private static void CheckAvailability(PetController controller, Action<bool, string> check,
        Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var original = controller.Settings;
        controller.ApplySettings(original with { ToysEnabled = true, HomeEnabled = true, FullscreenAutoHide = true });
        var settings = new SettingsWindow(controller.Settings);
        var service = new ApplicationService(Application.Current,
            new AppLogger(Path.Combine(evidence, "p9-owned-service")));
        typeof(ApplicationService).GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, controller);
        typeof(ApplicationService).GetField("_settingsWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, settings);
        void Update() => typeof(ApplicationService).GetMethod("UpdateTray", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(service, null);
        var buttons = new[] { "BallButton", "FrisbeeButton", "ChallengeButton", "HomeButton", "FollowButton" }
            .Select(name => (Button)settings.FindName(name)).ToArray();
        try
        {
            settings.SettingsEdited += controller.ApplySettings;
            controller.HidePet(); Update();
            check(buttons.All(button => !button.IsEnabled), "P9 hidden pet disables all five settings play actions through application state");
            var availability = settings.FindName("PlayAvailability") as TextBlock;
            check(availability is not null && availability.Visibility == Visibility.Visible && availability.Text.Contains("隐藏"),
                "P9 hidden play actions explain why they are unavailable");
            ((CheckBox)settings.FindName("ToysCheck")).IsChecked = false;
            ((CheckBox)settings.FindName("ToysCheck")).IsChecked = true;
            check(buttons.All(button => !button.IsEnabled), "P9 changing toy settings while hidden cannot re-enable unavailable play actions");
            var showButton = settings.FindName("ShowPetButton") as Button;
            var showAction = typeof(SettingsWindow).GetEvent("ShowPetRequested");
            if (showButton is null || showAction?.EventHandlerType != typeof(Action))
                check(false, "P9 settings provide a display action for the hidden pet");
            else
            {
                showAction.AddEventHandler(settings, (Action)controller.ShowPet);
                var previousClickThrough = controller.Settings.ClickThrough;
                var requests = 0;
                showAction.AddEventHandler(settings, (Action)(() => requests++));
                showButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Update();
                check(requests == 1 && controller.IsPetVisible && controller.Settings.ClickThrough == previousClickThrough,
                    "P9 displaying from settings shows the pet without resetting preferences");
            }
            controller.ShowPet(); Update();
            check(buttons.All(button => button.IsEnabled), "P9 showing the pet restores all enabled play actions");
            controller.SetFullscreenSuppressed(true); Update();
            check(buttons.All(button => !button.IsEnabled) && availability?.Text.Contains("全屏") == true && showButton?.IsEnabled == false,
                "P9 fullscreen suppression explains unavailable actions and prevents a display request that cannot succeed");
            ((FrameworkElement)settings.Content).LayoutTransform = new ScaleTransform(1.5, 1.5);
            ((TabControl)settings.FindName("SettingsTabs")).SelectedIndex = 3;
            settings.ShowActivated = false;
            settings.Show(); pump(100);
            render(settings, Path.Combine(evidence, "p9-fullscreen-play-150pct.png"));
            controller.SetFullscreenSuppressed(false); Update();
            check(buttons.All(button => button.IsEnabled), "P9 leaving fullscreen restores the play actions automatically");
            ((CheckBox)settings.FindName("ToysCheck")).IsChecked = false; Update();
            check(!buttons[0].IsEnabled && !buttons[1].IsEnabled && !buttons[2].IsEnabled && buttons[3].IsEnabled && buttons[4].IsEnabled,
                "P9 visible play actions still honor independent toy and home preferences");
            ((TabControl)settings.FindName("SettingsTabs")).SelectedIndex = 4;
            render(settings, Path.Combine(evidence, "p9-data-help-150pct.png"));
            var nameInput = (TextBox)settings.FindName("PetNameText");
            var completeName = new string('A', 19) + "🐶";
            nameInput.Text = completeName + "Z";
            nameInput.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, nameInput, settings)
                { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            check(controller.Settings.PetName == completeName && nameInput.Text == completeName,
                "P9 leaving the name editor displays the same complete emoji name that is saved");
        }
        finally
        {
            settings.Close();
            typeof(ApplicationService).GetField("_settingsWindow", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, null);
            typeof(ApplicationService).GetField("_controller", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(service, null);
            service.Dispose();
            controller.SetFullscreenSuppressed(false);
            controller.ApplySettings(original);
            controller.ShowPet();
        }
    }

    private static void CheckSaving(PetController controller, PetWindow pet, Action<bool, string> check)
    {
        var settingsService = Field<SettingsService>(controller, "_settingsService");
        var directory = Path.GetDirectoryName(settingsService.SettingsPath)!;
        var profiles = new PetProfileService(directory, new AppLogger(directory));
        controller.SaveNow();
        var results = new List<bool>();
        void Saved(bool saved) => results.Add(saved);
        controller.SettingsSaved += Saved;
        try
        {
            using (var locked = new FileStream(profiles.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                results.Clear(); controller.SaveNow();
                check(results.SequenceEqual(new[] { false }), "P9 a locked profile produces one failed aggregate save result without a premature success");
                check(File.ReadAllText(profiles.ProfilePath).Length > 0, "P9 failed profile replacement preserves the existing profile");
            }
            results.Clear(); controller.Companion.Save();
            check(results.SequenceEqual(new[] { true }), "P9 successful profile retry clears the previous aggregate save failure");
            using (var locked = new FileStream(settingsService.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                results.Clear(); controller.SaveNow(); controller.Companion.Save();
                check(results.Count > 0 && results.All(result => !result), "P9 successful profile writes cannot mask a settings-file save failure");
            }
            results.Clear(); controller.SaveNow();
            check(results.SequenceEqual(new[] { true }), "P9 saving both files successfully restores the aggregate result");
        }
        finally { controller.SettingsSaved -= Saved; }

        var retryRoot = Path.Combine(directory, "p9-retry");
        var retryProfiles = new PetProfileService(retryRoot, new AppLogger(retryRoot));
        using var companion = new CompanionService(pet.Dispatcher, retryProfiles,
            new AppSettings { GrowthEnabled = false, JournalEnabled = false }, () => false, () => false, () => false);
        var now = DateTimeOffset.UtcNow;
        companion.Save(now);
        var attempts = 0;
        bool? lastResult = null;
        companion.ProfileSaved += saved => { attempts++; lastResult = saved; };
        using (var locked = new FileStream(retryProfiles.ProfilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            companion.Save(now);
            check(lastResult == false, "P9 retry fixture actually produces a profile save failure");
            attempts = 0;
            companion.Advance(now.AddSeconds(9));
            check(attempts == 0, "P9 a failed profile save does not retry every timer tick");
        }
        companion.Advance(now.AddSeconds(11));
        check(attempts == 1 && lastResult == true, "P9 profile persistence retries within eleven seconds after the lock is released");
        companion.Advance(now.AddSeconds(59));
        check(attempts == 1, "P9 profile recovery returns to the regular save interval");
        companion.Advance(now.AddSeconds(72));
        check(attempts == 2 && lastResult == true, "P9 normal minute saves continue after profile recovery");
    }
}
