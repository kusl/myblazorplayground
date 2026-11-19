using System.Text.Json;
using MyBlazorPlayground.Data.Nest;

namespace MyBlazorPlayground.Web.Nest;

internal static class NestStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static NestEntryRow ToRow(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new NestEntryRow(
            entry.Id,
            entry.Kind,
            [.. entry.Keys],
            entry.Url,
            entry.Fields is null ? null : JsonSerializer.Serialize(entry.Fields, Json),
            entry.Text);
    }

    public static Entry? ToEntry(NestEntryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        IReadOnlyList<FieldValue>? fields = null;
        if (record.Fields is not null)
        {
            try
            {
                fields = JsonSerializer.Deserialize<List<FieldValue>>(record.Fields, Json);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        var entry = new Entry(record.Id, record.Kind, record.Keys, record.Url, fields, record.Text);
        return EntryRules.IsNormalized(entry) ? entry : null;
    }
}
