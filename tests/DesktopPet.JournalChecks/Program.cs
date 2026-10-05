using DesktopPet.Journal;
using DesktopPet.Models;
using System.Text.Json;

internal static class Program
{
    private static int _checks;
    private static int _failures;
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 9, 0, 0, TimeSpan.FromHours(8));

    private static int Main()
    {
        Run("legacy and growth", LegacyAndGrowth);
        Run("disabled recording", DisabledRecording);
        Run("local date visits", LocalDateVisits);
        Run("independent cooldowns", Cooldowns);
        Run("milestone thresholds", Milestones);
        Run("invalid action", InvalidAction);
        Run("bounded chronological diary", BoundedDiary);
        Run("state normalization", StateNormalization);
        Run("future state and backward clock", ClockChanges);
        Run("full visit capacity", FullVisitCapacity);
        Run("profile JSON round trip", JsonRoundTrip);
        Run("malformed nested JSON", MalformedJson);
        Console.WriteLine($"Journal checks: {_checks - _failures}/{_checks} passed; {_failures} failed.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Run(string name, Action body)
    {
        try { body(); }
        catch (Exception error) { Check(false, $"{name} threw {error.GetType().Name}: {error.Message}"); }
    }

    private static void Check(bool condition, string name)
    {
        _checks++;
        if (!condition) _failures++;
        Console.WriteLine($"{(condition ? "PASS" : "FAIL")}: {name}");
    }

    private static void LegacyAndGrowth()
    {
        var old = JsonSerializer.Deserialize<PetProfile>("{\"Affection\":38,\"Fullness\":65,\"Energy\":82}")!.Normalize();
        Check(old.Journal is not null && old.Journal.Entries.Count == 0, "old profiles initialize empty persisted journal");
        var tracker = new JournalTracker(old, true, Now);
        Check(tracker.Profile.Journal.VisitedDays.Count == 1 && tracker.Entries.Count == 1, "enabled constructor records the current companionship day once");
        tracker.Record(JournalAction.Petting, Now);
        tracker.Record(JournalAction.Feeding, Now);
        tracker.Record(JournalAction.FocusCompleted, Now);
        Check(tracker.Profile.Affection == 38 && tracker.Profile.Fullness == 65 && tracker.Profile.Energy == 82,
            "journal interaction recording leaves growth values unchanged");
        Check(tracker.Profile.UpdatedUtc == old.UpdatedUtc && tracker.Profile.LastBondUtc == old.LastBondUtc,
            "journal recording leaves care timestamps unchanged");
        var growthOff = new AppSettings { GrowthEnabled = false, JournalEnabled = true };
        var independent = new JournalTracker(new PetProfile(), growthOff.JournalEnabled, Now);
        independent.Record(JournalAction.Feeding, Now);
        Check(independent.Profile.Journal.FeedingCount == 1, "journal remains active when growth setting is disabled");
        var normalized = new PetProfile { Affection = double.NaN, Fullness = -10, Energy = 500, Journal = null! }.Normalize();
        Check(normalized.Affection == 20 && normalized.Fullness == 0 && normalized.Energy == 100 && normalized.Journal is not null,
            "profile normalization preserves legacy care clamping and repairs null journal");
    }

    private static void DisabledRecording()
    {
        var tracker = new JournalTracker(new PetProfile(), false, Now);
        tracker.Visit(Now.AddDays(1));
        foreach (var action in Enum.GetValues<JournalAction>()) tracker.Record(action, Now.AddDays(2));
        tracker.UpdateSettings(false, Now.AddDays(3));
        Check(tracker.Profile.Journal.VisitedDays.Count == 0 && tracker.Entries.Count == 0,
            "disabled journal freezes visits and all action counters");
        tracker.UpdateSettings(true, Now.AddDays(5));
        Check(tracker.Profile.Journal.VisitedDays.SequenceEqual(new[] { new DateOnly(2026, 10, 8) }),
            "re-enabling records today without filling offline days");
        tracker.Record(JournalAction.Home, Now.AddDays(5));
        tracker.UpdateSettings(false, Now.AddDays(5));
        var saved = JsonSerializer.Serialize(tracker.Profile);
        tracker.Record(JournalAction.Feeding, Now.AddDays(6));
        tracker.Visit(Now.AddDays(6));
        Check(JsonSerializer.Serialize(tracker.Profile) == saved, "turning journal off preserves existing progress without new entries");
        Check(tracker.UnlockedDialogue("first-home") is not null, "disabled journal retains already unlocked reward dialogue");
    }

    private static void LocalDateVisits()
    {
        var late = new DateTimeOffset(2026, 10, 3, 23, 55, 0, TimeSpan.FromHours(8));
        var tracker = new JournalTracker(new PetProfile(), true, late);
        for (var i = 0; i < 300; i++) tracker.Visit(late.AddMilliseconds(i));
        Check(tracker.Entries.Count == 1 && tracker.Profile.Journal.VisitedDays.Count == 1,
            "frequent Visit calls create one entry per local date");
        tracker.Visit(late.AddMinutes(10));
        Check(tracker.Profile.Journal.VisitedDays.SequenceEqual(new[] { new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 4) }),
            "companionship days follow local midnight rather than UTC midnight");
        tracker.Visit(late.AddMinutes(10).ToOffset(TimeSpan.FromHours(-8)));
        Check(tracker.Profile.Journal.VisitedDays.Count == 2, "offset changes do not duplicate a previously visited local date");
        tracker.Visit(late.AddDays(9));
        Check(tracker.Profile.Journal.VisitedDays.Count == 3, "companionship counts actual visited dates without enforcing a streak");
        var restored = new JournalTracker(tracker.Profile, true, late.AddDays(9));
        Check(restored.Profile.Journal.VisitedDays.Count == 3 && restored.Entries.Count == tracker.Entries.Count,
            "restoring on the same local date does not duplicate its visit or milestone");
    }

    private static int Count(JournalState state, JournalAction action) => action switch
    {
        JournalAction.Petting => state.PettingCount,
        JournalAction.Feeding => state.FeedingCount,
        JournalAction.Play => state.PlayCount,
        JournalAction.Home => state.HomeCount,
        JournalAction.FocusCompleted => state.FocusCompletedCount,
        _ => -1
    };

    private static void Cooldowns()
    {
        foreach (var action in Enum.GetValues<JournalAction>())
        {
            var tracker = new JournalTracker(new PetProfile(), true, Now);
            tracker.Record(action, Now);
            for (var i = 0; i < 500; i++) tracker.Record(action, Now.AddMilliseconds(i));
            tracker.Record(action, Now.AddSeconds(9));
            Check(Count(tracker.Profile.Journal, action) == 1, $"{action} rapid repeated clicks are throttled");
            tracker.Record(action, Now.AddSeconds(10));
            Check(Count(tracker.Profile.Journal, action) == 2, $"{action} accepts a new event at the cooldown boundary");
            var restored = new JournalTracker(tracker.Profile, true, Now.AddSeconds(11));
            restored.Record(action, Now.AddSeconds(11));
            Check(Count(restored.Profile.Journal, action) == 2, $"{action} cooldown survives profile reload");
        }
        var independent = new JournalTracker(new PetProfile(), true, Now);
        foreach (var action in Enum.GetValues<JournalAction>()) independent.Record(action, Now);
        Check(Enum.GetValues<JournalAction>().All(action => Count(independent.Profile.Journal, action) == 1),
            "each action has an independent cooldown");
        Check(independent.Profile.Journal.FocusCompletedCount == 1, "focus completion is not suppressed by unrelated interactions");
    }

    private static void Milestones()
    {
        var tracker = new JournalTracker(new PetProfile(), true, Now);
        Check(tracker.Achievements.Count == 6 && tracker.Achievements.All(a => !a.Unlocked), "six milestone views start locked");
        Check(tracker.Achievements.Count == 6 && tracker.Achievements.All(a => !string.IsNullOrWhiteSpace(a.Title) && !string.IsNullOrWhiteSpace(a.Description)
            && !string.IsNullOrWhiteSpace(a.Status) && !string.IsNullOrWhiteSpace(a.RewardDialogue)),
            "milestone views include titles, progress, descriptions and reward dialogue");
        Check(tracker.UnlockedDialogue("first-interaction") is null && tracker.UnlockedDialogue("unknown") is null,
            "locked and unknown milestones do not expose reward dialogue");
        tracker.Record(JournalAction.FocusCompleted, Now);
        Check(tracker.UnlockedDialogue("first-interaction") is null, "focus progress is separate from the first pet interaction");
        tracker.Record(JournalAction.Petting, Now);
        Check(tracker.UnlockedDialogue("first-interaction") is not null, "first pet interaction unlocks its dialogue");
        tracker.Visit(Now.AddDays(1));
        Check(tracker.UnlockedDialogue("three-days") is null, "two visited dates keep the companionship milestone locked");
        tracker.Visit(Now.AddDays(2));
        Check(tracker.UnlockedDialogue("three-days") is not null, "three visited dates unlock the companionship dialogue");
        for (var i = 0; i < 9; i++) tracker.Record(JournalAction.Feeding, Now.AddMinutes(i));
        Check(tracker.UnlockedDialogue("ten-feeds") is null && tracker.Achievements.Single(a => a.Id == "ten-feeds").Current == 9,
            "nine feeds show real progress without unlocking the ten-feed reward");
        tracker.Record(JournalAction.Feeding, Now.AddMinutes(9));
        Check(tracker.UnlockedDialogue("ten-feeds") is not null, "ten feeds unlock the feeding dialogue");
        for (var i = 0; i < 4; i++) tracker.Record(JournalAction.Play, Now.AddMinutes(i));
        Check(tracker.UnlockedDialogue("five-plays") is null, "four plays keep the play milestone locked");
        tracker.Record(JournalAction.Play, Now.AddMinutes(4));
        Check(tracker.UnlockedDialogue("five-plays") is not null, "five plays unlock the play dialogue");
        tracker.Record(JournalAction.Home, Now);
        Check(tracker.UnlockedDialogue("first-home") is not null, "first return home unlocks its dialogue");
        tracker.Record(JournalAction.FocusCompleted, Now.AddMinutes(1));
        Check(tracker.UnlockedDialogue("three-focus") is null, "two focus completions keep the focus milestone locked");
        tracker.Record(JournalAction.FocusCompleted, Now.AddMinutes(2));
        Check(tracker.Achievements.All(a => a.Unlocked), "three focus completions complete the six milestones");
        Check(tracker.Entries.Count(e => e.Kind == JournalEntryKind.Achievement) == 6, "each milestone adds one unlock diary entry");
        tracker.Record(JournalAction.Feeding, Now.AddMinutes(10));
        tracker.Visit(Now.AddDays(3));
        Check(tracker.Entries.Count(e => e.Kind == JournalEntryKind.Achievement) == 6, "later progress does not repeat milestone unlock entries");
    }

    private static void InvalidAction()
    {
        var tracker = new JournalTracker(new PetProfile(), true, Now);
        var before = JsonSerializer.Serialize(tracker.Profile);
        tracker.Record((JournalAction)999, Now.AddDays(1));
        Check(JsonSerializer.Serialize(tracker.Profile) == before, "undefined actions cannot mutate visits, counters or diary");
    }

    private static void BoundedDiary()
    {
        var tracker = new JournalTracker(new PetProfile(), true, Now);
        for (var i = 0; i < 300; i++) tracker.Record(JournalAction.Petting, Now.AddSeconds(i * 10));
        Check(tracker.Entries.Count == 200 && tracker.Profile.Journal.PettingCount == 300,
            "diary retains the most recent 200 entries without truncating lifetime progress");
        Check(tracker.Entries.Count == 200 && tracker.Entries.First().AtUtc == Now.AddSeconds(1000).ToUniversalTime()
            && tracker.Entries.Last().AtUtc == Now.AddSeconds(2990).ToUniversalTime(), "bounded diary evicts the oldest entries");
        Check(tracker.Entries.Count == 200 && tracker.Entries.Zip(tracker.Entries.Skip(1)).All(pair => pair.First.AtUtc <= pair.Second.AtUtc),
            "diary entries remain chronological");
        Check(tracker.Entries.Count > 0 && tracker.Entries.All(e => e.LocalDate == new DateOnly(2026, 10, 3) && e.Text.Length > 0 && e.AtUtc.Offset == TimeSpan.Zero),
            "recent entry views expose local dates, readable text and UTC timestamps");
        var snapshot = tracker.Entries;
        tracker.Record(JournalAction.Petting, Now.AddSeconds(3000));
        Check(snapshot.Count > 0 && snapshot.Last().AtUtc == Now.AddSeconds(2990).ToUniversalTime(), "entry views are stable immutable snapshots");
    }

    private static void StateNormalization()
    {
        var entries = Enumerable.Range(0, 300).Select(i => new JournalEntry(Now.AddSeconds(i), new DateOnly(2026, 10, 3),
            JournalEntryKind.Petting, "摸头")).Reverse().ToList();
        entries.Add(null!);
        entries.Add(new JournalEntry(Now, default, JournalEntryKind.Visit, "bad date"));
        entries.Add(new JournalEntry(default, new DateOnly(2026, 10, 3), JournalEntryKind.Visit, "bad timestamp"));
        entries.Add(new JournalEntry(Now, new DateOnly(2026, 10, 3), (JournalEntryKind)999, "bad enum"));
        var state = new JournalState
        {
            PettingCount = -1, FeedingCount = int.MaxValue, PlayCount = -999,
            HomeCount = int.MaxValue, FocusCompletedCount = -50,
            VisitedDays = new[] { default(DateOnly), new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 3) },
            Entries = entries,
            LastActions = new[] { null!, new JournalActionStamp((JournalAction)999, Now), new JournalActionStamp(JournalAction.Feeding, default),
                new JournalActionStamp(JournalAction.Petting, Now), new JournalActionStamp(JournalAction.Petting, Now.AddSeconds(1)) }
        }.Normalize();
        Check(state.PettingCount == 0 && state.FeedingCount == 1_000_000 && state.PlayCount == 0
            && state.HomeCount == 1_000_000 && state.FocusCompletedCount == 0, "negative and oversized counters normalize to finite bounds");
        Check(state.VisitedDays.SequenceEqual(new[] { new DateOnly(2026, 10, 3), new DateOnly(2026, 10, 4) }),
            "visited dates normalize to valid sorted unique dates");
        Check(state.Entries.Count == 200 && state.Entries.First().AtUtc == Now.AddSeconds(100).ToUniversalTime(),
            "oversized unordered diary normalizes to the most recent valid 200 entries");
        Check(state.LastActions.Count == 1 && state.LastActions[0].AtUtc == Now.AddSeconds(1).ToUniversalTime(),
            "cooldown stamps reject malformed enums/dates and merge duplicate actions");
        var nulls = new JournalState { Entries = null!, VisitedDays = null!, LastActions = null! }.Normalize();
        Check(nulls.Entries.Count == 0 && nulls.VisitedDays.Count == 0 && nulls.LastActions.Count == 0,
            "null nested journal collections normalize without throwing");
        var text = new JournalState { Entries = new[] { new JournalEntry(Now, new DateOnly(2026, 10, 3),
            JournalEntryKind.Petting, new string('x', 10000) + "\0\r\n") } }.Normalize();
        Check(text.Entries[0].Text.Length <= 160 && !text.Entries[0].Text.Any(char.IsControl),
            "entry text is bounded and strips control characters");
        var dates = new JournalState { VisitedDays = Enumerable.Range(0, 40000).Select(i => new DateOnly(1970, 1, 1).AddDays(i)).ToArray() }.Normalize();
        Check(dates.VisitedDays.Count == 36600, "oversized visited date history has a finite storage bound");
        Check(state.Entries is not IList<JournalEntry> entryList || entryList.IsReadOnly,
            "normalized persisted entry collection prevents external mutation");
    }

    private static void ClockChanges()
    {
        var future = new PetProfile { Journal = new JournalState
        {
            VisitedDays = new[] { new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 9) },
            Entries = new[] { new JournalEntry(Now.AddDays(-1), new DateOnly(2026, 10, 2), JournalEntryKind.Visit, "past"),
                new JournalEntry(Now.AddDays(6), new DateOnly(2026, 10, 9), JournalEntryKind.Visit, "future") },
            LastActions = new[] { new JournalActionStamp(JournalAction.Petting, Now.AddDays(6)) }
        } };
        var tracker = new JournalTracker(future, true, Now);
        tracker.Record(JournalAction.Petting, Now);
        Check(tracker.Profile.Journal.PettingCount == 1, "future persisted cooldown does not freeze action recording");
        Check(tracker.Profile.Journal.VisitedDays.SequenceEqual(new[] { new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 3) })
            && tracker.Entries.All(e => e.AtUtc <= Now), "future persisted visits and entries are removed before recording today");
        tracker.Record(JournalAction.Petting, Now.AddMinutes(2));
        tracker.Record(JournalAction.Petting, Now.AddMinutes(1));
        tracker.Record(JournalAction.Petting, Now.AddMinutes(1));
        Check(tracker.Profile.Journal.PettingCount == 3, "backward clock correction rebases cooldown once and still throttles repeats");
        tracker.Record(JournalAction.Petting, Now.AddMinutes(1).AddSeconds(10));
        Check(tracker.Profile.Journal.PettingCount == 4, "actions resume after cooldown on a corrected backward clock");
        Check(tracker.Entries.Zip(tracker.Entries.Skip(1)).All(pair => pair.First.AtUtc <= pair.Second.AtUtc),
            "backward clock corrections retain chronological diary ordering");
        var reversing = new JournalTracker(new PetProfile(), true, Now);
        reversing.Record(JournalAction.Petting, Now.AddMinutes(2));
        for (var i = 0; i < 300; i++) reversing.Record(JournalAction.Petting, Now.AddMinutes(1).AddMilliseconds(-i));
        Check(reversing.Profile.Journal.PettingCount == 2, "continuously reversing clock cannot farm repeated interaction events");
        reversing.Record(JournalAction.Petting, Now.AddMinutes(1).AddSeconds(10));
        Check(reversing.Profile.Journal.PettingCount == 3, "a corrected reversing clock resumes after the normal cooldown");
    }

    private static void FullVisitCapacity()
    {
        var profile = new PetProfile { Journal = new JournalState
        {
            VisitedDays = Enumerable.Range(0, 36600).Select(i => new DateOnly(1970, 1, 2).AddDays(i)).ToArray()
        } };
        var tracker = new JournalTracker(profile, true, new DateTimeOffset(2085, 1, 1, 9, 0, 0, TimeSpan.Zero));
        var earlier = new DateTimeOffset(1970, 1, 1, 9, 0, 0, TimeSpan.Zero);
        tracker.Visit(earlier);
        for (var i = 0; i < 300; i++) tracker.Visit(earlier.AddMilliseconds(i));
        Check(tracker.Profile.Journal.VisitedDays.Count == 36600
            && tracker.Profile.Journal.VisitedDays.Contains(new DateOnly(1970, 1, 1)),
            "a visit at full date capacity retains its current date for persistent deduplication");
        Check(tracker.Entries.Count(entry => entry.Kind == JournalEntryKind.Visit && entry.LocalDate == new DateOnly(1970, 1, 1)) == 1,
            "full date capacity and a backward clock still create one visit entry per local date");
    }

    private static void JsonRoundTrip()
    {
        var tracker = new JournalTracker(new PetProfile(), true, Now);
        tracker.Record(JournalAction.Home, Now);
        tracker.Record(JournalAction.FocusCompleted, Now);
        var json = JsonSerializer.Serialize(tracker.Profile);
        var roundTrip = JsonSerializer.Deserialize<PetProfile>(json)!.Normalize();
        var restored = new JournalTracker(roundTrip, true, Now.AddSeconds(1));
        Check(restored.Profile.Journal.HomeCount == 1 && restored.Profile.Journal.FocusCompletedCount == 1
            && restored.Entries.Count == tracker.Entries.Count, "profile JSON round trip preserves counters and diary without duplicate visits");
        Check(restored.UnlockedDialogue("first-home") is not null && restored.UnlockedDialogue("first-home") == tracker.UnlockedDialogue("first-home"),
            "reward dialogue survives profile serialization");
        restored.Record(JournalAction.Home, Now.AddSeconds(1));
        Check(restored.Profile.Journal.HomeCount == 1, "serialized cooldowns protect against rapid restart click farming");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };
        var camel = JsonSerializer.Deserialize<PetProfile>(JsonSerializer.Serialize(tracker.Profile, options), options)!;
        Check(camel.Journal.HomeCount == 1 && camel.Journal.Entries.Count == tracker.Entries.Count, "journal serialization respects case-insensitive JSON options");
    }

    private static void MalformedJson()
    {
        foreach (var nested in new[] { "null", "42", "\"bad\"", "[]" })
        {
            var profile = JsonSerializer.Deserialize<PetProfile>("{\"Affection\":38,\"Journal\":" + nested + "}")!.Normalize();
            Check(profile.Affection == 38 && profile.Journal.Entries.Count == 0, $"malformed nested journal {nested} preserves healthy growth values");
        }
        const string broken = """
        {"Affection":38,"Fullness":65,"Energy":82,"Journal":{
          "PettingCount":-3,"FeedingCount":"bad","PlayCount":9999999999999999999,
          "HomeCount":true,"FocusCompletedCount":2,
          "VisitedDays":["2026-10-02","2026-10-02","bad",null,{},"0001-01-01"],
          "Entries":[null,42,{},
            {"AtUtc":"2026-10-02T01:00:00+00:00","LocalDate":"2026-10-02","Kind":1,"Text":"ok"},
            {"AtUtc":"bad","LocalDate":"2026-10-02","Kind":1,"Text":"bad"},
            {"AtUtc":"2026-10-02T01:00:00+00:00","LocalDate":"bad","Kind":999,"Text":42}],
          "LastActions":[null,{},
            {"Action":1,"AtUtc":"bad"},
            {"Action":4,"AtUtc":"2026-10-02T01:00:00+00:00"}]}}
        """;
        var repaired = JsonSerializer.Deserialize<PetProfile>(broken)!.Normalize();
        Check(repaired.Affection == 38 && repaired.Fullness == 65 && repaired.Energy == 82,
            "malformed nested values do not discard healthy profile growth");
        Check(repaired.Journal.PettingCount == 0 && repaired.Journal.FeedingCount == 0 && repaired.Journal.PlayCount == 1_000_000
            && repaired.Journal.HomeCount == 0 && repaired.Journal.FocusCompletedCount == 2,
            "malformed numeric journal properties default or clamp safely");
        Check(repaired.Journal.VisitedDays.Count == 1 && repaired.Journal.Entries.Count == 1 && repaired.Journal.LastActions.Count == 1,
            "malformed nested dates, enum values and non-object collection items are rejected individually");
        var wrongCollections = JsonSerializer.Deserialize<PetProfile>("{\"Journal\":{\"VisitedDays\":5,\"Entries\":{},\"LastActions\":\"bad\"}}")!.Normalize();
        Check(wrongCollections.Journal.VisitedDays.Count == 0 && wrongCollections.Journal.Entries.Count == 0
            && wrongCollections.Journal.LastActions.Count == 0, "wrong-type collections normalize to safe empty arrays");
    }
}
