using System.Text.Json.Serialization;

namespace DesktopPet.Journal;

[JsonConverter(typeof(JournalStateJsonConverter))]
public sealed record JournalState
{
    public const int MaximumEntries = 200;
    public const int MaximumVisitedDays = 36600;
    public const int MaximumCounter = 1_000_000;
    internal static readonly DateOnly EarliestDate = new(1970, 1, 1);
    public IReadOnlyList<DateOnly> VisitedDays { get; init; } = Array.Empty<DateOnly>();
    public int PettingCount { get; init; }
    public int FeedingCount { get; init; }
    public int PlayCount { get; init; }
    public int HomeCount { get; init; }
    public int FocusCompletedCount { get; init; }
    public IReadOnlyList<JournalEntry> Entries { get; init; } = Array.Empty<JournalEntry>();
    public IReadOnlyList<JournalActionStamp> LastActions { get; init; } = Array.Empty<JournalActionStamp>();

    public JournalState Normalize() => Normalize(null);

    internal JournalState Normalize(DateTimeOffset? now) => this with
    {
        PettingCount = Counter(PettingCount), FeedingCount = Counter(FeedingCount),
        PlayCount = Counter(PlayCount), HomeCount = Counter(HomeCount), FocusCompletedCount = Counter(FocusCompletedCount),
        VisitedDays = BoundDays(VisitedDays ?? Array.Empty<DateOnly>(), now),
        Entries = BoundEntries(Entries ?? Array.Empty<JournalEntry>(), now),
        LastActions = BoundActions(LastActions ?? Array.Empty<JournalActionStamp>(), now)
    };

    internal static int Counter(int value) => Math.Clamp(value, 0, MaximumCounter);
    internal static bool ValidTime(DateTimeOffset value) => value >= DateTimeOffset.UnixEpoch;

    internal static IReadOnlyList<DateOnly> BoundDays(IEnumerable<DateOnly> values, DateTimeOffset? now = null)
    {
        var days = new SortedSet<DateOnly>();
        var today = now is { } time ? DateOnly.FromDateTime(time.DateTime) : DateOnly.MaxValue;
        foreach (var day in values)
        {
            if (day < EarliestDate || day > today) continue;
            days.Add(day);
            if (days.Count > MaximumVisitedDays) days.Remove(days.Min);
        }
        return Array.AsReadOnly(days.ToArray());
    }

    internal static IReadOnlyList<JournalEntry> BoundEntries(IEnumerable<JournalEntry> values, DateTimeOffset? now = null)
    {
        var entries = new List<JournalEntry>(MaximumEntries);
        var today = now is { } time ? DateOnly.FromDateTime(time.DateTime) : DateOnly.MaxValue;
        foreach (var entry in values)
        {
            if (entry is null || !ValidTime(entry.AtUtc) || entry.LocalDate < EarliestDate || entry.LocalDate > today
                || !Enum.IsDefined(entry.Kind) || (now is { } latest && entry.AtUtc > latest)) continue;
            if (entry.Kind == JournalEntryKind.Achievement && !JournalPolicy.IsAchievement(entry.AchievementId)) continue;
            var safe = entry with
            {
                AtUtc = entry.AtUtc.ToUniversalTime(), Text = SafeText(entry.Text, entry.Kind),
                AchievementId = entry.Kind == JournalEntryKind.Achievement ? entry.AchievementId : null
            };
            // Insertion is bounded to 200 items, including when the incoming file is oversized or unordered.
            var index = entries.FindIndex(existing => existing.AtUtc > safe.AtUtc);
            if (index < 0) entries.Add(safe); else entries.Insert(index, safe);
            if (entries.Count > MaximumEntries) entries.RemoveAt(0);
        }
        return Array.AsReadOnly(entries.ToArray());
    }

    internal static IReadOnlyList<JournalActionStamp> BoundActions(IEnumerable<JournalActionStamp> values, DateTimeOffset? now = null)
    {
        var actions = new Dictionary<JournalAction, DateTimeOffset>();
        foreach (var stamp in values)
        {
            if (stamp is null || !Enum.IsDefined(stamp.Action) || !ValidTime(stamp.AtUtc)
                || (now is { } latest && stamp.AtUtc > latest)) continue;
            if (!actions.TryGetValue(stamp.Action, out var previous) || stamp.AtUtc > previous)
                actions[stamp.Action] = stamp.AtUtc.ToUniversalTime();
        }
        return Array.AsReadOnly(actions.OrderBy(pair => pair.Key).Select(pair => new JournalActionStamp(pair.Key, pair.Value)).ToArray());
    }

    private static string SafeText(string? value, JournalEntryKind kind)
    {
        var text = new string((value ?? "").Where(c => !char.IsControl(c)).Take(160).ToArray()).Trim();
        return text.Length > 0 ? text : JournalPolicy.EntryText(kind);
    }
}
