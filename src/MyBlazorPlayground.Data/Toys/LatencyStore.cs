using Dapper;

namespace MyBlazorPlayground.Data.Toys;

public sealed record LatencyHistory(IReadOnlyList<ToyMessageRecord> Recent, IReadOnlyList<ToyMessageRecord> Leaders);

public sealed class LatencyStore(Database database)
{
    public async Task<int> InsertAsync(IReadOnlyCollection<LatencyRunRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO latency_run (id, text, browser_median, server_median)
            SELECT * FROM unnest(@ids::uuid[], @texts::text[], @browser::integer[], @server::integer[])
            ON CONFLICT (id) DO NOTHING
            """,
            new
            {
                ids = rows.Select(row => row.Id).ToArray(),
                texts = rows.Select(row => row.Text).ToArray(),
                browser = rows.Select(row => row.BrowserMedian).ToArray(),
                server = rows.Select(row => row.ServerMedian).ToArray(),
            },
            cancellationToken: cancellationToken));
    }

    public async Task<LatencyHistory> LoadAsync(int recent, int ranks, int jackpots, int fastBelow, int slowFrom, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var latest = (await connection.QueryAsync<ToyMessageRecord>(new CommandDefinition(
            "SELECT id, text FROM latency_run ORDER BY id DESC LIMIT @recent",
            new { recent },
            cancellationToken: cancellationToken))).AsList();
        var leaders = (await connection.QueryAsync<ToyMessageRecord>(new CommandDefinition(
            """
            SELECT DISTINCT id, text FROM (
                (SELECT id, text FROM latency_run WHERE browser_median IS NOT NULL ORDER BY browser_median, id LIMIT @ranks)
                UNION ALL
                (SELECT id, text FROM latency_run WHERE browser_median IS NOT NULL ORDER BY browser_median DESC, id LIMIT @ranks)
                UNION ALL
                (SELECT id, text FROM latency_run WHERE server_median IS NOT NULL ORDER BY server_median, id LIMIT @ranks)
                UNION ALL
                (SELECT id, text FROM latency_run WHERE server_median IS NOT NULL ORDER BY server_median DESC, id LIMIT @ranks)
                UNION ALL
                (SELECT id, text FROM latency_run WHERE browser_median < @fastBelow OR server_median < @fastBelow ORDER BY id DESC LIMIT @jackpots)
                UNION ALL
                (SELECT id, text FROM latency_run WHERE browser_median >= @slowFrom OR server_median >= @slowFrom ORDER BY id DESC LIMIT @jackpots)
            ) AS leader
            ORDER BY id
            """,
            new { ranks, jackpots, fastBelow, slowFrom },
            cancellationToken: cancellationToken))).AsList();
        return new LatencyHistory(latest, leaders);
    }
}
