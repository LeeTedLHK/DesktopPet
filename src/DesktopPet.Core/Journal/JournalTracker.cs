using DesktopPet.Models;

namespace DesktopPet.Journal;

public sealed class JournalTracker
{
    private static readonly TimeSpan ActionCooldown = TimeSpan.FromSeconds(10);
    private readonly Dictionary<JournalAction, DateTimeOffset> _rollbackCeilings = new();
    private bool _enabled;

    public JournalTracker(PetProfile profile, bool enabled, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Profile = profile.Normalize();
        if (JournalState.ValidTime(now)) Profile = Profile with { Journal = Profile.Journal.Normalize(now) };
        _enabled = enabled;
        Visit(now);
    }

    public PetProfile Profile { get; private set; }
    public IReadOnlyList<JournalEntry> Entries => Profile.Journal.Entries;
    public IReadOnlyList<JournalAchievement> Achievements => JournalPolicy.Achievements(Profile.Journal);

    public void UpdateSettings(bool enabled, DateTimeOffset now)
    {
        _enabled = enabled;
        Visit(now);
    }

    public void Visit(DateTimeOffset now)
    {
        if (!_enabled || !JournalState.ValidTime(now)) return;
        var day = DateOnly.FromDateTime(now.DateTime);
        var before = Profile.Journal;
        if (before.VisitedDays.Contains(day)) return;
        // Reserve a slot for the observed date so an old clock date cannot be evicted and logged every tick.
        var retained = before.VisitedDays.Count >= JournalState.MaximumVisitedDays ? before.VisitedDays.Skip(1) : before.VisitedDays;
        var next = before with { VisitedDays = JournalState.BoundDays(retained.Append(day)) };
        Commit(before, next, new JournalEntry(now.ToUniversalTime(), day, JournalEntryKind.Visit,
            $"今天也一起度过啦，这是第 {next.VisitedDays.Count} 天的陪伴。"), now);
    }

    public void Record(JournalAction action, DateTimeOffset now)
    {
        if (!_enabled || !Enum.IsDefined(action) || !JournalState.ValidTime(now)) return;
        Visit(now);
        var before = Profile.Journal;
        var last = before.LastActions.FirstOrDefault(stamp => stamp.Action == action)?.AtUtc;
        if (_rollbackCeilings.TryGetValue(action, out var ceiling) && now >= ceiling) _rollbackCeilings.Remove(action);
        if (last is { } previous)
        {
            if (now >= previous && now - previous < ActionCooldown) return;
            if (now < previous)
            {
                // Accept a clock correction once; a clock that keeps moving backwards cannot farm events.
                if (_rollbackCeilings.ContainsKey(action))
                {
                    Profile = Profile with { Journal = before with { LastActions = Stamps(before, action, now) } };
                    return;
                }
                _rollbackCeilings[action] = previous;
            }
        }

        var next = action switch
        {
            JournalAction.Petting => before with { PettingCount = JournalState.Counter(before.PettingCount + 1) },
            JournalAction.Feeding => before with { FeedingCount = JournalState.Counter(before.FeedingCount + 1) },
            JournalAction.Play => before with { PlayCount = JournalState.Counter(before.PlayCount + 1) },
            JournalAction.Home => before with { HomeCount = JournalState.Counter(before.HomeCount + 1) },
            JournalAction.FocusCompleted => before with { FocusCompletedCount = JournalState.Counter(before.FocusCompletedCount + 1) },
            _ => before
        };
        next = next with { LastActions = Stamps(before, action, now) };
        var kind = action switch
        {
            JournalAction.Petting => JournalEntryKind.Petting, JournalAction.Feeding => JournalEntryKind.Feeding,
            JournalAction.Play => JournalEntryKind.Play, JournalAction.Home => JournalEntryKind.Home,
            _ => JournalEntryKind.FocusCompleted
        };
        Commit(before, next, new JournalEntry(now.ToUniversalTime(), DateOnly.FromDateTime(now.DateTime), kind,
            JournalPolicy.EntryText(kind)), now);
    }

    public string? UnlockedDialogue(string id) => Achievements.FirstOrDefault(a => a.Unlocked && a.Id == id)?.RewardDialogue;

    private static IReadOnlyList<JournalActionStamp> Stamps(JournalState state, JournalAction action, DateTimeOffset now) =>
        Array.AsReadOnly(state.LastActions.Where(stamp => stamp.Action != action)
            .Append(new JournalActionStamp(action, now.ToUniversalTime())).OrderBy(stamp => stamp.Action).ToArray());

    private void Commit(JournalState before, JournalState next, JournalEntry entry, DateTimeOffset now)
    {
        var unlocked = JournalPolicy.Achievements(before).Where(a => a.Unlocked).Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var entries = before.Entries.Append(entry).ToList();
        foreach (var achievement in JournalPolicy.Achievements(next).Where(a => a.Unlocked && !unlocked.Contains(a.Id)))
            entries.Add(new JournalEntry(now.ToUniversalTime(), DateOnly.FromDateTime(now.DateTime), JournalEntryKind.Achievement,
                $"解锁成就「{achievement.Title}」：{achievement.RewardDialogue}", achievement.Id));
        Profile = Profile with { Journal = (next with { Entries = entries }).Normalize() };
    }
}
