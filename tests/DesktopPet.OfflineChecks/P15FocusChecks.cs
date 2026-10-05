using System.Reflection;
using DesktopPet.Companions;
using DesktopPet.Models;

internal static class P15FocusChecks
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 9, 0, 0, TimeSpan.FromHours(8));
    private static readonly PropertyInfo? DurationProperty = typeof(FocusTimer).GetProperty("Duration");
    private static readonly PropertyInfo? EndProperty = typeof(FocusTimer).GetProperty("EstimatedEndUtc");
    private static readonly MethodInfo? ProgressMethod = typeof(FocusTimer).GetMethod("Progress", [typeof(DateTimeOffset)]);

    public static void Run(Action<bool, string> check)
    {
        // These probes compile against P14 and fail semantically when P15 details are absent.
        check(DurationProperty?.PropertyType == typeof(TimeSpan) && DurationProperty.GetMethod?.IsPublic == true,
            "P15 focus details expose the actual phase duration as TimeSpan");
        check(EndProperty?.PropertyType == typeof(DateTimeOffset?) && EndProperty.GetMethod?.IsPublic == true,
            "P15 focus details expose a nullable UTC end estimate");
        check(ProgressMethod?.ReturnType == typeof(double),
            "P15 focus details expose progress for an observed timestamp");
        if (DurationProperty is null || EndProperty is null || ProgressMethod is null) return;

        CheckPhaseEndpoints(check);
        CheckFocusPauseResume(check);
        CheckBreakPauseResume(check);
        CheckFullPauseEdges(check);
        CheckStoppedDetails(check);
        CheckRollback(check);
        CheckClampedDurations(check);
        CheckNextSessionPreferences(check);
    }

    private static void CheckPhaseEndpoints(Action<bool, string> check)
    {
        var timer = new FocusTimer();
        timer.Start(Now, 25, 5);
        check(timer.Phase == FocusPhase.Focus && Duration(timer) == TimeSpan.FromMinutes(25) &&
            End(timer) == Now.AddMinutes(25) && End(timer)?.Offset == TimeSpan.Zero && Close(Progress(timer, Now), 0),
            "P15 a 25 minute focus starts at zero progress with its UTC deadline");
        check(Close(Progress(timer, Now.AddMinutes(-1)), 0) && Close(Progress(timer, Now.AddMinutes(12.5)), .5),
            "P15 focus progress clamps before start and reaches half after 12.5 minutes");
        check(Close(Progress(timer, Now.AddMinutes(25)), 1) && timer.Phase == FocusPhase.Focus,
            "P15 focus reaches full progress without consuming its transition");
        check(timer.Tick(Now.AddMinutes(25)) == FocusPhase.Break &&
            timer.Tick(Now.AddMinutes(25)) is null && Duration(timer) == TimeSpan.FromMinutes(5) &&
            End(timer) == Now.AddMinutes(30) && End(timer)?.Offset == TimeSpan.Zero &&
            Close(Progress(timer, Now.AddMinutes(25)), 0),
            "P15 the single focus transition starts a separate five minute break at zero progress");
        check(Close(Progress(timer, Now.AddMinutes(27.5)), .5) && Close(Progress(timer, Now.AddMinutes(30)), 1),
            "P15 break progress uses its own five minute duration and endpoint");
        check(timer.Tick(Now.AddMinutes(30)) == FocusPhase.Completed &&
            timer.Tick(Now.AddMinutes(31)) is null && !timer.IsActive && Duration(timer) == TimeSpan.Zero &&
            End(timer) is null && Close(Progress(timer, Now.AddMinutes(40)), 1),
            "P15 completion reports full progress with no duration or end estimate and one transition");

        var late = new FocusTimer();
        late.Start(Now, 25, 5);
        check(Close(Progress(late, Now.AddMinutes(40)), 1) && late.Phase == FocusPhase.Focus &&
            Duration(late) == TimeSpan.FromMinutes(25) && End(late) == Now.AddMinutes(25),
            "P15 overdue phase details stay bounded without changing the actual duration or phase");
        check(late.Tick(Now.AddMinutes(40)) == FocusPhase.Break && End(late) == Now.AddMinutes(45) &&
            Close(Progress(late, Now.AddMinutes(40)), 0),
            "P15 a late focus tick preserves the existing full break starting at observed time");
    }

    private static void CheckFocusPauseResume(Action<bool, string> check)
    {
        var timer = new FocusTimer();
        timer.Start(Now, 25, 5);
        check(timer.Pause(Now.AddMinutes(10)) is null && timer.IsPaused &&
            Duration(timer) == TimeSpan.FromMinutes(25) && End(timer) is null &&
            timer.Remaining(Now.AddMinutes(40)) == TimeSpan.FromMinutes(15) &&
            Close(Progress(timer, Now.AddMinutes(10)), .4) && Close(Progress(timer, Now.AddMinutes(40)), .4),
            "P15 paused focus retains actual duration and progress while removing its stale deadline");
        check(timer.Pause(Now.AddMinutes(40)) is null && timer.Tick(Now.AddMinutes(40)) is null &&
            Close(Progress(timer, Now.AddMinutes(40)), .4),
            "P15 repeated pause and paused ticks cannot advance focus progress");
        timer.Resume(Now.AddMinutes(40));
        check(!timer.IsPaused && End(timer) == Now.AddMinutes(55) && End(timer)?.Offset == TimeSpan.Zero &&
            Duration(timer) == TimeSpan.FromMinutes(25) && Close(Progress(timer, Now.AddMinutes(40)), .4),
            "P15 resuming focus retains progress and exposes the recalculated UTC deadline");
        check(Close(Progress(timer, Now.AddMinutes(47.5)), .7) && Close(Progress(timer, Now.AddMinutes(55)), 1),
            "P15 resumed focus adds elapsed work using the original session duration");
    }

    private static void CheckBreakPauseResume(Action<bool, string> check)
    {
        var timer = new FocusTimer();
        timer.Start(Now, 25, 5);
        timer.Tick(Now.AddMinutes(25));
        check(timer.Pause(Now.AddMinutes(26)) is null && timer.IsPaused &&
            Duration(timer) == TimeSpan.FromMinutes(5) && End(timer) is null &&
            timer.Remaining(Now.AddMinutes(120)) == TimeSpan.FromMinutes(4) &&
            Close(Progress(timer, Now.AddMinutes(120)), .2),
            "P15 a paused break retains its own duration and progress without an end estimate");
        timer.Resume(Now.AddMinutes(120));
        check(End(timer) == Now.AddMinutes(124) && End(timer)?.Offset == TimeSpan.Zero &&
            Close(Progress(timer, Now.AddMinutes(120)), .2) && Close(Progress(timer, Now.AddMinutes(122)), .6),
            "P15 resuming a break shifts its deadline and continues its original progress");
    }

    private static void CheckFullPauseEdges(Action<bool, string> check)
    {
        var full = new FocusTimer();
        full.Start(Now, 25, 5);
        full.Pause(Now);
        check(full.IsPaused && End(full) is null && Duration(full) == TimeSpan.FromMinutes(25) &&
            Close(Progress(full, Now.AddDays(1)), 0) && full.Remaining(Now.AddDays(1)) == TimeSpan.FromMinutes(25),
            "P15 pausing immediately retains the full focus duration at zero progress");
        full.Resume(Now.AddMinutes(120));
        check(End(full) == Now.AddMinutes(145) && Close(Progress(full, Now.AddMinutes(120)), 0) &&
            Close(Progress(full, Now.AddMinutes(132.5)), .5),
            "P15 a full-duration pause resumes with a full focus deadline");

        var boundary = new FocusTimer();
        boundary.Start(Now, 25, 5);
        check(boundary.Pause(Now.AddMinutes(25)) == FocusPhase.Break && boundary.IsPaused &&
            boundary.Phase == FocusPhase.Break && Duration(boundary) == TimeSpan.FromMinutes(5) &&
            End(boundary) is null && Close(Progress(boundary, Now.AddMinutes(100)), 0),
            "P15 pausing at the focus boundary transitions once and pauses the full new break");
        boundary.Resume(Now.AddMinutes(100));
        check(End(boundary) == Now.AddMinutes(105) && Close(Progress(boundary, Now.AddMinutes(100)), 0),
            "P15 a break paused at its start resumes with its full duration");
        check(boundary.Pause(Now.AddMinutes(105)) == FocusPhase.Completed && !boundary.IsPaused &&
            Duration(boundary) == TimeSpan.Zero && End(boundary) is null &&
            Close(Progress(boundary, Now.AddMinutes(105)), 1),
            "P15 pausing at the final boundary keeps completed details and no paused state");
    }

    private static void CheckStoppedDetails(Action<bool, string> check)
    {
        var timer = new FocusTimer();
        check(timer.Phase == FocusPhase.Stopped && Duration(timer) == TimeSpan.Zero &&
            End(timer) is null && Close(Progress(timer, Now), 0),
            "P15 a new stopped timer has zero progress and no duration or estimate");
        timer.Start(Now, 25, 5);
        timer.Pause(Now.AddMinutes(10));
        timer.Stop();
        timer.Resume(Now.AddMinutes(20));
        check(timer.Phase == FocusPhase.Stopped && !timer.IsPaused && !timer.IsActive &&
            Duration(timer) == TimeSpan.Zero && End(timer) is null &&
            Close(Progress(timer, Now.AddMinutes(20)), 0) && timer.Tick(Now.AddMinutes(60)) is null,
            "P15 stopping a paused session clears visible progress and estimate without allowing resume");
    }

    private static void CheckRollback(Action<bool, string> check)
    {
        var timer = new FocusTimer();
        timer.Start(Now, 25, 5);
        check(Close(Progress(timer, Now.AddMinutes(10)), .4) &&
            timer.Remaining(Now.AddMinutes(5)) == TimeSpan.FromMinutes(15) &&
            Close(Progress(timer, Now.AddMinutes(5)), .4),
            "P15 progress participates in the existing shared observed-time rollback protection");
        timer.Pause(Now.AddMinutes(5));
        check(Close(Progress(timer, Now.AddMinutes(100)), .4) &&
            Duration(timer) == TimeSpan.FromMinutes(25) && End(timer) is null,
            "P15 a backward pause retains the last observed progress and original duration");
        timer.Resume(Now.AddMinutes(2));
        check(End(timer) == Now.AddMinutes(25) && Close(Progress(timer, Now.AddMinutes(5)), .4) &&
            timer.Tick(Now.AddMinutes(9)) is null,
            "P15 backward resume uses the prior observation without counting paused reads as elapsed work");
        check(timer.Remaining(Now.AddMinutes(20)) == TimeSpan.FromMinutes(5) &&
            Close(Progress(timer, Now.AddMinutes(15)), .8) && Duration(timer) == TimeSpan.FromMinutes(25),
            "P15 remaining-time observations protect later progress from clock rollback without shortening duration");
        timer.Tick(Now.AddMinutes(25));
        check(Close(Progress(timer, Now.AddMinutes(27.5)), .5) &&
            Close(Progress(timer, Now.AddMinutes(26)), .5) && Duration(timer) == TimeSpan.FromMinutes(5),
            "P15 break progress also retains its last observed fraction across rollback");
    }

    private static void CheckClampedDurations(Action<bool, string> check)
    {
        foreach (var (focus, rest, expectedFocus, expectedBreak) in new[]
        {
            (0, 0, 1, 1), (-20, -10, 1, 1), (121, 61, 120, 60),
            (int.MinValue, int.MinValue, 1, 1), (int.MaxValue, int.MaxValue, 120, 60)
        })
        {
            var timer = new FocusTimer();
            timer.Start(Now, focus, rest);
            check(Duration(timer) == TimeSpan.FromMinutes(expectedFocus) &&
                End(timer) == Now.AddMinutes(expectedFocus) &&
                Close(Progress(timer, Now.AddMinutes(expectedFocus / 2d)), .5),
                $"P15 requested focus {focus} uses its actual clamped {expectedFocus} minute duration");
            var breakStart = Now.AddMinutes(expectedFocus);
            check(timer.Tick(breakStart) == FocusPhase.Break &&
                Duration(timer) == TimeSpan.FromMinutes(expectedBreak) &&
                End(timer) == breakStart.AddMinutes(expectedBreak) &&
                Close(Progress(timer, breakStart.AddMinutes(expectedBreak / 2d)), .5),
                $"P15 requested break {rest} uses its actual clamped {expectedBreak} minute duration");
        }
    }

    private static void CheckNextSessionPreferences(Action<bool, string> check)
    {
        var preferences = new AppSettings { FocusMinutes = 25, BreakMinutes = 5 };
        var timer = new FocusTimer();
        timer.Start(Now, preferences.FocusMinutes, preferences.BreakMinutes);
        preferences.FocusMinutes = 45;
        preferences.BreakMinutes = 10;
        check(Duration(timer) == TimeSpan.FromMinutes(25) && End(timer) == Now.AddMinutes(25) &&
            Close(Progress(timer, Now.AddMinutes(12.5)), .5),
            "P15 editing next-session preferences during focus preserves the actual current duration and deadline");
        timer.Pause(Now.AddMinutes(12.5));
        preferences.FocusMinutes = 60;
        check(Duration(timer) == TimeSpan.FromMinutes(25) && End(timer) is null &&
            Close(Progress(timer, Now.AddMinutes(50)), .5),
            "P15 editing next-session preferences during pause preserves current progress");
        timer.Resume(Now.AddMinutes(50));
        check(End(timer) == Now.AddMinutes(62.5) && timer.Tick(Now.AddMinutes(62.5)) == FocusPhase.Break &&
            Duration(timer) == TimeSpan.FromMinutes(5) && End(timer) == Now.AddMinutes(67.5),
            "P15 the current session keeps its originally captured break duration after preference edits");
        timer.Stop();
        timer.Start(Now.AddMinutes(100), preferences.FocusMinutes, preferences.BreakMinutes);
        check(Duration(timer) == TimeSpan.FromMinutes(60) && End(timer) == Now.AddMinutes(160) &&
            Close(Progress(timer, Now.AddMinutes(130)), .5),
            "P15 starting the next session reads the newly selected focus duration");
        check(timer.Tick(Now.AddMinutes(160)) == FocusPhase.Break && Duration(timer) == TimeSpan.FromMinutes(10) &&
            End(timer) == Now.AddMinutes(170) && Close(Progress(timer, Now.AddMinutes(165)), .5),
            "P15 the next session uses the newly selected break duration independently");
    }

    private static TimeSpan Duration(FocusTimer timer) => (TimeSpan)DurationProperty!.GetValue(timer)!;
    private static DateTimeOffset? End(FocusTimer timer) => EndProperty!.GetValue(timer) as DateTimeOffset?;
    private static double Progress(FocusTimer timer, DateTimeOffset now) => (double)ProgressMethod!.Invoke(timer, [now])!;
    private static bool Close(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) < .000000001;
}
