using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Latency;
using MyBlazorPlayground.Web.Chaos.Messages;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal sealed class LatencyJournal(LatencyStore store, ILogger<LatencyJournal> logger) : WriteBehindJournal<BoardMessage>(logger)
{
    protected override string Subject => "latency runs";

    protected override Task WriteAsync(IReadOnlyList<BoardMessage> batch, CancellationToken cancellationToken) =>
        store.InsertAsync([.. batch.Select(ToyStorage.ToLatencyRow)], cancellationToken);
}

internal sealed partial class LatencyLoader(LatencyStore store, LatencyService latency, ILogger<LatencyLoader> logger) : IStateLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var history = await store.LoadAsync(latency.Capacity, latency.RankCapacity, latency.JackpotCapacity, LatencyLeaders.FastBelow, LatencyLeaders.SlowFrom, cancellationToken);
        LogLoaded(logger, latency.Load(ToyStorage.ToMessages(history.Recent), ToyStorage.ToMessages(history.Leaders)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} latency runs")]
    private static partial void LogLoaded(ILogger logger, int count);
}
