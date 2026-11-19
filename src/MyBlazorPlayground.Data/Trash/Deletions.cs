using Dapper;
using MyBlazorPlayground.Data.Audit;

namespace MyBlazorPlayground.Data.Trash;

public enum Subject
{
    Project,
    Sprint,
    WorkItem,
    Comment,
    Attachment,
    NestEntry,
}

public sealed class TrashRecord
{
    public string Type { get; init; } = string.Empty;

    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? ProjectKey { get; init; }

    public DateTime DeletedAt { get; init; }

    public string? DeletedByName { get; init; }

    public Guid? ProjectId { get; init; }

    public Subject Subject => SubjectNames.Parse(Type);
}

public static class SubjectNames
{
    public static string Of(Subject subject) => subject switch
    {
        Subject.Project => "project",
        Subject.Sprint => "sprint",
        Subject.WorkItem => "work_item",
        Subject.Comment => "comment",
        Subject.Attachment => "attachment",
        Subject.NestEntry => "nest_entry",
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };

    public static Subject Parse(string? value) => value switch
    {
        "project" => Subject.Project,
        "sprint" => Subject.Sprint,
        "work_item" => Subject.WorkItem,
        "comment" => Subject.Comment,
        "attachment" => Subject.Attachment,
        "nest_entry" => Subject.NestEntry,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    public static bool TryParse(string? value, out Subject subject)
    {
        try
        {
            subject = Parse(value);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            subject = default;
            return false;
        }
    }

    public static string Label(Subject subject) => subject switch
    {
        Subject.Project => "Project",
        Subject.Sprint => "Sprint",
        Subject.WorkItem => "Work item",
        Subject.Comment => "Comment",
        Subject.Attachment => "Attachment",
        Subject.NestEntry => "Home entry",
        _ => "Record",
    };

    internal static string Table(Subject subject) => subject switch
    {
        Subject.Project => "project",
        Subject.Sprint => "sprint",
        Subject.WorkItem => "work_item",
        Subject.Comment => "work_item_comment",
        Subject.Attachment => "attachment",
        Subject.NestEntry => "nest_entry",
        _ => throw new ArgumentOutOfRangeException(nameof(subject)),
    };
}

public sealed class Deletions(Database database)
{
    private const string TrashSelect =
        """
        SELECT * FROM (
            SELECT 'project' AS type, p.id, p.key || ' ' || p.name AS title, p.key AS project_key, p.deleted_at, d.display_name AS deleted_by_name, p.id AS project_id
            FROM project p LEFT JOIN person d ON d.id = p.deleted_by WHERE p.deleted_at IS NOT NULL
            UNION ALL
            SELECT 'sprint', s.id, s.name, p.key, s.deleted_at, d.display_name, s.project_id
            FROM sprint s JOIN project p ON p.id = s.project_id LEFT JOIN person d ON d.id = s.deleted_by WHERE s.deleted_at IS NOT NULL
            UNION ALL
            SELECT 'work_item', w.id, '#' || w.number || ' ' || w.title, p.key, w.deleted_at, d.display_name, w.project_id
            FROM work_item w JOIN project p ON p.id = w.project_id LEFT JOIN person d ON d.id = w.deleted_by WHERE w.deleted_at IS NOT NULL
            UNION ALL
            SELECT 'comment', c.id, '#' || w.number || ': ' || left(c.body, 80), p.key, c.deleted_at, d.display_name, w.project_id
            FROM work_item_comment c JOIN work_item w ON w.id = c.work_item_id JOIN project p ON p.id = w.project_id
            LEFT JOIN person d ON d.id = c.deleted_by WHERE c.deleted_at IS NOT NULL
            UNION ALL
            SELECT 'attachment', f.id, f.file_name, p.key, f.deleted_at, d.display_name, f.project_id
            FROM attachment f JOIN project p ON p.id = f.project_id LEFT JOIN person d ON d.id = f.deleted_by WHERE f.deleted_at IS NOT NULL
            UNION ALL
            SELECT 'nest_entry', n.id, n.kind || ': ' || left(coalesce(n.text, n.url, array_to_string(n.keys, ' ')), 80), NULL, n.deleted_at, d.display_name, NULL
            FROM nest_entry n LEFT JOIN person d ON d.id = n.deleted_by WHERE n.deleted_at IS NOT NULL AND n.purged_at IS NULL
        ) t
        """;

    public async Task<IReadOnlyList<TrashRecord>> ListAsync(Guid? projectId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<TrashRecord>(new CommandDefinition(
            TrashSelect + " WHERE @projectId::uuid IS NULL OR (t.project_id = @projectId AND t.type <> 'project') ORDER BY t.deleted_at DESC LIMIT @limit",
            new { projectId, limit },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<TrashRecord?> FindAsync(Subject subject, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<TrashRecord>(new CommandDefinition(
            TrashSelect + " WHERE t.type = @type AND t.id = @id",
            new { type = SubjectNames.Of(subject), id },
            cancellationToken: cancellationToken));
    }

    public Task<bool> SoftDeleteAsync(Subject subject, Guid id, Guid actorId, AuditEntry audit, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            $"UPDATE {SubjectNames.Table(subject)} SET deleted_at = now(), deleted_by = @actorId WHERE id = @id AND deleted_at IS NULL",
            new { id, actorId },
            audit,
            cancellationToken);

    public Task<bool> RestoreAsync(Subject subject, Guid id, AuditEntry audit, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            subject == Subject.NestEntry
                ? "UPDATE nest_entry SET deleted_at = NULL, deleted_by = NULL WHERE id = @id AND deleted_at IS NOT NULL AND purged_at IS NULL"
                : $"UPDATE {SubjectNames.Table(subject)} SET deleted_at = NULL, deleted_by = NULL WHERE id = @id AND deleted_at IS NOT NULL",
            new { id },
            audit,
            cancellationToken);

    public Task<bool> HardDeleteAsync(Subject subject, Guid id, AuditEntry audit, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            subject == Subject.NestEntry
                ? "UPDATE nest_entry SET keys = '{}', url = NULL, fields = NULL, text = NULL, purged_at = now() WHERE id = @id AND deleted_at IS NOT NULL AND purged_at IS NULL"
                : $"DELETE FROM {SubjectNames.Table(subject)} WHERE id = @id AND deleted_at IS NOT NULL",
            new { id },
            audit,
            cancellationToken);

    private async Task<bool> ChangeAsync(string sql, object parameters, AuditEntry audit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            if (changed == 0)
            {
                return false;
            }

            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return true;
        }, cancellationToken);
    }
}
