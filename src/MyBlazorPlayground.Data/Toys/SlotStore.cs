using Dapper;

namespace MyBlazorPlayground.Data.Toys;

public sealed record SlotHistory(IReadOnlyList<ToyMessageRecord> Recent, IReadOnlyList<ToyMessageRecord> Winners, IReadOnlyList<ToyMessageRecord> Jackpots);

public sealed class SlotStore(Database database)
{
    public async Task<int> InsertAsync(IReadOnlyCollection<SlotSpinRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO slot_spin (id, text, winner, jackpot)
            SELECT * FROM unnest(@ids::uuid[], @texts::text[], @winners::boolean[], @jackpots::boolean[])
            ON CONFLICT (id) DO NOTHING
            """,
            new
            {
                ids = rows.Select(row => row.Id).ToArray(),
                texts = rows.Select(row => row.Text).ToArray(),
                winners = rows.Select(row => row.Winner).ToArray(),
                jackpots = rows.Select(row => row.Jackpot).ToArray(),
            },
            cancellationToken: cancellationToken));
    }

    public async Task<SlotHistory> LoadAsync(int recent, int winners, int jackpots, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        async Task<IReadOnlyList<ToyMessageRecord>> QueryAsync(string filter, int limit) =>
            (await connection.QueryAsync<ToyMessageRecord>(new CommandDefinition(
                $"SELECT id, text FROM slot_spin {filter} ORDER BY id DESC LIMIT @limit",
                new { limit },
                cancellationToken: cancellationToken))).AsList();

        return new SlotHistory(
            await QueryAsync(string.Empty, recent),
            await QueryAsync("WHERE winner", winners),
            await QueryAsync("WHERE jackpot", jackpots));
    }
}
