using Dapper;

namespace MyBlazorPlayground.Data.Toys;

public sealed class CanvasStore(Database database)
{
    public Task<int> AppendAsync(IReadOnlyCollection<CanvasRow> rows, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return Task.FromResult(0);
        }

        return database.InTransactionAsync(async (connection, transaction) =>
        {
            var written = 0;
            foreach (var row in rows)
            {
                written += await connection.ExecuteAsync(new CommandDefinition(
                    row.IsReset
                        ? "INSERT INTO canvas_reset (id) VALUES (@Id) ON CONFLICT (id) DO NOTHING"
                        : "INSERT INTO canvas_stroke (id, points) VALUES (@Id, @Points) ON CONFLICT (id) DO NOTHING",
                    row,
                    transaction,
                    cancellationToken: cancellationToken));
            }

            return written;
        }, cancellationToken);
    }

    public async Task<CanvasState> LoadAsync(int maxStrokes, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var reset = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM canvas_reset ORDER BY id DESC LIMIT 1",
            cancellationToken: cancellationToken));
        var strokes = (await connection.QueryAsync<CanvasStrokeRecord>(new CommandDefinition(
            "SELECT id, points FROM canvas_stroke WHERE @reset::uuid IS NULL OR id > @reset::uuid ORDER BY id DESC LIMIT @maxStrokes",
            new { reset, maxStrokes },
            cancellationToken: cancellationToken))).AsList();
        return new CanvasState(reset, strokes);
    }
}
