using DesktopPet.Behaviors;
using DesktopPet.Companions;
using DesktopPet.Models;
using DesktopPet.Services;

internal static class P3Checks
{
    public static void Run(Action<bool, string> check)
    {
        var now = new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);
        var legacy = new AppSettings();
        check(legacy.GrowthEnabled && !legacy.WaterReminderEnabled && legacy.PetPersonality == PetPersonality.Default,
            "P3 legacy defaults preserve behavior and quiet reminders");
        var invalid = (legacy with { PetName = "  ", WaterReminderMinutes = 0, FocusMinutes = 999,
            PetPersonality = (PetPersonality)999, ActivityRange = ActivityRange.Custom,
            ActivityLeftPercent = 95, ActivityWidthPercent = 80, ActivityTopPercent = double.NaN }).Normalize();
        check(invalid.PetName == "团团" && invalid.WaterReminderMinutes == 15 && invalid.FocusMinutes == 120 &&
            invalid.PetPersonality == PetPersonality.Default, "P3 invalid settings normalize");
        check(invalid.ActivityLeftPercent == 90 && invalid.ActivityWidthPercent == 10 &&
            invalid.ActivityTopPercent == 0, "custom percentages are finite and fit work area");

        var care = new CareTracker(new PetProfile { Affection = 20, Fullness = 70, Energy = 50,
            UpdatedUtc = now.AddDays(-5) }, now);
        care.Advance(now, true, false);
        check(care.Profile.Fullness == 70 && care.Profile.Energy == 50, "no offline care penalty");
        care.Advance(now.AddMinutes(5), true, false);
        check(Math.Abs(care.Profile.Fullness - 69.75) < .001 && Math.Abs(care.Profile.Energy - 49.5) < .001,
            "awake care changes follow elapsed time");
        care.Advance(now.AddMinutes(10), true, true);
        check(Math.Abs(care.Profile.Energy - 50.5) < .001, "sleep restores energy");
        care.Advance(now.AddMinutes(15), false, false);
        check(Math.Abs(care.Profile.Energy - 50.5) < .001, "disabled care freezes values");
        care.Pet(now.AddMinutes(15), true);
        care.Pet(now.AddMinutes(15).AddSeconds(1), true);
        check(care.Profile.Affection == 21, "rapid petting does not farm affection");
        care.Feed(now.AddMinutes(16), true);
        check(care.Profile.Affection == 23 && care.Profile.Fullness > 89, "feeding rewards bond and fullness");
        var frozen = care.Profile;
        care.Feed(now.AddMinutes(17), false);
        check(care.Profile == frozen, "disabled growth leaves feeding visual only");
        var normalized = new PetProfile { Affection = double.NaN, Fullness = -10, Energy = 500 }.Normalize();
        check(normalized.Affection == 20 && normalized.Fullness == 0 && normalized.Energy == 100,
            "care values normalize safely");
        var correctedClock = new CareTracker(new PetProfile { LastBondUtc = now.AddDays(1) }, now);
        correctedClock.Pet(now, true);
        check(correctedClock.Profile.Affection == 21, "future persisted reward time recovers after clock correction");
        correctedClock.Pet(now.AddDays(1), true);
        correctedClock.Pet(now.AddMinutes(2), true);
        check(correctedClock.Profile.Affection == 23, "running care reward recovers after backward clock correction");

        var root = Path.Combine(Path.GetTempPath(), "DesktopPet.P3." + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new AppLogger(root);
            var profiles = new PetProfileService(root, logger);
            check(profiles.Save(care.Profile) && profiles.Load().Affection == 23, "profile atomic save and reload");
            File.WriteAllText(profiles.ProfilePath, "bad json");
            check(profiles.Load().Fullness == 70 && Directory.GetFiles(root, "profile.corrupt.*.json").Length == 1,
                "corrupt profile is backed up and recovered");
        }
        finally { Directory.Delete(root, true); }

        var focus = new FocusTimer();
        focus.Start(now, 25, 5);
        focus.Pause(now.AddMinutes(10));
        check(focus.IsPaused && focus.Remaining(now.AddHours(1)) == TimeSpan.FromMinutes(15),
            "paused focus keeps remaining time");
        focus.Resume(now.AddHours(1));
        check(focus.Tick(now.AddHours(1).AddMinutes(14)) is null, "resumed focus keeps deadline");
        check(focus.Tick(now.AddHours(1).AddMinutes(15)) == FocusPhase.Break && !focus.IsPaused,
            "focus ends and starts break once");
        check(focus.Tick(now.AddHours(1).AddMinutes(20)) == FocusPhase.Completed &&
            focus.Tick(now.AddHours(2)) is null, "break ends without starting more cycles");
        focus.Start(now.AddHours(3), 25, 5);
        check(focus.Tick(now.AddHours(8)) == FocusPhase.Break &&
            focus.Remaining(now.AddHours(8)) == TimeSpan.FromMinutes(5), "long suspension gives a full break");
        focus.Stop();
        check(focus.Phase == FocusPhase.Stopped && focus.Tick(now.AddHours(9)) is null, "stop cancels focus");
        focus.Start(now.AddHours(10), 25, 5);
        focus.Pause(now.AddHours(10).AddMinutes(25));
        check(focus.Phase == FocusPhase.Break && focus.IsPaused && focus.Remaining(now.AddHours(11)) ==
            TimeSpan.FromMinutes(5), "pause at deadline safely pauses the new break");
        focus.Start(now.AddHours(12), 25, 5);
        focus.Remaining(now.AddHours(12).AddMinutes(10));
        check(focus.Remaining(now.AddHours(12).AddMinutes(5)) == TimeSpan.FromMinutes(15),
            "clock reversal cannot lengthen focus");

        var reminders = new ReminderScheduler();
        var options = legacy with { WaterReminderEnabled = true, WaterReminderMinutes = 15,
            MoveReminderEnabled = true, MoveReminderMinutes = 15 };
        reminders.Configure(options, now);
        check(reminders.Tick(now.AddMinutes(14), true) is null, "health reminder waits for interval");
        check(reminders.Tick(now.AddMinutes(15), false) is null && reminders.PendingCount == 2,
            "suppressed reminders wait without displaying");
        check(reminders.Tick(now.AddMinutes(16), true)?.Kind == ReminderKind.Water,
            "unsuppressed reminder delivers once");
        check(reminders.Tick(now.AddMinutes(17), true) is null &&
            reminders.Tick(now.AddMinutes(18), true)?.Kind == ReminderKind.Move, "reminders are spaced by two minutes");
        reminders.Tick(now.AddMinutes(30), false);
        reminders.Configure(options with { WaterReminderEnabled = false, MoveReminderEnabled = false }, now.AddMinutes(31));
        check(reminders.PendingCount == 0, "disable clears queued health reminders");
        reminders.Enqueue(ReminderKind.FocusFinished, now);
        reminders.Enqueue(ReminderKind.FocusFinished, now.AddMinutes(1));
        check(reminders.PendingCount == 1, "same notification is merged");
        check(reminders.Tick(now.AddMinutes(12), true) is null, "stale hidden notifications expire");
        reminders.Enqueue(ReminderKind.FocusFinished, now.AddMinutes(13));
        reminders.ClearFocus();
        check(reminders.PendingCount == 0, "stopping focus clears its queued notices");
        var priority = new ReminderScheduler();
        priority.Configure(options, now);
        priority.Tick(now.AddMinutes(15), false);
        priority.Enqueue(ReminderKind.FocusFinished, now.AddMinutes(15));
        check(priority.Tick(now.AddMinutes(15), true)?.Kind == ReminderKind.FocusFinished,
            "focus completion takes priority over health messages");

        var random = new Random(41);
        var livelyWalks = 0;
        var quietWalks = 0;
        for (var i = 0; i < 1000; i++)
        {
            if (PersonalityPolicy.NextAction(random, PetPersonality.Lively, false, false) is PetState.WalkingLeft or PetState.WalkingRight) livelyWalks++;
            if (PersonalityPolicy.NextAction(random, PetPersonality.Quiet, false, false) is PetState.WalkingLeft or PetState.WalkingRight) quietWalks++;
        }
        check(livelyWalks > 650 && quietWalks < 250, "personalities visibly change walking probability");
        check(PersonalityPolicy.WaitMultiplier(PetPersonality.Quiet) > PersonalityPolicy.WaitMultiplier(PetPersonality.Lively),
            "quiet personality waits longer");
        check(PersonalityPolicy.LookCooldown(PetPersonality.Clingy) < PersonalityPolicy.LookCooldown(PetPersonality.Default),
            "clingy pet observes pointer more often");
        var tired = 0;
        for (var i = 0; i < 100; i++) if (PersonalityPolicy.NextAction(random, PetPersonality.Default, true, true) == PetState.Sleeping) tired++;
        check(tired > 40, "low energy favors rest");
        var area = new WorkArea(-1000, 40, 1000, 800);
        var region = ActivityAreaPolicy.Resolve(legacy with { ActivityRange = ActivityRange.Custom,
            ActivityLeftPercent = 25, ActivityTopPercent = 50, ActivityWidthPercent = 50, ActivityHeightPercent = 25 },
            new(160, 160), area);
        check(region == new WorkArea(-750, 440, 500, 200), "custom region uses relative work area including negative origins");
        var small = ActivityAreaPolicy.Resolve(legacy with { ActivityRange = ActivityRange.Custom,
            ActivityLeftPercent = 90, ActivityTopPercent = 90, ActivityWidthPercent = 10, ActivityHeightPercent = 10 },
            new(320, 320), area);
        check(small.Right <= area.Right && small.Bottom <= area.Bottom && small.Width == 320 && small.Height == 320,
            "small corner region expands inwards for scaled pet");
        var bottom = ActivityAreaPolicy.Resolve(legacy with { ActivityRange = ActivityRange.Bottom }, new(160, 160), area);
        check(bottom.Left == -1000 && bottom.Bottom == 840 && bottom.Height == 200, "bottom strip stays in work area");
    }
}
