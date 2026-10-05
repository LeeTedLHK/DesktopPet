using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using DesktopPet.Journal;
using DesktopPet.Models;

internal static class P15JournalQueryChecks
{
    private static readonly DateOnly Today = new(2026, 10, 5);

    public static void Run(Action<bool, string> check)
    {
        // These probes compile against P14 and fail for the absent P15 behavior, before implementation.
        var assembly = typeof(PetProfile).Assembly;
        var queryType = assembly.GetType("DesktopPet.Journal.JournalQuery");
        var filterType = assembly.GetType("DesktopPet.Journal.JournalFilter");
        var rangeType = assembly.GetType("DesktopPet.Journal.JournalDateRange");
        check(queryType is not null && filterType is not null && rangeType?.IsEnum == true,
            "P15 diary query exposes immutable filters and local-date ranges");
        if (queryType is null || filterType is null || rangeType is null) return;

        var constructor = filterType.GetConstructor([typeof(string), typeof(JournalEntryKind?), rangeType, typeof(DateOnly)]);
        var filter = queryType.GetMethod("Filter", [typeof(IEnumerable<JournalEntry>), filterType]);
        var description = queryType.GetMethod("Description", [filterType]);
        var label = queryType.GetMethod("KindLabel", [typeof(JournalEntryKind)]);
        var exportType = assembly.GetType("DesktopPet.Services.JournalExportService");
        var filteredExport = exportType?.GetMethod("CreateFiltered",
            [typeof(string), typeof(string), typeof(PetProfile), typeof(DateTimeOffset), filterType]);
        check(constructor is not null && filter?.ReturnType == typeof(IReadOnlyList<JournalEntry>) &&
            description?.ReturnType == typeof(string) && label?.ReturnType == typeof(string) &&
            filteredExport?.ReturnType == typeof(void),
            "P15 query and filtered TXT export provide the specified callable contracts");
        if (constructor is null || filter is null || description is null || label is null || filteredExport is null) return;

        var api = new QueryApi(rangeType, constructor, filter, description, label, filteredExport);
        CheckSearchAndKinds(api, check);
        CheckLocalDateRanges(api, check);
        CheckOrderedImmutableSnapshot(api, check);
        CheckDescriptions(api, check);
        var root = Path.Combine(Path.GetTempPath(), "DesktopPet.P15Journal." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            CheckFilteredSnapshot(api, root, check);
            CheckFullCompatibility(exportType!, root, check);
            CheckSafeFailures(api, root, check);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static void CheckSearchAndKinds(QueryApi api, Action<bool, string> check)
    {
        var feeding = Entry(Today, JournalEntryKind.Feeding, "FRESH MILK🥣", 4);
        var petting = Entry(Today, JournalEntryKind.Petting, "milk cuddles", 3);
        var chinese = Entry(Today, JournalEntryKind.Feeding, "今天吃饱啦🥣", 2);
        var focus = Entry(Today, JournalEntryKind.FocusCompleted, "一段专注", 1);
        JournalEntry[] entries = [petting, chinese, feeding, focus];
        var priorCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            check(SameReferences(api.Filter(entries, api.NewFilter("  mIlK  ", JournalEntryKind.Feeding, 0, Today)), feeding),
                "P15 search trims whitespace and uses ordinal case-insensitive text matching with kind AND");
        }
        finally { CultureInfo.CurrentCulture = priorCulture; }
        check(SameReferences(api.Filter(entries, api.NewFilter(" 喂食 ", null, 0, Today)), feeding, chinese),
            "P15 keyword search includes readable Chinese record-kind labels");
        check(SameReferences(api.Filter(entries, api.NewFilter("🥣", JournalEntryKind.Feeding, 0, Today)), feeding, chinese),
            "P15 keyword search retains Chinese and emoji text without lossy conversion");
        check(SameReferences(api.Filter(entries, api.NewFilter(" \t\r\n ", null, 0, Today)), feeding, petting, chinese, focus),
            "P15 blank search and all kinds retain every matching record");
        var oldMatch = Entry(Today.AddDays(-7), JournalEntryKind.Feeding, "milk", 8);
        var wrongKind = Entry(Today, JournalEntryKind.Home, "milk", 7);
        var wrongText = Entry(Today, JournalEntryKind.Feeding, "water", 6);
        check(SameReferences(api.Filter([oldMatch, wrongKind, wrongText, feeding],
            api.NewFilter("milk", JournalEntryKind.Feeding, 2, Today)), feeding),
            "P15 keyword, kind and local-date range combine with AND");
        var undefined = (JournalEntryKind)999;
        check(api.Filter([feeding, Entry(Today, undefined, "milk", 9)], api.NewFilter("", undefined, 0, Today)).Count == 0,
            "P15 an undefined selected kind matches no record, including undefined input kinds");
        (JournalEntryKind Kind, string Label)[] labels =
        [
            (JournalEntryKind.Visit, "相伴"), (JournalEntryKind.Petting, "摸头"),
            (JournalEntryKind.Feeding, "喂食"), (JournalEntryKind.Play, "玩耍"),
            (JournalEntryKind.Home, "回窝"), (JournalEntryKind.FocusCompleted, "完成专注"),
            (JournalEntryKind.Achievement, "成长成就")
        ];
        check(labels.All(pair => api.KindLabel(pair.Kind) == pair.Label) && api.KindLabel(undefined) == "日记",
            "P15 readable kind labels preserve existing diary export terminology");
    }

    private static void CheckLocalDateRanges(QueryApi api, Action<bool, string> check)
    {
        // UTC instants deliberately do not correspond to stored dates: LocalDate alone defines membership.
        var future = Entry(Today.AddDays(1), JournalEntryKind.Visit, "future", 10);
        var today = Entry(Today, JournalEntryKind.Visit, "today", 9);
        var yesterday = Entry(Today.AddDays(-1), JournalEntryKind.Visit, "yesterday", 8);
        var sevenStart = Entry(Today.AddDays(-6), JournalEntryKind.Visit, "seven-start", 7);
        var eightOld = Entry(Today.AddDays(-7), JournalEntryKind.Visit, "eight-old", 6);
        var thirtyStart = Entry(Today.AddDays(-29), JournalEntryKind.Visit, "thirty-start", 5);
        var thirtyOneOld = Entry(Today.AddDays(-30), JournalEntryKind.Visit, "thirty-one-old", 4);
        JournalEntry[] entries = [thirtyStart, yesterday, thirtyOneOld, future, eightOld, today, sevenStart];
        check(SameReferences(api.Filter(entries, api.NewFilter("", null, 1, Today)), today),
            "P15 Today uses the stored local date rather than the timestamp or machine clock");
        check(SameReferences(api.Filter(entries, api.NewFilter("", null, 2, Today)), today, yesterday, sevenStart),
            "P15 Last7Days includes today-6 and today, excludes today-7 and future dates");
        check(SameReferences(api.Filter(entries, api.NewFilter("", null, 3, Today)), today, yesterday, sevenStart, eightOld, thirtyStart),
            "P15 Last30Days includes today-29 and today, excludes today-30 and future dates");
        check(SameReferences(api.Filter(entries, api.NewFilter("", null, 0, Today)),
            future, today, yesterday, sevenStart, eightOld, thirtyStart, thirtyOneOld),
            "P15 All retains future and older stored entries without date restrictions");
        check(SameReferences(api.Filter(entries, api.NewFilter("", null, 999, Today)),
            future, today, yesterday, sevenStart, eightOld, thirtyStart, thirtyOneOld),
            "P15 an invalid date range safely falls back to All");

        var minimumToday = DateOnly.MinValue.AddDays(2);
        var minimum = Entry(DateOnly.MinValue, JournalEntryKind.Visit, "minimum", 1);
        var next = Entry(DateOnly.MinValue.AddDays(1), JournalEntryKind.Visit, "next", 2);
        var current = Entry(minimumToday, JournalEntryKind.Visit, "current", 3);
        var tooNew = Entry(minimumToday.AddDays(1), JournalEntryKind.Visit, "too-new", 4);
        foreach (var range in new[] { 2, 3 })
            check(SameReferences(api.Filter([tooNew, minimum, next, current], api.NewFilter("", null, range, minimumToday)),
                current, next, minimum), $"P15 relative range {range} saturates at DateOnly.MinValue without admitting future dates");
        check(SameReferences(api.Filter([minimum, next], api.NewFilter("", null, 2, DateOnly.MinValue)), minimum),
            "P15 a relative range anchored at the minimum date is well-defined");
    }

    private static void CheckOrderedImmutableSnapshot(QueryApi api, Action<bool, string> check)
    {
        var earlier = new JournalEntry(DateTimeOffset.Parse("2026-10-05T11:00:00+08:00"), Today,
            JournalEntryKind.Visit, "earlier");
        var latest = new JournalEntry(DateTimeOffset.Parse("2026-10-04T22:00:00-08:00"), Today.AddDays(-1),
            JournalEntryKind.Feeding, "latest");
        var middle = new JournalEntry(DateTimeOffset.Parse("2026-10-05T05:00:00Z"), Today.AddDays(1),
            JournalEntryKind.Play, "middle");
        var input = new List<JournalEntry> { earlier, latest, middle };
        var result = api.Filter(input, api.NewFilter("", null, 0, Today));
        check(SameReferences(result, latest, middle, earlier) && SameReferences(input, earlier, latest, middle),
            "P15 results sort newest-first by absolute timestamp, retain entry identity and preserve source order");
        check(RejectsMutation(result, earlier), "P15 callers cannot mutate a query result through collection interfaces");
        input.Clear();
        input.Add(Entry(Today, JournalEntryKind.Home, "new input", 12));
        check(SameReferences(result, latest, middle, earlier),
            "P15 query results are snapshots independent of later input-list changes");
        var empty = api.Filter(input, api.NewFilter("missing", null, 0, Today));
        check(empty.Count == 0 && RejectsMutation(empty, earlier),
            "P15 no-match queries return an immutable empty snapshot");
    }

    private static void CheckDescriptions(QueryApi api, Action<bool, string> check)
    {
        var description = api.Description(api.NewFilter("  milk🥣  ", JournalEntryKind.Feeding, 2, Today));
        check(description.Contains("milk🥣", StringComparison.Ordinal) && !description.Contains("  milk", StringComparison.Ordinal) &&
            description.Contains("喂食", StringComparison.Ordinal) && description.Contains("2026-09-29", StringComparison.Ordinal) &&
            description.Contains("2026-10-05", StringComparison.Ordinal),
            "P15 filter description identifies the trimmed keyword, readable kind and captured inclusive local-date bounds");
        var thirty = api.Description(api.NewFilter("", null, 3, Today));
        var today = api.Description(api.NewFilter("", null, 1, Today));
        check(thirty.Contains("2026-09-06", StringComparison.Ordinal) && thirty.Contains("2026-10-05", StringComparison.Ordinal) &&
            today.Contains("2026-10-05", StringComparison.Ordinal),
            "P15 relative descriptions disclose their captured dates rather than a later machine date");
        var all = api.Description(api.NewFilter("", null, 0, Today));
        check(all.Contains("全部", StringComparison.Ordinal) &&
            api.Description(api.NewFilter("", null, 999, Today)) == all &&
            api.Description(api.NewFilter("", null, 2, DateOnly.MinValue)).Contains("0001-01-01", StringComparison.Ordinal),
            "P15 descriptions explain All, invalid-range fallback and saturated date bounds");
    }

    private static void CheckFilteredSnapshot(QueryApi api, string root, Action<bool, string> check)
    {
        var early = new JournalEntry(DateTimeOffset.Parse("2026-09-29T12:00:00+08:00"), Today.AddDays(-6),
            JournalEntryKind.Feeding, "selected 最早🥣\n");
        var late = new JournalEntry(DateTimeOffset.Parse("2026-10-05T04:00:00Z"), Today,
            JournalEntryKind.Feeding, "selected 最后🥣");
        var old = Entry(Today.AddDays(-7), JournalEntryKind.Feeding, "selected 超出范围🥣", 4);
        var wrongKind = Entry(Today, JournalEntryKind.Home, "selected 超出类型🥣", 3);
        var wrongText = Entry(Today, JournalEntryKind.Feeding, "未匹配关键词🥣", 2);
        var invalid = new JournalEntry(DateTimeOffset.UnixEpoch.AddSeconds(-1), new(1969, 12, 31),
            JournalEntryKind.Feeding, "selected 无效记录🥣");
        var entries = new List<JournalEntry> { late, invalid, old, wrongKind, early, wrongText };
        var visited = new List<DateOnly> { Today, Today.AddDays(-1), Today };
        var journal = new JournalState
        {
            Entries = entries, VisitedDays = visited, PettingCount = 17, FeedingCount = 31,
            PlayCount = 23, HomeCount = 9, FocusCompletedCount = 5
        };
        var profile = new PetProfile { Affection = 42, Fullness = 88, Energy = 67, Journal = journal };
        var created = DateTimeOffset.Parse("2026-10-09T12:00:00+08:00");
        var filter = api.NewFilter("  selected  ", JournalEntryKind.Feeding, 2, Today);
        var destination = Path.Combine(root, "nested", "筛选日记.TXT");
        api.Export(destination, "团团🐾", profile, created, filter);
        var text = ReadUtf8(destination);
        check(text.Contains("团团🐾", StringComparison.Ordinal) && text.Contains("selected 最早🥣", StringComparison.Ordinal) &&
            text.Contains("selected 最后🥣", StringComparison.Ordinal) && !text.Contains("超出范围", StringComparison.Ordinal) &&
            !text.Contains("超出类型", StringComparison.Ordinal) && !text.Contains("未匹配关键词", StringComparison.Ordinal) &&
            !text.Contains("无效记录", StringComparison.Ordinal) && !text.Contains('\uFFFD'),
            "P15 filtered TXT includes only the AND-matching normalized Unicode diary rows");
        check(text.Contains("筛选条件：", StringComparison.Ordinal) && text.Contains("selected", StringComparison.Ordinal) &&
            text.Contains("喂食", StringComparison.Ordinal) && text.Contains("2026-09-29", StringComparison.Ordinal) &&
            HasLine(text, "筛选基准本地日期：2026-10-05") && HasLine(text, "筛选条目：2") && HasLine(text, "日记条目总数：5"),
            "P15 filtered TXT labels criteria, captured local date, selected count and normalized total count");
        check(text.Contains("累计陪伴记录", StringComparison.Ordinal) && HasLine(text, "陪伴天数：2") &&
            HasLine(text, "摸头次数：17") && HasLine(text, "喂食次数：31") && HasLine(text, "玩耍次数：23") &&
            HasLine(text, "回窝次数：9") && HasLine(text, "完成专注次数：5") &&
            HasLine(text, "亲密度：42/100") && HasLine(text, "饱腹度：88/100") && HasLine(text, "精力：67/100"),
            "P15 filtered TXT identifies lifetime companion totals and preserves current care values");
        var earlyIndex = text.IndexOf("selected 最早🥣", StringComparison.Ordinal);
        var lateIndex = text.IndexOf("selected 最后🥣", StringComparison.Ordinal);
        check(earlyIndex >= 0 && earlyIndex < lateIndex &&
            text.Contains("导出时间（UTC）：2026-10-09 04:00:00Z", StringComparison.Ordinal) &&
            text.Contains("2026-09-29 04:00:00Z", StringComparison.Ordinal),
            "P15 filtered TXT writes chronological rows and unambiguous UTC times independently of its captured filter date");
        check(ReferenceEquals(profile.Journal, journal) && ReferenceEquals(journal.Entries, entries) &&
            ReferenceEquals(journal.VisitedDays, visited) && SameReferences(entries, late, invalid, old, wrongKind, early, wrongText) &&
            early.Text.EndsWith('\n') && visited.SequenceEqual(new[] { Today, Today.AddDays(-1), Today }),
            "P15 filtered export normalizes its own snapshot without changing live records, collection order or visited days");

        api.Export(destination, "新伙伴🐶", new PetProfile(), created, api.NewFilter("missing", null, 0, Today));
        var empty = ReadUtf8(destination);
        check(HasLine(empty, "筛选条目：0") && HasLine(empty, "日记条目总数：0") &&
            empty.Contains("暂无", StringComparison.Ordinal) && empty.Contains("新伙伴🐶", StringComparison.Ordinal) &&
            !empty.Contains("selected 最后", StringComparison.Ordinal),
            "P15 empty-result exports remain readable and successful replacement removes stale rows");
        check(!Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).Any(),
            "P15 successful filtered exports leave no temporary files");
    }

    private static void CheckFullCompatibility(Type exportType, string root, Action<bool, string> check)
    {
        var create = exportType.GetMethod("Create", [typeof(string), typeof(string), typeof(PetProfile), typeof(DateTimeOffset)]);
        var build = exportType.GetMethod("BuildText", [typeof(string), typeof(PetProfile), typeof(DateTimeOffset)]);
        check(create is not null && build is not null && exportType.GetMethods().Count(method => method.Name == "Create") == 1 &&
            exportType.GetMethods().Count(method => method.Name == "BuildText") == 1,
            "P15 preserves the exact existing Create4 and BuildText3 contracts without ambiguous overloads");
        if (create is null || build is null) return;
        var created = DateTimeOffset.Parse("2026-10-05T04:00:00Z");
        var expected = string.Join(Environment.NewLine, new[]
        {
            "DesktopPet 陪伴日记", "宠物：伙伴", "导出时间（UTC）：2026-10-05 04:00:00Z", "",
            "当前照料状态", "亲密度：20/100", "饱腹度：70/100", "精力：80/100", "",
            "累计陪伴记录", "陪伴天数：0", "摸头次数：0", "喂食次数：0", "玩耍次数：0", "回窝次数：0",
            "完成专注次数：0", "日记条目：0", "", "日记（本地日期 / UTC 时间，按时间顺序）", "暂无日记。", ""
        });
        check((string)Invoke(build, "伙伴", new PetProfile(), created)! == expected,
            "P15 full diary BuildText retains its complete existing readable format");
        var destination = Path.Combine(root, "full-compatible.txt");
        Invoke(create, destination, "伙伴", new PetProfile(), created);
        byte[] expectedBytes = [0xEF, 0xBB, 0xBF, .. new UTF8Encoding(false, true).GetBytes(expected)];
        check(File.ReadAllBytes(destination).SequenceEqual(expectedBytes),
            "P15 full TXT export retains its byte-compatible BOM and content");
    }

    private static void CheckSafeFailures(QueryApi api, string root, Action<bool, string> check)
    {
        var profile = new PetProfile();
        var created = DateTimeOffset.Parse("2026-10-05T04:00:00Z");
        var filter = api.NewFilter("", null, 0, Today);
        var foreignTemporary = Path.Combine(root, ".desktop-pet-journal-foreign.tmp");
        byte[] original = Encoding.UTF8.GetBytes("保留已保存日记🐾");
        File.WriteAllBytes(foreignTemporary, original);
        foreach (var suffix in new[] { ".json", ".txt.bak", "" })
        {
            var destination = Path.Combine(root, "protected" + suffix);
            File.WriteAllBytes(destination, original);
            var rejected = false;
            try { api.Export(destination, "伙伴", profile, created, filter); }
            catch (ArgumentException) { rejected = true; }
            check(rejected && File.ReadAllBytes(destination).SequenceEqual(original),
                $"P15 filtered export rejects suffix '{suffix}' before changing an existing file");
        }

        var lockedTarget = Path.Combine(root, "locked-filtered.txt");
        File.WriteAllBytes(lockedTarget, original);
        using (var locked = new FileStream(lockedTarget, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var failed = false;
            try { api.Export(lockedTarget, "伙伴", profile, created, filter); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failed = true; }
            check(failed && File.ReadAllBytes(lockedTarget).SequenceEqual(original),
                "P15 failed atomic filtered replacement retains every byte of the locked target");
        }
        var invalidTextTarget = Path.Combine(root, "invalid-text-filtered.txt");
        File.WriteAllBytes(invalidTextTarget, original);
        var invalidTextRejected = false;
        try { api.Export(invalidTextTarget, "\uD800", profile, created, filter); }
        catch (EncoderFallbackException) { invalidTextRejected = true; }
        check(invalidTextRejected && File.ReadAllBytes(invalidTextTarget).SequenceEqual(original),
            "P15 invalid UTF-16 filtered export fails before replacing the existing target");
        check(Directory.EnumerateFiles(root, "*.tmp", SearchOption.AllDirectories).SequenceEqual([foreignTemporary]) &&
            File.ReadAllBytes(foreignTemporary).SequenceEqual(original),
            "P15 failed filtered exports clean only their owned temporary file and preserve unrelated temporary files");
    }

    private static JournalEntry Entry(DateOnly date, JournalEntryKind kind, string text, int instantDay) =>
        new(DateTimeOffset.UnixEpoch.AddDays(instantDay), date, kind, text);

    private static bool SameReferences(IEnumerable<JournalEntry> actual, params JournalEntry[] expected)
    {
        var items = actual.ToArray();
        return items.Length == expected.Length && items.Where((item, index) => !ReferenceEquals(item, expected[index])).Count() == 0;
    }

    private static bool RejectsMutation(IReadOnlyList<JournalEntry> result, JournalEntry entry)
    {
        if (result is ICollection<JournalEntry> generic)
        {
            try { generic.Add(entry); return false; }
            catch (NotSupportedException) { }
        }
        if (result is IList list && result.Count > 0)
        {
            try { list[0] = entry; return false; }
            catch (NotSupportedException) { }
        }
        return true;
    }

    private static string ReadUtf8(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static bool HasLine(string text, string expected) =>
        text.Split(["\r\n", "\n"], StringSplitOptions.None).Contains(expected, StringComparer.Ordinal);

    private static object? Invoke(MethodBase method, params object?[] arguments)
    {
        try { return method is ConstructorInfo constructor ? constructor.Invoke(arguments) : ((MethodInfo)method).Invoke(null, arguments); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private sealed class QueryApi(Type rangeType, ConstructorInfo constructor, MethodInfo filter,
        MethodInfo description, MethodInfo kindLabel, MethodInfo export)
    {
        public object NewFilter(string searchText, JournalEntryKind? kind, int range, DateOnly today) =>
            Invoke(constructor, searchText, kind, Enum.ToObject(rangeType, range), today)!;
        public IReadOnlyList<JournalEntry> Filter(IEnumerable<JournalEntry> entries, object criteria) =>
            (IReadOnlyList<JournalEntry>)Invoke(filter, entries, criteria)!;
        public string Description(object criteria) => (string)Invoke(description, criteria)!;
        public string KindLabel(JournalEntryKind kind) => (string)Invoke(kindLabel, kind)!;
        public void Export(string destination, string petName, PetProfile profile, DateTimeOffset createdUtc, object criteria) =>
            Invoke(export, destination, petName, profile, createdUtc, criteria);
    }
}
