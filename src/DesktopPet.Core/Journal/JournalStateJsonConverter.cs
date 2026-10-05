using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DesktopPet.Journal;

// The diary is optional nested data: one damaged field must not reset otherwise healthy care values.
public sealed class JournalStateJsonConverter : JsonConverter<JournalState>
{
    public override bool HandleNull => true;

    public override JournalState Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return new JournalState().Normalize();
        return new JournalState
        {
            PettingCount = Count(Get(root, nameof(JournalState.PettingCount), options)),
            FeedingCount = Count(Get(root, nameof(JournalState.FeedingCount), options)),
            PlayCount = Count(Get(root, nameof(JournalState.PlayCount), options)),
            HomeCount = Count(Get(root, nameof(JournalState.HomeCount), options)),
            FocusCompletedCount = Count(Get(root, nameof(JournalState.FocusCompletedCount), options)),
            VisitedDays = JournalState.BoundDays(Days(Get(root, nameof(JournalState.VisitedDays), options))),
            Entries = JournalState.BoundEntries(Entries(Get(root, nameof(JournalState.Entries), options), options)),
            LastActions = JournalState.BoundActions(Actions(Get(root, nameof(JournalState.LastActions), options), options))
        };
    }

    public override void Write(Utf8JsonWriter writer, JournalState value, JsonSerializerOptions options)
    {
        var state = (value ?? new JournalState()).Normalize();
        writer.WriteStartObject();
        writer.WriteNumber(Name(nameof(state.PettingCount), options), state.PettingCount);
        writer.WriteNumber(Name(nameof(state.FeedingCount), options), state.FeedingCount);
        writer.WriteNumber(Name(nameof(state.PlayCount), options), state.PlayCount);
        writer.WriteNumber(Name(nameof(state.HomeCount), options), state.HomeCount);
        writer.WriteNumber(Name(nameof(state.FocusCompletedCount), options), state.FocusCompletedCount);
        writer.WritePropertyName(Name(nameof(state.VisitedDays), options));
        JsonSerializer.Serialize(writer, state.VisitedDays, options);
        writer.WritePropertyName(Name(nameof(state.Entries), options));
        JsonSerializer.Serialize(writer, state.Entries, options);
        writer.WritePropertyName(Name(nameof(state.LastActions), options));
        JsonSerializer.Serialize(writer, state.LastActions, options);
        writer.WriteEndObject();
    }

    private static string Name(string value, JsonSerializerOptions options) => options.PropertyNamingPolicy?.ConvertName(value) ?? value;

    private static JsonElement Get(JsonElement element, string name, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object) return default;
        var comparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var expected = Name(name, options);
        foreach (var property in element.EnumerateObject())
            if (string.Equals(property.Name, expected, comparison)) return property.Value;
        return default;
    }

    private static int Count(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var count) || double.IsNaN(count) || count <= 0) return 0;
        return count >= JournalState.MaximumCounter ? JournalState.MaximumCounter : (int)count;
    }

    private static IEnumerable<DateOnly> Days(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in element.EnumerateArray())
            if (Date(item) is { } date) yield return date;
    }

    private static IEnumerable<JournalEntry> Entries(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Time(Get(item, nameof(JournalEntry.AtUtc), options)) is not { } time
                || Date(Get(item, nameof(JournalEntry.LocalDate), options)) is not { } date
                || EnumValue<JournalEntryKind>(Get(item, nameof(JournalEntry.Kind), options)) is not { } kind) continue;
            yield return new JournalEntry(time, date, kind, Text(Get(item, nameof(JournalEntry.Text), options)) ?? "",
                Text(Get(item, nameof(JournalEntry.AchievementId), options)));
        }
    }

    private static IEnumerable<JournalActionStamp> Actions(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Array) yield break;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || Time(Get(item, nameof(JournalActionStamp.AtUtc), options)) is not { } time
                || EnumValue<JournalAction>(Get(item, nameof(JournalActionStamp.Action), options)) is not { } action) continue;
            yield return new JournalActionStamp(action, time);
        }
    }

    private static DateOnly? Date(JsonElement value) => value.ValueKind == JsonValueKind.String
        && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
        ? date : null;

    private static DateTimeOffset? Time(JsonElement value) => value.ValueKind == JsonValueKind.String
        && value.TryGetDateTimeOffset(out var time) ? time : null;

    private static string? Text(JsonElement value) => value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static TEnum? EnumValue<TEnum>(JsonElement value) where TEnum : struct, Enum
    {
        TEnum parsed;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            parsed = (TEnum)Enum.ToObject(typeof(TEnum), number);
        else if (value.ValueKind == JsonValueKind.String && Enum.TryParse(value.GetString(), false, out parsed)) { }
        else return null;
        return Enum.IsDefined(parsed) ? parsed : null;
    }
}
