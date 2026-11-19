using Dapper;
using Npgsql;

namespace MyBlazorPlayground.Data.Audit;

public sealed record AuditEntry(Guid? ActorId, string? ActorName, string Action, string SubjectType, string SubjectId, string Summary, string? Client = null);

public sealed class AuditRecord
{
    public Guid Id { get; init; }

    public DateTime OccurredAt { get; init; }

    public string? ActorName { get; init; }

    public string Action { get; init; } = string.Empty;

    public string SubjectType { get; init; } = string.Empty;

    public string SubjectId { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;
}

public sealed class AuditLog(Database database)
{
    private const string Insert =
        """
        INSERT INTO audit_event (id, actor_id, actor_name, action, subject_type, subject_id, summary, client)
        VALUES (@id, @ActorId, @ActorName, @Action, @SubjectType, @SubjectId, @Summary, @Client)
        """;

    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await WriteAsync(connection, null, entry, cancellationToken);
    }

    public static Task WriteAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, AuditEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(entry);
        return connection.ExecuteAsync(new CommandDefinition(
            Insert,
            new { id = Guid.CreateVersion7(), entry.ActorId, entry.ActorName, entry.Action, entry.SubjectType, entry.SubjectId, entry.Summary, entry.Client },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<AuditRecord>> ListAsync(int limit, Guid? before, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<AuditRecord>(new CommandDefinition(
            """
            SELECT id, occurred_at, actor_name, action, subject_type, subject_id, summary
            FROM audit_event
            WHERE @before::uuid IS NULL OR id < @before::uuid
            ORDER BY id DESC
            LIMIT @limit
            """,
            new { limit, before },
            cancellationToken: cancellationToken))).AsList();
    }
}
