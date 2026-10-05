namespace DesktopPet.Journal;

internal static class JournalPolicy
{
    private sealed record Milestone(string Id, string Title, string Description, int Required,
        string RewardDialogue, Func<JournalState, int> Count);

    private static readonly Milestone[] Milestones =
    [
        new("first-interaction", "初次亲近", "第一次摸头、喂食、玩耍或回窝", 1,
            "记住你的温柔啦，以后也想一直陪着你。", state => state.PettingCount + state.FeedingCount + state.PlayCount + state.HomeCount),
        new("three-days", "三日好伙伴", "在三个不同的日子相伴", 3,
            "三天的陪伴，已经是我的小小幸福啦。", state => state.VisitedDays.Count),
        new("ten-feeds", "暖心小厨师", "累计喂食十次", 10,
            "吃饱啦！你照顾我的每一次，我都记得。", state => state.FeedingCount),
        new("five-plays", "快乐玩伴", "累计玩耍五次", 5,
            "和你玩耍的时光，比最喜欢的玩具还珍贵。", state => state.PlayCount),
        new("first-home", "安心的小窝", "第一次回到小窝", 1,
            "有你留给我的小窝，哪里都是家。", state => state.HomeCount),
        new("three-focus", "专注搭档", "累计完成三次专注", 3,
            "你又认真完成了目标，我一直在旁边为你加油。", state => state.FocusCompletedCount)
    ];

    internal static bool IsAchievement(string? id) => Milestones.Any(m => string.Equals(m.Id, id, StringComparison.Ordinal));

    internal static IReadOnlyList<JournalAchievement> Achievements(JournalState state) => Array.AsReadOnly(Milestones.Select(m =>
    {
        var current = Math.Clamp(m.Count(state), 0, m.Required);
        return new JournalAchievement(m.Id, m.Title, m.Description, current, m.Required, current >= m.Required, m.RewardDialogue);
    }).ToArray());

    internal static string EntryText(JournalEntryKind kind) => kind switch
    {
        JournalEntryKind.Visit => "今天也一起度过啦。",
        JournalEntryKind.Petting => "被温柔地摸了摸头，心里暖暖的。",
        JournalEntryKind.Feeding => "收到一份好吃的，肚子和心情都很满足。",
        JournalEntryKind.Play => "一起玩耍，留下了一段快乐时光。",
        JournalEntryKind.Home => "回到小窝，安心休息一会儿。",
        JournalEntryKind.FocusCompleted => "陪你完成了一次专注，为你鼓掌。",
        _ => "解锁了一段新的成长回忆。"
    };
}
