using Dapper;

namespace MyBlazorPlayground.Data.Work;

public sealed class CommentRecord
{
    public Guid Id { get; init; }

    public Guid WorkItemId { get; init; }

    public Guid? AuthorId { get; init; }

    public string? AuthorName { get; init; }

    public string Body { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? EditedAt { get; init; }
}

public sealed class CommentStore(Database database)
{
    private const string Select =
        """
        SELECT c.id, c.work_item_id, c.author_id, p.display_name AS author_name, c.body, c.created_at, c.edited_at
        FROM work_item_comment c LEFT JOIN person p ON p.id = c.author_id
        """;

    public async Task<IReadOnlyList<CommentRecord>> ListAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<CommentRecord>(new CommandDefinition(
            Select + " WHERE c.work_item_id = @itemId AND c.deleted_at IS NULL ORDER BY c.created_at",
            new { itemId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<CommentRecord?> FindAsync(Guid itemId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CommentRecord>(new CommandDefinition(
            Select + " WHERE c.work_item_id = @itemId AND c.id = @id AND c.deleted_at IS NULL",
            new { itemId, id },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid> AddAsync(Guid itemId, Guid authorId, string body, CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item_comment (id, work_item_id, author_id, body) VALUES (@id, @itemId, @authorId, @body);
            UPDATE work_item SET updated_at = now() WHERE id = @itemId;
            """,
            new { id, itemId, authorId, body },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<bool> EditAsync(Guid itemId, Guid id, string body, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item_comment SET body = @body, edited_at = now() WHERE id = @id AND work_item_id = @itemId AND deleted_at IS NULL AND body <> @body",
            new { itemId, id, body },
            cancellationToken: cancellationToken)) > 0;
    }
}
