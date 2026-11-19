using Dapper;

namespace MyBlazorPlayground.Data.Nest;

public sealed class NestEntryRecord
{
    public Guid Id { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string[] Keys { get; init; } = [];

    public string? Url { get; init; }

    public string? Fields { get; init; }

    public string? Text { get; init; }

    public DateTime? DeletedAt { get; init; }
}

public sealed record NestEntryRow(Guid Id, string Kind, string[] Keys, string? Url, string? Fields, string? Text);

public sealed class NestEntryStore(Database database)
{
    public async Task<IReadOnlyList<NestEntryRecord>> LoadAsync(int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<NestEntryRecord>(new CommandDefinition(
            """
            (SELECT id, kind, keys, url, fields::text AS fields, text, deleted_at FROM nest_entry WHERE deleted_at IS NULL ORDER BY id DESC LIMIT @limit)
            UNION ALL
            (SELECT id, kind, keys, NULL, NULL, NULL, deleted_at FROM nest_entry WHERE deleted_at IS NOT NULL)
            """,
            new { limit },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<int> InsertAsync(IReadOnlyCollection<NestEntryRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return 0;
        }

        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            var inserted = 0;
            foreach (var row in rows)
            {
                inserted += await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO nest_entry (id, kind, keys, url, fields, text)
                    VALUES (@Id, @Kind, @Keys, @Url, @Fields::jsonb, @Text)
                    ON CONFLICT (id) DO NOTHING
                    """,
                    row,
                    transaction,
                    cancellationToken: cancellationToken));
            }

            return inserted;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<NestEntryRecord>> ForKeysAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<NestEntryRecord>(new CommandDefinition(
            "SELECT id, kind, keys, url, fields::text AS fields, text, deleted_at FROM nest_entry WHERE keys && @keys ORDER BY id DESC LIMIT 2000",
            new { keys = keys.ToArray() },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<NestEntryRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<NestEntryRecord>(new CommandDefinition(
            "SELECT id, kind, keys, url, fields::text AS fields, text, deleted_at FROM nest_entry WHERE id = @id",
            new { id },
            cancellationToken: cancellationToken));
    }
}
