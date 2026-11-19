using MyBlazorPlayground.Data.Nest;

namespace MyBlazorPlayground.Web.Nest;

internal sealed class NestJournal(NestEntryStore store, ILogger<NestJournal> logger) : WriteBehindJournal<Entry>(logger)
{
    protected override string Subject => "home entries";

    protected override Task WriteAsync(IReadOnlyList<Entry> batch, CancellationToken cancellationToken) =>
        store.InsertAsync([.. batch.Select(NestStorage.ToRow)], cancellationToken);
}
