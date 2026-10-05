using System.Globalization;

namespace DesktopPet.Journal;

public enum JournalDateRange { All, Today, Last7Days, Last30Days }

public sealed record JournalFilter(string SearchText, JournalEntryKind? Kind, JournalDateRange Range, DateOnly Today);

/// <summary>Queries stored diary records without changing or normalizing the source.</summary>
public static class JournalQuery
{
    public static IReadOnlyList<JournalEntry> Filter(IEnumerable<JournalEntry> entries, JournalFilter filter)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.Kind is { } kind && !Enum.IsDefined(kind))
            return Array.AsReadOnly(Array.Empty<JournalEntry>());

        var keyword = (filter.SearchText ?? "").Trim();
        var range = EffectiveRange(filter.Range);
        var start = StartDate(range, filter.Today);
        return Array.AsReadOnly(entries
            .Where(entry => filter.Kind is null || entry.Kind == filter.Kind)
            .Where(entry => range == JournalDateRange.All || entry.LocalDate >= start && entry.LocalDate <= filter.Today)
            .Where(entry => keyword.Length == 0 ||
                (entry.Text ?? "").Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                KindLabel(entry.Kind).Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.AtUtc)
            .ToArray());
    }

    public static string Description(JournalFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var keyword = (filter.SearchText ?? "").Trim();
        var kind = filter.Kind is { } selected
            ? Enum.IsDefined(selected) ? KindLabel(selected) : "未知类型（无匹配）"
            : "全部";
        var range = EffectiveRange(filter.Range);
        var today = DateText(filter.Today);
        var start = DateText(StartDate(range, filter.Today));
        var dates = range switch
        {
            JournalDateRange.Today => $"今天（{today}）",
            JournalDateRange.Last7Days => $"最近 7 天（{start} 至 {today}）",
            JournalDateRange.Last30Days => $"最近 30 天（{start} 至 {today}）",
            _ => "全部"
        };
        return $"关键词：{(keyword.Length == 0 ? "全部" : keyword)}；类型：{kind}；日期：{dates}";
    }

    public static string KindLabel(JournalEntryKind kind) => kind switch
    {
        JournalEntryKind.Visit => "相伴",
        JournalEntryKind.Petting => "摸头",
        JournalEntryKind.Feeding => "喂食",
        JournalEntryKind.Play => "玩耍",
        JournalEntryKind.Home => "回窝",
        JournalEntryKind.FocusCompleted => "完成专注",
        JournalEntryKind.Achievement => "成长成就",
        _ => "日记"
    };

    private static JournalDateRange EffectiveRange(JournalDateRange range) =>
        Enum.IsDefined(range) ? range : JournalDateRange.All;

    private static DateOnly StartDate(JournalDateRange range, DateOnly today)
    {
        var daysBefore = range switch { JournalDateRange.Last7Days => 6, JournalDateRange.Last30Days => 29, _ => 0 };
        return DateOnly.FromDayNumber(Math.Max(DateOnly.MinValue.DayNumber, today.DayNumber - daysBefore));
    }

    private static string DateText(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
