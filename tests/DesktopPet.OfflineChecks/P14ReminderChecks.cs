using System.Reflection;
using System.Text.Json;
using DesktopPet.Companions;
using DesktopPet.Models;

internal static class P14ReminderChecks
{
    public static void Run(Action<bool, string> check)
    {
        CheckSettings(check);
        CheckSnoozeDurations(check);
        CheckPendingAndFocusDelivery(check);
        CheckSnoozeConfiguration(check);
        CheckQuietHealthSuppression(check);
        CheckEarlyResume(check);
        CheckExpiryAndRestart(check);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.FromHours(8));
    private static readonly AppSettings WaterOnly = new() { WaterReminderEnabled = true, WaterReminderMinutes = 15 };
    private static readonly AppSettings AllHealth = new()
    {
        WaterReminderEnabled = true, WaterReminderMinutes = 15,
        MoveReminderEnabled = true, MoveReminderMinutes = 15,
        RestReminderEnabled = true, RestReminderMinutes = 15,
    };

    private static void CheckSettings(Action<bool, string> check)
    {
        var legacy = JsonSerializer.Deserialize<AppSettings>("{\"PetName\":\"小狗\"}")!;
        check(!QuietEnabled(legacy) && legacy.AutoBehaviorEnabled && legacy.NightSleepEnabled &&
            !legacy.WaterReminderEnabled && legacy.WaterReminderMinutes == 45,
            "P14 legacy preferences retain normal behavior and health reminder defaults");
        var enabled = JsonSerializer.Deserialize<AppSettings>("{\"FocusQuietEnabled\":true,\"PetName\":\"  小狗  \"}")!.Normalize();
        check(QuietEnabled(enabled) && enabled.PetName == "小狗",
            "P14 opting into focus quiet persists through settings normalization and JSON");
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(enabled))!;
        check(QuietEnabled(roundTrip), "P14 focus quiet survives settings round trip");
    }

    private static void CheckSnoozeDurations(Action<bool, string> check)
    {
        foreach (var minutes in new[] { 15, 30, 60 })
        {
            var scheduler = new ReminderScheduler();
            scheduler.Configure(WaterOnly, Now);
            Snooze(scheduler, Now, minutes);
            var until = SnoozedUntil(scheduler);
            check(until == Now.AddMinutes(minutes) && until?.Offset == TimeSpan.Zero,
                $"P14 {minutes} minute health snooze exposes its UTC resume time");
            check(Tick(scheduler, Now.AddMinutes(minutes).AddSeconds(-1), true) is null,
                $"P14 {minutes} minute snooze suppresses health until its deadline");
            var notice = Tick(scheduler, Now.AddMinutes(minutes), true);
            check(notice?.Kind == ReminderKind.Water && notice.CreatedUtc == Now.AddMinutes(minutes) && SnoozedUntil(scheduler) is null,
                $"P14 {minutes} minute snooze expires and resumes one coalesced health reminder");
        }

        var invalid = new ReminderScheduler();
        check(ThrowsInvalidSnooze(invalid, 0) && ThrowsInvalidSnooze(invalid, -15) && ThrowsInvalidSnooze(invalid, 45),
            "P14 health snooze rejects unsupported durations");
        check(SnoozedUntil(invalid) is null, "P14 rejected snooze does not create session state");
    }

    private static void CheckPendingAndFocusDelivery(Action<bool, string> check)
    {
        var scheduler = new ReminderScheduler();
        scheduler.Configure(AllHealth, Now);
        Tick(scheduler, Now.AddMinutes(15), false);
        scheduler.Enqueue(ReminderKind.FocusFinished, Now.AddMinutes(15));
        Snooze(scheduler, Now.AddMinutes(15), 30);
        check(scheduler.PendingCount == 1 && Tick(scheduler, Now.AddMinutes(15), true)?.Kind == ReminderKind.FocusFinished,
            "P14 snooze clears all pending health notices and keeps the focus transition");
        scheduler.Enqueue(ReminderKind.BreakFinished, Now.AddMinutes(16));
        check(Tick(scheduler, Now.AddMinutes(16), true) is null &&
            Tick(scheduler, Now.AddMinutes(17), true)?.Kind == ReminderKind.BreakFinished,
            "P14 snoozed health leaves focus and break notices subject to the existing two minute spacing");
        check(Tick(scheduler, Now.AddMinutes(44), true) is null,
            "P14 snoozed health notices do not reappear after focus delivery");
        check(Tick(scheduler, Now.AddMinutes(45), true)?.Kind == ReminderKind.Water &&
            Tick(scheduler, Now.AddMinutes(46), true) is null &&
            Tick(scheduler, Now.AddMinutes(47), true)?.Kind == ReminderKind.Move &&
            Tick(scheduler, Now.AddMinutes(49), true)?.Kind == ReminderKind.Rest,
            "P14 health delivery after snooze preserves ordering and two minute spacing");

        var focus = new FocusTimer();
        var transitions = new ReminderScheduler();
        focus.Start(Now, 1, 1);
        Snooze(transitions, Now, 60);
        check(focus.Tick(Now.AddMinutes(1)) == FocusPhase.Break,
            "P14 health snooze does not delay the focus to break transition");
        transitions.Enqueue(ReminderKind.FocusFinished, Now.AddMinutes(1));
        check(Tick(transitions, Now.AddMinutes(1), true)?.Kind == ReminderKind.FocusFinished,
            "P14 focus completion notice passes during a health snooze");
        check(focus.Tick(Now.AddMinutes(2)) == FocusPhase.Completed,
            "P14 health snooze does not delay break completion");
        transitions.Enqueue(ReminderKind.BreakFinished, Now.AddMinutes(2));
        check(Tick(transitions, Now.AddMinutes(3), true)?.Kind == ReminderKind.BreakFinished,
            "P14 break completion notice passes during a health snooze");
    }

    private static void CheckSnoozeConfiguration(Action<bool, string> check)
    {
        var scheduler = new ReminderScheduler();
        var longer = WaterOnly with { WaterReminderMinutes = 60 };
        scheduler.Configure(longer, Now);
        Snooze(scheduler, Now.AddMinutes(5), 15);
        scheduler.Configure(longer with { PetName = "伙伴", Topmost = false, BallScale = 1.3 }, Now.AddMinutes(10));
        check(SnoozedUntil(scheduler) == Now.AddMinutes(20),
            "P14 unrelated preference edits preserve the session health snooze");
        check(Tick(scheduler, Now.AddMinutes(20), true) is null &&
            Tick(scheduler, Now.AddMinutes(59), true) is null &&
            Tick(scheduler, Now.AddMinutes(60), true)?.Kind == ReminderKind.Water,
            "P14 snoozing never pulls a later health reminder forward");

        var enabledDuringSnooze = new ReminderScheduler();
        enabledDuringSnooze.Configure(new AppSettings(), Now);
        Snooze(enabledDuringSnooze, Now, 60);
        enabledDuringSnooze.Configure(WaterOnly, Now.AddMinutes(5));
        check(Tick(enabledDuringSnooze, Now.AddMinutes(20), true) is null &&
            Tick(enabledDuringSnooze, Now.AddMinutes(60), true)?.Kind == ReminderKind.Water,
            "P14 newly enabled health reminders respect an active snooze");

        var changedDuringSnooze = new ReminderScheduler();
        changedDuringSnooze.Configure(longer, Now);
        Snooze(changedDuringSnooze, Now, 60);
        changedDuringSnooze.Configure(WaterOnly, Now.AddMinutes(5));
        check(Tick(changedDuringSnooze, Now.AddMinutes(20), true) is null &&
            Tick(changedDuringSnooze, Now.AddMinutes(60), true)?.Kind == ReminderKind.Water,
            "P14 health interval changes respect an active snooze");
    }

    private static void CheckQuietHealthSuppression(Action<bool, string> check)
    {
        var scheduler = new ReminderScheduler();
        scheduler.Configure(AllHealth, Now);
        Tick(scheduler, Now.AddMinutes(15), false);
        check(Tick(scheduler, Now.AddMinutes(15), true, false) is null && scheduler.PendingCount == 0,
            "P14 focus quiet discards health notices already pending");
        scheduler.Enqueue(ReminderKind.FocusFinished, Now.AddMinutes(16));
        check(Tick(scheduler, Now.AddMinutes(16), true, false)?.Kind == ReminderKind.FocusFinished,
            "P14 focus quiet allows focus transition notices");
        scheduler.Enqueue(ReminderKind.BreakFinished, Now.AddMinutes(17));
        check(Tick(scheduler, Now.AddMinutes(17), true, false) is null &&
            Tick(scheduler, Now.AddMinutes(18), true, false)?.Kind == ReminderKind.BreakFinished,
            "P14 focus quiet preserves focus notice spacing");
        check(Tick(scheduler, Now.AddMinutes(30), true, false) is null && scheduler.PendingCount == 0 &&
            Tick(scheduler, Now.AddMinutes(31), true) is null,
            "P14 focus quiet advances health intervals without accumulating a burst on exit");
        scheduler.Enqueue(ReminderKind.FocusFinished, Now.AddMinutes(45));
        check(Tick(scheduler, Now.AddMinutes(45), true)?.Kind == ReminderKind.FocusFinished &&
            Tick(scheduler, Now.AddMinutes(47), true)?.Kind == ReminderKind.Water,
            "P14 normal delivery after focus quiet retains focus priority over health reminders");

        var later = new ReminderScheduler();
        later.Configure(WaterOnly with { WaterReminderMinutes = 60 }, Now);
        check(Tick(later, Now.AddMinutes(15), true, false) is null &&
            Tick(later, Now.AddMinutes(30), true) is null &&
            Tick(later, Now.AddMinutes(60), true)?.Kind == ReminderKind.Water,
            "P14 focus quiet does not pull a later health deadline forward");
    }

    private static void CheckEarlyResume(Action<bool, string> check)
    {
        var scheduler = new ReminderScheduler();
        scheduler.Configure(WaterOnly, Now);
        Snooze(scheduler, Now, 60);
        scheduler.Enqueue(ReminderKind.Water, Now.AddMinutes(5));
        scheduler.Enqueue(ReminderKind.FocusFinished, Now.AddMinutes(5));
        Resume(scheduler, Now.AddMinutes(5));
        check(SnoozedUntil(scheduler) is null && scheduler.PendingCount == 1 &&
            Tick(scheduler, Now.AddMinutes(5), true)?.Kind == ReminderKind.FocusFinished,
            "P14 early health resume clears its stale health notice and retains focus notices");
        check(Tick(scheduler, Now.AddMinutes(7), true) is null &&
            Tick(scheduler, Now.AddMinutes(19), true) is null &&
            Tick(scheduler, Now.AddMinutes(20), true)?.Kind == ReminderKind.Water,
            "P14 early health resume restarts the normal interval without an immediate burst");
    }

    private static void CheckExpiryAndRestart(Action<bool, string> check)
    {
        var focusExpiry = new ReminderScheduler();
        focusExpiry.Enqueue(ReminderKind.FocusFinished, Now);
        Snooze(focusExpiry, Now, 60);
        check(Tick(focusExpiry, Now.AddMinutes(10), true) is null && focusExpiry.PendingCount == 0,
            "P14 snooze retains the existing ten minute focus notice expiry");

        var healthExpiry = new ReminderScheduler();
        healthExpiry.Configure(WaterOnly, Now);
        Snooze(healthExpiry, Now, 15);
        check(Tick(healthExpiry, Now.AddMinutes(25), true) is null && healthExpiry.PendingCount == 0 &&
            SnoozedUntil(healthExpiry) is null && Tick(healthExpiry, Now.AddMinutes(40), true)?.Kind == ReminderKind.Water,
            "P14 missed snooze deadlines retain notice expiry and coalesced scheduling");

        var settingsExpiry = new ReminderScheduler();
        settingsExpiry.Configure(WaterOnly, Now);
        Snooze(settingsExpiry, Now, 15);
        settingsExpiry.Configure(WaterOnly, Now.AddMinutes(15));
        check(SnoozedUntil(settingsExpiry) is null,
            "P14 updating settings after a snooze deadline clears its expired status");
        check(SnoozedUntil(new ReminderScheduler()) is null,
            "P14 a new session does not retain the previous health snooze");
    }

    // Compatible probes let these behavior checks compile before the P14 APIs exist.
    private static bool QuietEnabled(AppSettings settings)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(settings));
        return json.RootElement.TryGetProperty("FocusQuietEnabled", out var value) && value.GetBoolean();
    }

    private static DateTimeOffset? SnoozedUntil(ReminderScheduler scheduler) =>
        typeof(ReminderScheduler).GetProperty("HealthSnoozedUntil")?.GetValue(scheduler) as DateTimeOffset?;

    private static void Snooze(ReminderScheduler scheduler, DateTimeOffset now, int minutes) =>
        typeof(ReminderScheduler).GetMethod("SnoozeHealth", [typeof(DateTimeOffset), typeof(int)])?.Invoke(scheduler, [now, minutes]);

    private static void Resume(ReminderScheduler scheduler, DateTimeOffset now) =>
        typeof(ReminderScheduler).GetMethod("ResumeHealth", [typeof(DateTimeOffset)])?.Invoke(scheduler, [now]);

    private static ReminderNotice? Tick(ReminderScheduler scheduler, DateTimeOffset now, bool canDisplay, bool healthAllowed = true)
    {
        var method = typeof(ReminderScheduler).GetMethod("Tick", [typeof(DateTimeOffset), typeof(bool), typeof(bool)]);
        return method is null ? scheduler.Tick(now, canDisplay) : method.Invoke(scheduler, [now, canDisplay, healthAllowed]) as ReminderNotice;
    }

    private static bool ThrowsInvalidSnooze(ReminderScheduler scheduler, int minutes)
    {
        try { Snooze(scheduler, Now, minutes); }
        catch (TargetInvocationException error) when (error.InnerException is ArgumentOutOfRangeException) { return true; }
        return false;
    }
}
