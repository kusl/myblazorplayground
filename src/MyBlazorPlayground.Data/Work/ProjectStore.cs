using Dapper;
using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Domain.Access;

namespace MyBlazorPlayground.Data.Work;

public sealed class ProjectRecord
{
    public Guid Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? DeletedAt { get; init; }

    public string? Role { get; init; }

    public long OpenItems { get; init; }

    public long MemberCount { get; init; }
}

public sealed class MemberRecord
{
    public Guid PersonId { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public DateTime AddedAt { get; init; }
}

public enum CreateProjectResult
{
    Created,
    KeyTaken,
}

public enum MemberChange
{
    Changed,
    NotFound,
    LastEditor,
    Unchanged,
}

public sealed class ProjectStore(Database database)
{
    private const string Select =
        """
        SELECT p.id, p.key, p.name, p.description, p.created_at, p.updated_at, p.deleted_at,
               m.role,
               (SELECT count(*) FROM work_item w WHERE w.project_id = p.id AND w.deleted_at IS NULL AND w.status <> 'done') AS open_items,
               (SELECT count(*) FROM project_member pm WHERE pm.project_id = p.id) AS member_count
        FROM project p
        LEFT JOIN project_member m ON m.project_id = p.id AND m.person_id = @personId
        """;

    public async Task<(CreateProjectResult Result, Guid Id)> CreateAsync(string key, string name, string description, Guid creatorId, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        var id = Guid.CreateVersion7();
        try
        {
            return await database.InTransactionAsync(async (connection, transaction) =>
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO project (id, key, name, description, created_by) VALUES (@id, @key, @name, @description, @creatorId)",
                    new { id, key, name, description, creatorId },
                    transaction,
                    cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO project_member (project_id, person_id, role, added_by) VALUES (@id, @creatorId, @role, @creatorId)",
                    new { id, creatorId, role = PageRoles.Editor },
                    transaction,
                    cancellationToken: cancellationToken));
                await AuditLog.WriteAsync(connection, transaction, audit with { SubjectId = id.ToString() }, cancellationToken);
                return (CreateProjectResult.Created, id);
            }, cancellationToken);
        }
        catch (Exception exception) when (Database.IsUniqueViolation(exception))
        {
            return (CreateProjectResult.KeyTaken, Guid.Empty);
        }
    }

    public async Task<IReadOnlyList<ProjectRecord>> ListAsync(Guid personId, bool everything, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<ProjectRecord>(new CommandDefinition(
            Select + " WHERE p.deleted_at IS NULL AND (@everything OR m.person_id IS NOT NULL) ORDER BY p.name",
            new { personId, everything },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<ProjectRecord?> FindAsync(string key, Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProjectRecord>(new CommandDefinition(
            Select + " WHERE p.key = @key",
            new { key, personId },
            cancellationToken: cancellationToken));
    }

    public async Task<ProjectRecord?> FindByIdAsync(Guid id, Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ProjectRecord>(new CommandDefinition(
            Select + " WHERE p.id = @id",
            new { id, personId },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, string name, string description, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE project SET name = @name, description = @description, updated_at = now() WHERE id = @id",
            new { id, name, description },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MemberRecord>> MembersAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<MemberRecord>(new CommandDefinition(
            """
            SELECT m.person_id, p.user_name, p.display_name, m.role, m.added_at
            FROM project_member m JOIN person p ON p.id = m.person_id
            WHERE m.project_id = @projectId
            ORDER BY m.role, p.display_name
            """,
            new { projectId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<MemberChange> SetMemberAsync(Guid projectId, string userName, string role, Guid actorId, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            var personId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                "SELECT id FROM person WHERE user_name = @userName AND disabled_at IS NULL",
                new { userName },
                transaction,
                cancellationToken: cancellationToken));
            if (personId is null)
            {
                return MemberChange.NotFound;
            }

            if (role != PageRoles.Editor && await IsLastEditorAsync(connection, transaction, projectId, personId.Value, cancellationToken))
            {
                return MemberChange.LastEditor;
            }

            var changed = await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO project_member (project_id, person_id, role, added_by) VALUES (@projectId, @personId, @role, @actorId)
                ON CONFLICT (project_id, person_id) DO UPDATE SET role = excluded.role
                WHERE project_member.role <> excluded.role
                """,
                new { projectId, personId, role, actorId },
                transaction,
                cancellationToken: cancellationToken));
            if (changed == 0)
            {
                return MemberChange.Unchanged;
            }

            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return MemberChange.Changed;
        }, cancellationToken);
    }

    public async Task<MemberChange> RemoveMemberAsync(Guid projectId, Guid personId, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            if (await IsLastEditorAsync(connection, transaction, projectId, personId, cancellationToken))
            {
                return MemberChange.LastEditor;
            }

            var removed = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM project_member WHERE project_id = @projectId AND person_id = @personId",
                new { projectId, personId },
                transaction,
                cancellationToken: cancellationToken));
            if (removed == 0)
            {
                return MemberChange.NotFound;
            }

            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return MemberChange.Changed;
        }, cancellationToken);
    }

    private static async Task<bool> IsLastEditorAsync(Npgsql.NpgsqlConnection connection, Npgsql.NpgsqlTransaction transaction, Guid projectId, Guid personId, CancellationToken cancellationToken) =>
        await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (SELECT 1 FROM project_member WHERE project_id = @projectId AND person_id = @personId AND role = 'editor')
               AND (SELECT count(*) FROM project_member WHERE project_id = @projectId AND role = 'editor') <= 1
            """,
            new { projectId, personId },
            transaction,
            cancellationToken: cancellationToken));
}
