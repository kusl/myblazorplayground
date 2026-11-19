using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Messages;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal sealed class BoardJournal(BoardStore store, ILogger<BoardJournal> logger) : WriteBehindJournal<BoardMessage>(logger)
{
    protected override string Subject => "board messages";

    protected override Task WriteAsync(IReadOnlyList<BoardMessage> batch, CancellationToken cancellationToken) =>
        store.InsertAsync([.. batch.Select(ToyStorage.ToBoardRow)], cancellationToken);
}

internal sealed partial class BoardLoader(BoardStore store, BoardService board, ILogger<BoardLoader> logger) : IStateLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var records = await store.LoadAsync(board.Capacity, cancellationToken);
        LogLoaded(logger, board.Load(ToyStorage.ToMessages(records)));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Count} board messages")]
    private static partial void LogLoaded(ILogger logger, int count);
}
