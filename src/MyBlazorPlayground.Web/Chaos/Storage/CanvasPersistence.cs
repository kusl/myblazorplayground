using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Drawing;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal sealed class CanvasJournal(CanvasStore store, ILogger<CanvasJournal> logger) : WriteBehindJournal<CanvasEvent>(logger)
{
    protected override string Subject => "canvas strokes and resets";

    protected override Task WriteAsync(IReadOnlyList<CanvasEvent> batch, CancellationToken cancellationToken) =>
        store.AppendAsync([.. batch.Select(ToyStorage.ToCanvasRow)], cancellationToken);
}

internal sealed partial class CanvasLoader(CanvasStore store, CanvasService canvas, ILogger<CanvasLoader> logger) : IStateLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var state = await store.LoadAsync(canvas.MaxPoints, cancellationToken);
        LogLoaded(logger, canvas.Load(state.Reset, state.Strokes.Select(stroke => new StrokeOffer(stroke.Id, stroke.Points))));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} canvas strokes")]
    private static partial void LogLoaded(ILogger logger, int count);
}
