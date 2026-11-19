using Dapper;

namespace MyBlazorPlayground.Data.Toys;

public sealed class BoardStore(Database database)
{
    public async Task<int> InsertAsync(IReadOnlyCollection<BoardMessageRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO board_message (id, text, meta)
            SELECT id, text, meta::jsonb FROM unnest(@ids::uuid[], @texts::text[], @metas::text[]) AS row (id, text, meta)
            ON CONFLICT (id) DO NOTHING
            """,
            new
            {
                ids = rows.Select(row => row.Id).ToArray(),
                texts = rows.Select(row => row.Text).ToArray(),
                metas = rows.Select(row => row.Meta).ToArray(),
            },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ToyMessageRecord>> LoadAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<ToyMessageRecord>(new CommandDefinition(
            "SELECT id, text, meta::text AS meta FROM board_message ORDER BY id DESC LIMIT @limit",
            new { limit },
            cancellationToken: cancellationToken))).AsList();
    }
}
