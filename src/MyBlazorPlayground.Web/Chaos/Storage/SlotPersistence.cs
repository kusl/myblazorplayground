using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Messages;
using MyBlazorPlayground.Web.Chaos.Slots;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal sealed class SlotJournal(SlotStore store, ILogger<SlotJournal> logger) : WriteBehindJournal<BoardMessage>(logger)
{
    protected override string Subject => "slot spins";

    protected override Task WriteAsync(IReadOnlyList<BoardMessage> batch, CancellationToken cancellationToken) =>
        store.InsertAsync([.. batch.Select(ToyStorage.ToSlotRow)], cancellationToken);
}

internal sealed partial class SlotLoader(SlotStore store, SlotService slots, ILogger<SlotLoader> logger) : IStateLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var history = await store.LoadAsync(slots.Capacity, slots.WinnerCapacity, slots.JackpotCapacity, cancellationToken);
        LogLoaded(logger, slots.Load(ToyStorage.ToMessages(history.Recent), ToyStorage.ToMessages(history.Winners), ToyStorage.ToMessages(history.Jackpots)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} slot spins")]
    private static partial void LogLoaded(ILogger logger, int count);
}
