using Microsoft.Extensions.Options;
using MyBlazorPlayground.Data.Nest;

namespace MyBlazorPlayground.Web.Nest;

internal sealed partial class NestLoader(NestEntryStore store, NestService service, IOptions<NestOptions> options, ILogger<NestLoader> logger)
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var records = await store.LoadAsync(options.Value.Capacity, cancellationToken);
        var deleted = records.Where(record => record.DeletedAt is not null).Select(record => record.Id).ToList();
        var live = new List<Entry>();
        var skipped = 0;
        foreach (var record in records.Where(record => record.DeletedAt is null))
        {
            if (NestStorage.ToEntry(record) is { } entry)
            {
                live.Add(entry);
            }
            else
            {
                skipped++;
            }
        }

        service.Load(live, deleted);
        LogLoaded(logger, live.Count, deleted.Count, skipped);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded {Live} home entries ({Deleted} deleted, {Skipped} unreadable)")]
    private static partial void LogLoaded(ILogger logger, int live, int deleted, int skipped);
}
