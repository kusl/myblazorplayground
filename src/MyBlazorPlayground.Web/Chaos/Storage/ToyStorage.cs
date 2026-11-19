using System.Text.Json;
using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Drawing;
using MyBlazorPlayground.Web.Chaos.Latency;
using MyBlazorPlayground.Web.Chaos.Messages;
using MyBlazorPlayground.Web.Chaos.Postcards;
using MyBlazorPlayground.Web.Chaos.Slots;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal static class ToyStorage
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? MetaToJson(IReadOnlyList<MetaEntry>? meta) =>
        meta is { Count: > 0 } ? JsonSerializer.Serialize(meta, Json) : null;

    public static bool TryMetaFromJson(string? json, out IReadOnlyList<MetaEntry>? meta)
    {
        meta = null;
        if (string.IsNullOrEmpty(json))
        {
            return true;
        }

        try
        {
            meta = JsonSerializer.Deserialize<List<MetaEntry>>(json, Json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static BoardMessageRow ToBoardRow(BoardMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new BoardMessageRow(message.Id, message.Text, MetaToJson(message.Meta));
    }

    public static SlotSpinRow ToSlotRow(BoardMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return new SlotSpinRow(message.Id, message.Text, SlotRules.Classify(message.Text) is not SlotWin.None, SlotRules.IsJackpot(message.Text));
    }

    public static LatencyRunRow ToLatencyRow(BoardMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var rank = LatencyRun.Rank(message.Text);
        return new LatencyRunRow(message.Id, message.Text, rank.Browser, rank.Server);
    }

    public static CanvasRow ToCanvasRow(CanvasEvent change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return new CanvasRow(change.Id, change.Points);
    }

    public static PostcardRow ToPostcardRow(StoredPostcard postcard)
    {
        ArgumentNullException.ThrowIfNull(postcard);
        return new PostcardRow(
            postcard.Card.Id,
            postcard.Card.Text,
            MetaToJson(postcard.Card.Meta),
            [.. postcard.Images.Select(image => new PostcardImageRow(image.Info.Type, image.Info.Width, image.Info.Height, image.Bytes))]);
    }

    public static BoardMessage? ToMessage(ToyMessageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return TryMetaFromJson(record.Meta, out var meta) ? new BoardMessage(record.Id, record.Text, meta) : null;
    }

    public static IEnumerable<BoardMessage> ToMessages(IEnumerable<ToyMessageRecord> records) =>
        records.Select(ToMessage).OfType<BoardMessage>();
}
