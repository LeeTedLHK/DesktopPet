using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using DesktopPet.Journal;
using DesktopPet.Models;

internal static class P14JournalExportChecks
{
    public static void Run(Action<bool, string> check)
    {
        // Reflection keeps the RED probes compilable before the new service exists.
        var create = typeof(PetProfile).Assembly.GetType("DesktopPet.Services.JournalExportService")?
            .GetMethod("Create", [typeof(string), typeof(string), typeof(PetProfile), typeof(DateTimeOffset)]);
        check(create is not null, "P14 readable diary export is available for a current profile snapshot");
        if (create is null) return;

        var root = Path.Combine(Path.GetTempPath(), "DesktopPet.P14Journal." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CheckReadableSnapshot(create, root, check);
            CheckNormalization(create, root, check);
            CheckEmptySnapshots(create, root, check);
            CheckPreservation(create, root, check);
            check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(),
                "P14 successful and failed diary exports leave no temporary files");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void CheckReadableSnapshot(MethodInfo create, string root, Action<bool, string> check)
    {
        var created = DateTimeOffset.Parse("2026-10-05T12:00:00+08:00");
        var early = new JournalEntry(DateTimeOffset.Parse("2026-10-04T10:05:00+08:00"),
            new(2026, 10, 4), JournalEntryKind.Feeding, "最早：喂食🥣");
        var middle = new JournalEntry(DateTimeOffset.Parse("2026-10-04T23:30:00-01:00"),
            new(2026, 10, 4), JournalEntryKind.Play, "中间：玩球🎾");
        var late = new JournalEntry(DateTimeOffset.Parse("2026-10-05T00:45:00Z"),
            new(2026, 10, 5), JournalEntryKind.Home, "最后：回窝🛏️");
        var entries = new List<JournalEntry> { late, early, middle };
        var visited = new List<DateOnly> { new(2026, 10, 5), new(2026, 10, 4), new(2026, 10, 4) };
        var journal = new JournalState
        {
            VisitedDays = visited, Entries = entries, PettingCount = 11, FeedingCount = 7,
            PlayCount = 13, HomeCount = 5, FocusCompletedCount = 3
        };
        var profile = new PetProfile { Affection = 42, Fullness = 88, Energy = 67, Journal = journal };
        var destination = Path.Combine(root, "nested", "团团日记.TXT");
        Export(create, destination, "团团🐾", profile, created);
        var text = ReadUtf8(destination);

        check(text.Contains("团团🐾", StringComparison.Ordinal) &&
            text.Contains("最早：喂食🥣", StringComparison.Ordinal) &&
            text.Contains("中间：玩球🎾", StringComparison.Ordinal) &&
            text.Contains("最后：回窝🛏️", StringComparison.Ordinal) && !text.Contains('\uFFFD'),
            "P14 text export roundtrips Chinese names, diary text and emoji as valid UTF-8");
        check(text.Contains("导出时间（UTC）：2026-10-05 04:00:00Z", StringComparison.Ordinal) &&
            text.Contains("2026-10-04 02:05:00Z", StringComparison.Ordinal) &&
            text.Contains("2026-10-05 00:30:00Z", StringComparison.Ordinal),
            "P14 exported timestamps identify their UTC basis across input offsets");
        var earlyIndex = text.IndexOf("最早：喂食🥣", StringComparison.Ordinal);
        var middleIndex = text.IndexOf("中间：玩球🎾", StringComparison.Ordinal);
        var lateIndex = text.IndexOf("最后：回窝🛏️", StringComparison.Ordinal);
        check(earlyIndex >= 0 && earlyIndex < middleIndex && middleIndex < lateIndex,
            "P14 diary entries are chronological by instant rather than input order or local timestamp");
        check(HasLine(text, "亲密度：42/100") && HasLine(text, "饱腹度：88/100") && HasLine(text, "精力：67/100") &&
            HasLine(text, "陪伴天数：2") && HasLine(text, "摸头次数：11") && HasLine(text, "喂食次数：7") &&
            HasLine(text, "玩耍次数：13") && HasLine(text, "回窝次数：5") && HasLine(text, "完成专注次数：3") &&
            HasLine(text, "日记条目：3"),
            "P14 readable snapshot includes current care, distinct visited days, all companion totals and entry count");
        check(ReferenceEquals(profile.Journal, journal) && ReferenceEquals(journal.Entries, entries) &&
            ReferenceEquals(journal.VisitedDays, visited) && entries.SequenceEqual(new[] { late, early, middle }) &&
            visited.SequenceEqual(new DateOnly[] { new(2026, 10, 5), new(2026, 10, 4), new(2026, 10, 4) }),
            "P14 export leaves the supplied journal and its original collection order untouched");

        Export(create, destination, "新伙伴🐶", new PetProfile(), created.AddMinutes(1));
        var replacement = ReadUtf8(destination);
        check(replacement.Contains("新伙伴🐶", StringComparison.Ordinal) &&
            !replacement.Contains("团团🐾", StringComparison.Ordinal) && !replacement.Contains("最后：回窝", StringComparison.Ordinal),
            "P14 a successful export replaces an existing text file without stale diary content");
    }

    private static void CheckNormalization(MethodInfo create, string root, Action<bool, string> check)
    {
        var now = DateTimeOffset.Parse("2026-10-05T04:00:00Z");
        var invalid = new JournalEntry(DateTimeOffset.UnixEpoch.AddSeconds(-1),
            new(1969, 12, 31), JournalEntryKind.Play, "不应导出的无效记录");
        var valid = new JournalEntry(now, new(2026, 10, 5), JournalEntryKind.Petting, "保留的记录🐕");
        var entries = new List<JournalEntry> { invalid, valid };
        var journal = new JournalState
        {
            Entries = entries, PettingCount = -8, FeedingCount = 2_000_000,
            PlayCount = 9, HomeCount = -1, FocusCompletedCount = 2,
            VisitedDays = [new(1969, 12, 31), new(2026, 10, 5), new(2026, 10, 5)]
        };
        var profile = new PetProfile { Affection = double.NaN, Fullness = -4, Energy = 250, Journal = journal };
        var destination = Path.Combine(root, "normalized.txt");
        Export(create, destination, "伙伴", profile, now);
        var text = ReadUtf8(destination);
        check(HasLine(text, "亲密度：20/100") && HasLine(text, "饱腹度：0/100") && HasLine(text, "精力：100/100") &&
            HasLine(text, "摸头次数：0") && HasLine(text, "喂食次数：1000000") && HasLine(text, "回窝次数：0") &&
            HasLine(text, "陪伴天数：1") && HasLine(text, "日记条目：1") &&
            text.Contains("保留的记录🐕", StringComparison.Ordinal) && !text.Contains("不应导出", StringComparison.Ordinal),
            "P14 text export uses normalized care, bounded counters and valid current diary entries");
        check(double.IsNaN(profile.Affection) && profile.Fullness == -4 && profile.Energy == 250 &&
            journal.PettingCount == -8 && journal.FeedingCount == 2_000_000 && journal.HomeCount == -1 &&
            ReferenceEquals(profile.Journal, journal) && ReferenceEquals(journal.Entries, entries) &&
            entries.SequenceEqual(new[] { invalid, valid }) && journal.VisitedDays.Count == 3,
            "P14 normalization affects only the exported snapshot and never repairs live input data");
    }

    private static void CheckEmptySnapshots(MethodInfo create, string root, Action<bool, string> check)
    {
        var now = DateTimeOffset.Parse("2026-10-05T04:00:00Z");
        var profiles = new[]
        {
            new PetProfile(),
            new PetProfile { Journal = null! },
            new PetProfile { Journal = new JournalState { Entries = null!, VisitedDays = null!, LastActions = null! } }
        };
        for (var i = 0; i < profiles.Length; i++)
        {
            var destination = Path.Combine(root, $"empty-{i}.txt");
            Export(create, destination, "新朋友", profiles[i], now);
            var text = ReadUtf8(destination);
            check(HasLine(text, "陪伴天数：0") && HasLine(text, "日记条目：0") &&
                HasLine(text, "摸头次数：0") && HasLine(text, "喂食次数：0") &&
                HasLine(text, "玩耍次数：0") && HasLine(text, "回窝次数：0") &&
                HasLine(text, "完成专注次数：0") && HasLine(text, "暂无日记。"),
                $"P14 empty or null journal collections export a readable empty diary (case {i})");
        }
        check(profiles[1].Journal is null && profiles[2].Journal.Entries is null &&
            profiles[2].Journal.VisitedDays is null && profiles[2].Journal.LastActions is null,
            "P14 empty-diary export does not assign collections back into the supplied profile");
    }

    private static void CheckPreservation(MethodInfo create, string root, Action<bool, string> check)
    {
        var now = DateTimeOffset.Parse("2026-10-05T04:00:00Z");
        var profile = new PetProfile();
        foreach (var suffix in new[] { ".json", ".txt.bak", "" })
        {
            var protectedPath = Path.Combine(root, "protected" + suffix);
            var original = Encoding.UTF8.GetBytes("保留原始数据🐾");
            File.WriteAllBytes(protectedPath, original);
            var rejected = false;
            try { Export(create, protectedPath, "伙伴", profile, now); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && File.ReadAllBytes(protectedPath).SequenceEqual(original),
                $"P14 diary export rejects suffix '{suffix}' before overwriting existing data");
        }

        var destination = Path.Combine(root, "locked.txt");
        var originalText = Encoding.UTF8.GetBytes("已经保存的日记🐶");
        File.WriteAllBytes(destination, originalText);
        using (var locked = new FileStream(destination, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failedSafely = false;
            try { Export(create, destination, "伙伴", profile, now); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failedSafely = true; }
            check(failedSafely && File.ReadAllBytes(destination).SequenceEqual(originalText),
                "P14 failed diary replacement preserves every byte of the existing text file");
            check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(),
                "P14 failed diary replacement removes its temporary snapshot before returning");
        }
    }

    private static string ReadUtf8(string path) => new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(File.ReadAllBytes(path));

    private static bool HasLine(string text, string expected) =>
        text.Split(["\r\n", "\n"], StringSplitOptions.None).Contains(expected, StringComparer.Ordinal);

    private static void Export(MethodInfo create, string destination, string petName, PetProfile profile, DateTimeOffset createdUtc)
    {
        try { create.Invoke(null, [destination, petName, profile, createdUtc]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
    }
}
