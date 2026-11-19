namespace MyBlazorPlayground.Data.Toys;

public sealed class ToyMessageRecord
{
    public Guid Id { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Meta { get; init; }
}

public sealed record BoardMessageRow(Guid Id, string Text, string? Meta);

public sealed record SlotSpinRow(Guid Id, string Text, bool Winner, bool Jackpot);

public sealed record LatencyRunRow(Guid Id, string Text, int? BrowserMedian, int? ServerMedian);

public sealed record CanvasRow(Guid Id, int[]? Points)
{
    public bool IsReset => Points is null;
}

public sealed record CanvasState(Guid? Reset, IReadOnlyList<CanvasStrokeRecord> Strokes);

public sealed class CanvasStrokeRecord
{
    public Guid Id { get; init; }

    public int[] Points { get; init; } = [];
}

public sealed record PostcardImageRow(string ContentType, int Width, int Height, byte[] Data);

public sealed record PostcardRow(Guid Id, string Text, string? Meta, IReadOnlyList<PostcardImageRow> Images);
