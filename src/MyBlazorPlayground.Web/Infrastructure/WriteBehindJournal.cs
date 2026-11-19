using System.Threading.Channels;

namespace MyBlazorPlayground.Web.Infrastructure;

internal abstract partial class WriteBehindJournal<T>(ILogger logger) : BackgroundService, IJournal<T>
{
    public const int BatchSize = 500;
    public const int Attempts = 5;

    private readonly Channel<T[]> _pending = Channel.CreateUnbounded<T[]>(new UnboundedChannelOptions { SingleReader = true });

    protected abstract string Subject { get; }

    public void Append(IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count > 0)
        {
            _pending.Writer.TryWrite([.. items]);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _pending.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected abstract Task WriteAsync(IReadOnlyList<T> batch, CancellationToken cancellationToken);

    protected virtual TimeSpan RetryDelay(int attempt) => TimeSpan.FromSeconds(attempt * 2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _pending.Reader;
        while (await WaitAsync(reader, stoppingToken))
        {
            var batch = new List<T>();
            while (batch.Count < BatchSize && reader.TryRead(out var items))
            {
                batch.AddRange(items);
            }

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await WriteAsync(batch, CancellationToken.None);
                    break;
                }
                catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException && attempt < Attempts)
                {
                    LogRetry(logger, batch.Count, Subject, attempt, exception.Message);
                    await Task.Delay(RetryDelay(attempt), CancellationToken.None);
                }
                catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException)
                {
                    LogLost(logger, batch.Count, Subject, exception.Message);
                    break;
                }
            }
        }
    }

    private static async Task<bool> WaitAsync(ChannelReader<T[]> reader, CancellationToken stoppingToken)
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not store {Count} {Subject} (attempt {Attempt}): {Reason}")]
    private static partial void LogRetry(ILogger logger, int count, string subject, int attempt, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Gave up storing {Count} {Subject}: {Reason}. Browsers still hold them and will offer them back.")]
    private static partial void LogLost(ILogger logger, int count, string subject, string reason);
}
