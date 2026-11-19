using Dapper;

namespace MyBlazorPlayground.Data.Toys;

public sealed class PostcardStore(Database database)
{
    public Task<int> InsertAsync(IReadOnlyCollection<PostcardRow> rows, CancellationToken cancellationToken = default)
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
                var inserted = await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO postcard (id, text, meta) VALUES (@Id, @Text, @Meta::jsonb) ON CONFLICT (id) DO NOTHING",
                    new { row.Id, row.Text, row.Meta },
                    transaction,
                    cancellationToken: cancellationToken));
                if (inserted == 0)
                {
                    continue;
                }

                written++;
                for (var position = 0; position < row.Images.Count; position++)
                {
                    var image = row.Images[position];
                    await connection.ExecuteAsync(new CommandDefinition(
                        """
                        INSERT INTO postcard_image (postcard_id, position, content_type, width, height, data)
                        VALUES (@id, @position, @ContentType, @Width, @Height, @Data)
                        """,
                        new { id = row.Id, position, image.ContentType, image.Width, image.Height, image.Data },
                        transaction,
                        cancellationToken: cancellationToken));
                }
            }

            return written;
        }, cancellationToken);
    }

    public async Task<PostcardRow?> LatestAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var card = await connection.QuerySingleOrDefaultAsync<ToyMessageRecord>(new CommandDefinition(
            "SELECT id, text, meta::text AS meta FROM postcard ORDER BY id DESC LIMIT 1",
            cancellationToken: cancellationToken));
        if (card is null)
        {
            return null;
        }

        var images = (await connection.QueryAsync<PostcardImageRow>(new CommandDefinition(
            "SELECT content_type AS ContentType, width, height, data FROM postcard_image WHERE postcard_id = @Id ORDER BY position",
            new { card.Id },
            cancellationToken: cancellationToken))).AsList();
        return new PostcardRow(card.Id, card.Text, card.Meta, images);
    }
}
