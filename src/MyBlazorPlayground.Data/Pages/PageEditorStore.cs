using Dapper;

namespace MyBlazorPlayground.Data.Pages;

public sealed class PageEditorRecord
{
    public string PageKey { get; init; } = string.Empty;

    public Guid PersonId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public DateTime GrantedAt { get; init; }
}

public sealed class PageEditorStore(Database database)
{
    public async Task<bool> IsEditorAsync(Guid personId, IReadOnlyCollection<string> pageKeys, CancellationToken cancellationToken = default)
    {
        if (pageKeys.Count == 0)
        {
            return false;
        }

        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM page_editor WHERE person_id = @personId AND page_key = ANY(@keys))",
            new { personId, keys = pageKeys.ToArray() },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PageEditorRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<PageEditorRecord>(new CommandDefinition(
            """
            SELECT e.page_key, e.person_id, p.user_name, p.display_name, e.granted_at
            FROM page_editor e JOIN person p ON p.id = e.person_id
            ORDER BY e.page_key, p.user_name
            """,
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> GrantAsync(string pageKey, Guid personId, Guid grantedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO page_editor (page_key, person_id, granted_by) VALUES (@pageKey, @personId, @grantedBy) ON CONFLICT DO NOTHING",
            new { pageKey, personId, grantedBy },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> RevokeAsync(string pageKey, Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM page_editor WHERE page_key = @pageKey AND person_id = @personId",
            new { pageKey, personId },
            cancellationToken: cancellationToken)) > 0;
    }
}
