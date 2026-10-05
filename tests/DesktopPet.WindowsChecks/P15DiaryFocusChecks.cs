using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using DesktopPet.Companions;
using DesktopPet.Controllers;
using DesktopPet.Journal;
using DesktopPet.Models;
using DesktopPet.Views;
using DesktopPet.Services;
using System.Windows.Threading;

internal static class P15DiaryFocusChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target)!;

    public static void Run(PetController controller, PetWindow pet, Action<bool, string> check,
        Action<int> pump, Action<FrameworkElement, string> render, string evidence)
    {
        var failed = new List<string>();
        void Probe(bool value, string label)
        { if (value) check(true, label); else { failed.Add(label); Console.WriteLine("FAIL: " + label); } }
        var settings = new SettingsWindow(controller.Settings);
        var original = controller.Settings;
        var world = Field<PetWorldService>(controller, "_world");
        var worldTimer = Field<DispatcherTimer>(world, "_timer");
        var worldInterval = worldTimer.Interval;
        try
        {
            var search = settings.FindName("JournalSearchText") as TextBox;
            var kind = settings.FindName("JournalKindCombo") as ComboBox;
            var range = settings.FindName("JournalRangeCombo") as ComboBox;
            var clear = settings.FindName("ClearJournalFilterButton") as Button;
            var export = settings.FindName("ExportFilteredJournalButton") as Button;
            Probe(search is not null && kind is not null && range is not null && clear is not null && export is not null,
                "P15 diary search, type, date, clear and filtered-export controls are available");
            Probe(settings.FindName("FocusPreset25Button") is Button && settings.FindName("FocusPreset45Button") is Button &&
                settings.FindName("FocusPreset60Button") is Button && settings.FindName("FocusProgressBar") is ProgressBar &&
                typeof(SettingsWindow).GetMethod("SetFocusDetails") is not null,
                "P15 focus presets and real-session progress/end-time rendering are available");
            if (search is not null && kind is not null && range is not null && clear is not null && export is not null)
            {
                var now = DateTimeOffset.UtcNow;
                var today = DateOnly.FromDateTime(DateTime.Now);
                var entries = new[] {
                    new JournalEntry(now.AddDays(-1), today.AddDays(-1), JournalEntryKind.Feeding, "零食旧记录"),
                    new JournalEntry(now.AddMinutes(-1), today, JournalEntryKind.Feeding, "零食🐾新记录"),
                    new JournalEntry(now.AddMinutes(-10), today, JournalEntryKind.Play, "追球 CatchABC"),
                    new JournalEntry(now.AddDays(-6), today.AddDays(-6), JournalEntryKind.Feeding, "一周边界🥣"),
                    new JournalEntry(now.AddDays(-7), today.AddDays(-7), JournalEntryKind.Home, "回到小窝") };
                var profile = new PetProfile { Journal = new JournalState { Entries = entries } };
                settings.SetCompanionStatus(profile, FocusPhase.Stopped, false, TimeSpan.Zero);
                var list = (ListBox)settings.FindName("DiaryList");
                Probe(list.Items.Count == 5 && ReferenceEquals(list.Items[0], entries[1]),
                    "P15 diary initially shows all records with newest absolute timestamp first");
                var edits = 0; settings.SettingsEdited += _ => edits++;
                search.Text = "  🐾  "; kind.SelectedValue = JournalEntryKind.Feeding; range.SelectedIndex = 1;
                Probe(list.Items.Count == 1 && ReferenceEquals(list.Items[0], entries[1]) && edits == 0,
                    "P15 keyword, type and local-date filters combine without editing preferences");
                list.SelectedItem = entries[1]; var source = list.ItemsSource;
                settings.SetCompanionStatus(profile, FocusPhase.Focus, false, TimeSpan.FromSeconds(59));
                settings.SetCompanionStatus(profile, FocusPhase.Focus, false, TimeSpan.FromSeconds(58));
                Probe(ReferenceEquals(source, list.ItemsSource) && ReferenceEquals(list.SelectedItem, entries[1]),
                    "P15 focus countdown ticks preserve filtered diary items and selection");
                search.Text = "没有这个词";
                Probe(list.Items.Count == 0 && !export.IsEnabled &&
                    ((TextBlock)settings.FindName("JournalEmptyHint")).Visibility == Visibility.Visible,
                    "P15 empty search displays a hint and disables filtered export");
                clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Probe(search.Text == "" && range.SelectedIndex == 0 && list.Items.Count == 5 && export.IsEnabled,
                    "P15 clearing filters restores the full diary view");
                search.Text = "catchabc";
                Probe(list.Items.Count == 1 && ReferenceEquals(list.Items[0], entries[2]),
                    "P15 text lookup supports case-insensitive Latin text alongside Chinese entries");
                var next = profile with { Journal = profile.Journal with { Entries = entries.Append(
                    new JournalEntry(now, today, JournalEntryKind.Play, "CatchABC 新玩耍")).ToArray() } };
                settings.SetCompanionStatus(next, FocusPhase.Stopped, false, TimeSpan.Zero);
                Probe(search.Text == "catchabc" && list.Items.Count == 2,
                    "P15 new journal snapshots retain active filters and update matching results");
                settings.LoadSettings(controller.Settings with { Topmost = !controller.Settings.Topmost });
                Probe(search.Text == "catchabc" && list.Items.Count == 2 && edits == 0,
                    "P15 reloading preferences keeps diary filters without issuing setting edits");
                JournalFilter? exportedFilter = null;
                settings.ExportFilteredJournalRequested += value => exportedFilter = value;
                range.SelectedIndex = 1;
                export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Probe(exportedFilter is { SearchText: "catchabc", Range: JournalDateRange.Today } &&
                    exportedFilter.Today == today && exportedFilter.Kind is null,
                    "P15 filtered export captures the displayed keyword, kind and local date");
                var beforeDateRefresh = list.ItemsSource;
                typeof(SettingsWindow).GetField("_journalFilterDate", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(settings, today.AddDays(-1));
                settings.SetCompanionStatus(next, FocusPhase.Focus, false, TimeSpan.FromSeconds(57));
                Probe(settings.CurrentJournalFilter.Today == today && !ReferenceEquals(beforeDateRefresh, list.ItemsSource) &&
                    list.Items.Count == 2, "P15 a changed local day refreshes relative filters with a stable journal snapshot");
                settings.Show(); pump(60);
                Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 3;
                Field<ScrollViewer>(settings, "WorldScroll").ScrollToBottom(); pump(60);
                render(settings, Path.Combine(evidence, "p15-diary-search.png"));
                search.Text = "没有这个词"; pump(60);
                render(settings, Path.Combine(evidence, "p15-diary-empty.png"));
            }
            if (settings.FindName("FocusPreset25Button") is Button && typeof(SettingsWindow).GetMethod("SetFocusDetails") is not null)
            {
                controller.ApplySettings(controller.Settings with { AutoBehaviorEnabled = false, NightSleepEnabled = false,
                    ToysEnabled = true, FocusMinutes = 1, BreakMinutes = 1 });
                settings.LoadSettings(controller.Settings);
                controller.StartFocus();
                var deadline = controller.Companion.Focus.EstimatedEndUtc;
                worldTimer.Interval = TimeSpan.FromSeconds(30);
                controller.ThrowToy(ToyKind.Ball);
                var fetch = typeof(PetWorldService).GetField("_fetch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world);
                var emitted = new List<AppSettings>();
                settings.SettingsEdited += emitted.Add;
                settings.SettingsEdited += controller.ApplySettings;
                foreach (var preset in new[] { ("FocusPreset25Button", 25, 5), ("FocusPreset45Button", 45, 10), ("FocusPreset60Button", 60, 10) })
                {
                    var previousCount = emitted.Count;
                    ((Button)settings.FindName(preset.Item1)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Probe(emitted.Count == previousCount + 1 && emitted[^1].FocusMinutes == preset.Item2 && emitted[^1].BreakMinutes == preset.Item3,
                        $"P15 {preset.Item2}/{preset.Item3} preset emits one atomic edit for next-session durations");
                }
                Probe(controller.Companion.Focus.Duration == TimeSpan.FromMinutes(1) && controller.Companion.Focus.EstimatedEndUtc == deadline &&
                    ReferenceEquals(fetch, typeof(PetWorldService).GetField("_fetch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(world)) && world.IsMoving,
                    "P15 presets preserve the actual current focus duration/deadline and current fetch");
                controller.ToggleFocusPause();
                ((Button)settings.FindName("FocusPreset25Button")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Probe(controller.Companion.Focus.IsPaused && controller.Companion.Focus.Duration == TimeSpan.FromMinutes(1) && world.IsMoving,
                    "P15 choosing a preset also preserves paused focus and explicit play");
                controller.StopPlay(); controller.StopFocus();
                var now = DateTimeOffset.UtcNow;
                var model = new FocusTimer(); model.Start(now, 25, 5);
                settings.SetCompanionStatus(new PetProfile(), model.Phase, false, model.Remaining(now.AddMinutes(12.5)));
                settings.SetFocusDetails(model, now.AddMinutes(12.5));
                var progress = (ProgressBar)settings.FindName("FocusProgressBar");
                var details = (TextBlock)settings.FindName("FocusDetailsText");
                Probe(Math.Abs(progress.Value - 50) < .001 && details.Text.Contains("预计") && details.Text.Contains(model.EstimatedEndUtc!.Value.ToLocalTime().ToString("HH:mm")),
                    "P15 focus view uses actual current-session progress and local end time");
                settings.Show(); pump(60);
                Field<TabControl>(settings, "SettingsTabs").SelectedIndex = 1;
                Field<ScrollViewer>(settings, "CompanionScroll").ScrollToBottom(); pump(60);
                render(settings, Path.Combine(evidence, "p15-focus-running.png"));
                model.Pause(now.AddMinutes(12.5)); settings.SetFocusDetails(model, now.AddMinutes(14));
                settings.SetCompanionStatus(new PetProfile(), model.Phase, true, model.Remaining(now.AddMinutes(14)));
                Probe(Math.Abs(progress.Value - 50) < .001 && details.Text.Contains("已暂停"),
                    "P15 paused view retains progress and removes the obsolete end-time estimate");
                pump(60); render(settings, Path.Combine(evidence, "p15-focus-paused.png"));
                model.Resume(now.AddMinutes(20)); settings.SetFocusDetails(model, now.AddMinutes(20));
                Probe(Math.Abs(progress.Value - 50) < .001 && details.Text.Contains(model.EstimatedEndUtc!.Value.ToLocalTime().ToString("HH:mm")),
                    "P15 resuming updates the end estimate without resetting elapsed progress");
                model.Tick(model.EstimatedEndUtc!.Value); settings.SetFocusDetails(model, model.EstimatedEndUtc!.Value.AddMinutes(-5));
                Probe(model.Phase == FocusPhase.Break && progress.Value == 0 && details.Text.Contains("预计"),
                    "P15 rest starts its own phase progress and duration");
                model.Tick(model.EstimatedEndUtc!.Value); settings.SetFocusDetails(model, now.AddHours(1));
                Probe(progress.Value == 100 && !details.Text.Contains("预计"), "P15 completed focus displays full progress without stale deadline");
                model.Stop(); settings.SetFocusDetails(model, now.AddHours(1));
                Probe(progress.Value == 0 && !details.Text.Contains(" · 预计 "), "P15 stopping clears progress and deadline");
            }
        }
        finally
        {
            settings.Close();
            controller.StopPlay(); controller.StopFocus();
            worldTimer.Interval = worldInterval;
            controller.ApplySettings(original);
        }
        check(failed.Count == 0, "P15 diary/focus usability: " + string.Join("; ", failed));
    }
}
