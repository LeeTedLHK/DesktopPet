namespace DesktopPet.Journal;

public enum JournalAction { Petting, Feeding, Play, Home, FocusCompleted }
public enum JournalEntryKind { Visit, Petting, Feeding, Play, Home, FocusCompleted, Achievement }

public sealed record JournalEntry(DateTimeOffset AtUtc, DateOnly LocalDate,
    JournalEntryKind Kind, string Text, string? AchievementId = null);
public sealed record JournalActionStamp(JournalAction Action, DateTimeOffset AtUtc);
public sealed record JournalAchievement(string Id, string Title, string Description,
    int Current, int Required, bool Unlocked, string RewardDialogue)
{
    public string Status => Unlocked ? "已解锁" : $"{Current}/{Required}";
    public string Progress => $"{Current}/{Required}";
}
