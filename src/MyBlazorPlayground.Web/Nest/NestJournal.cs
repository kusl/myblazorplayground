using System.Threading.Channels;
using MyBlazorPlayground.Data.Nest;

namespace MyBlazorPlayground.Web.Nest;

internal sealed partial class NestJournal(NestEntryStore store, ILogger<NestJournal> logger) : BackgroundService, INestJournal
{
    private readonly Channel<Entry[]> _pending = Channel.CreateUnbounded<Entry[]>(new UnboundedChannelOptions { SingleReader = true });

    public void Append(IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count > 0)
        {
            _pending.Writer.TryWrite([.. entries]);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _pending.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _pending.Reader;
        while (await WaitAsync(reader, stoppingToken))
        {
            var batch = new List<NestEntryRow>();
            while (batch.Count < 500 && reader.TryRead(out var entries))
            {
                batch.AddRange(entries.Select(NestStorage.ToRow));
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await store.InsertAsync(batch, CancellationToken.None);
                    break;
                }
                catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException && attempt < 5)
                {
                    LogRetry(logger, batch.Count, attempt, exception.Message);
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2), CancellationToken.None);
                }
                catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException)
                {
                    LogLost(logger, batch.Count, exception.Message);
                    break;
                }
            }
        }
    }

    private static async Task<bool> WaitAsync(ChannelReader<Entry[]> reader, CancellationToken stoppingToken)
    {
        try
        {
            return await reader.WaitToReadAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return reader.TryPeek(out _);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not store {Count} home entries (attempt {Attempt}): {Reason}")]
    private static partial void LogRetry(ILogger logger, int count, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Gave up storing {Count} home entries: {Reason}. Browsers still hold them and will offer them back.")]
    private static partial void LogLost(ILogger logger, int count, string reason);
}
