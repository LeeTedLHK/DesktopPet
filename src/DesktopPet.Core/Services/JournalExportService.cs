using System.Globalization;
using System.Text;
using DesktopPet.Journal;
using DesktopPet.Models;

namespace DesktopPet.Services;

/// <summary>Exports a readable care and diary snapshot without reading or changing live profile files.</summary>
public static class JournalExportService
{
    // A BOM helps Windows text editors recognize Chinese text; invalid UTF-16 fails safely before replacement.
    private static readonly Encoding TextEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true);

    public static void Create(string destination, string petName, PetProfile profile, DateTimeOffset createdUtc)
    {
        var target = GetTarget(destination);
        var text = BuildText(petName, profile, createdUtc);
        WriteAtomically(target, text);
    }

    public static void CreateFiltered(string destination, string petName, PetProfile profile, DateTimeOffset createdUtc,
        JournalFilter filter)
    {
        var target = GetTarget(destination);
        ArgumentNullException.ThrowIfNull(petName);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(filter);
        var snapshot = profile.Normalize();
        var entries = JournalQuery.Filter(snapshot.Journal.Entries, filter).OrderBy(entry => entry.AtUtc).ToArray();
        var text = BuildSnapshotText(petName, snapshot, createdUtc, entries, filter);
        WriteAtomically(target, text);
    }

    private static string GetTarget(string destination)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        var target = Path.GetFullPath(destination);
        if (!Path.GetExtension(target).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("陪伴日记必须保存为 TXT 文件。", nameof(destination));
        return target;
    }

    private static void WriteAtomically(string target, string text)
    {
        var directory = Path.GetDirectoryName(target)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, ".desktop-pet-journal-" + Guid.NewGuid().ToString("N") + ".tmp");
        var ownsTemporary = false;
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                using (var writer = new StreamWriter(stream, TextEncoding, leaveOpen: true))
                {
                    writer.Write(text);
                    writer.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            // ReplaceFile preserves existing file metadata and handles atomic replacement on Windows.
            // A plain move creates a new target and fails safely if another writer created it first.
            if (File.Exists(target)) File.Replace(temporary, target, destinationBackupFileName: null);
            else File.Move(temporary, target);
        }
        finally
        {
            if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static string BuildText(string petName, PetProfile profile, DateTimeOffset createdUtc)
    {
        ArgumentNullException.ThrowIfNull(petName);
        ArgumentNullException.ThrowIfNull(profile);
        var snapshot = profile.Normalize();
        return BuildSnapshotText(petName, snapshot, createdUtc, snapshot.Journal.Entries, filter: null);
    }

    private static string BuildSnapshotText(string petName, PetProfile snapshot, DateTimeOffset createdUtc,
        IReadOnlyList<JournalEntry> entries, JournalFilter? filter)
    {
        var journal = snapshot.Journal;
        var text = new StringBuilder();
        text.AppendLine(filter is null ? "DesktopPet 陪伴日记" : "DesktopPet 陪伴日记（筛选结果）");
        text.Append("宠物：").AppendLine(petName);
        text.Append("导出时间（UTC）：").AppendLine(UtcTime(createdUtc));
        text.AppendLine();
        if (filter is not null)
        {
            text.Append("筛选条件：").AppendLine(JournalQuery.Description(filter));
            text.Append("筛选基准本地日期：").AppendLine(filter.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            AppendCount(text, "筛选条目：", entries.Count);
            text.AppendLine();
        }
        text.AppendLine("当前照料状态");
        text.Append("亲密度：").Append(CareValue(snapshot.Affection)).AppendLine("/100");
        text.Append("饱腹度：").Append(CareValue(snapshot.Fullness)).AppendLine("/100");
        text.Append("精力：").Append(CareValue(snapshot.Energy)).AppendLine("/100");
        text.AppendLine();
        text.AppendLine("累计陪伴记录");
        AppendCount(text, "陪伴天数：", journal.VisitedDays.Count);
        AppendCount(text, "摸头次数：", journal.PettingCount);
        AppendCount(text, "喂食次数：", journal.FeedingCount);
        AppendCount(text, "玩耍次数：", journal.PlayCount);
        AppendCount(text, "回窝次数：", journal.HomeCount);
        AppendCount(text, "完成专注次数：", journal.FocusCompletedCount);
        AppendCount(text, filter is null ? "日记条目：" : "日记条目总数：", journal.Entries.Count);
        text.AppendLine();
        text.AppendLine("日记（本地日期 / UTC 时间，按时间顺序）");
        if (entries.Count == 0) text.AppendLine(filter is null ? "暂无日记。" : "暂无匹配日记。");
        foreach (var entry in entries)
        {
            text.Append(entry.LocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
                .Append(" / ").Append(UtcTime(entry.AtUtc))
                .Append(" [").Append(JournalQuery.KindLabel(entry.Kind)).Append("] ").AppendLine(entry.Text);
        }
        return text.ToString();
    }

    private static void AppendCount(StringBuilder text, string label, int count) =>
        text.Append(label).AppendLine(count.ToString(CultureInfo.InvariantCulture));

    private static string CareValue(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string UtcTime(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);

}
