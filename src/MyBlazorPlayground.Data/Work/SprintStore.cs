using System.Globalization;
using Dapper;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Data.Work;

public sealed class SprintRecord
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Goal { get; init; } = string.Empty;

    public string? StartsOn { get; init; }

    public string? EndsOn { get; init; }

    public string State { get; init; } = SprintStates.Planned;

    public long ItemCount { get; init; }

    public long DoneCount { get; init; }

    public long Points { get; init; }

    public long DonePoints { get; init; }

    public DateOnly? Starts => Parse(StartsOn);

    public DateOnly? Ends => Parse(EndsOn);

    private static DateOnly? Parse(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}

public sealed record SprintDraft(string Name, string Goal, DateOnly? StartsOn, DateOnly? EndsOn);

public enum SprintChange
{
    Changed,
    NotFound,
    AnotherIsActive,
    WrongState,
}

public sealed class SprintStore(Database database)
{
    private const string Select =
        """
        SELECT s.id, s.project_id, s.name, s.goal,
               to_char(s.starts_on, 'YYYY-MM-DD') AS starts_on, to_char(s.ends_on, 'YYYY-MM-DD') AS ends_on, s.state,
               count(w.id) AS item_count,
               count(w.id) FILTER (WHERE w.status = 'done') AS done_count,
               coalesce(sum(w.points), 0) AS points,
               coalesce(sum(w.points) FILTER (WHERE w.status = 'done'), 0) AS done_points
        FROM sprint s
        LEFT JOIN work_item w ON w.sprint_id = s.id AND w.deleted_at IS NULL
        """;

    public async Task<IReadOnlyList<SprintRecord>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<SprintRecord>(new CommandDefinition(
            Select + """
                 WHERE s.project_id = @projectId AND s.deleted_at IS NULL
                GROUP BY s.id
                ORDER BY CASE s.state WHEN 'active' THEN 0 WHEN 'planned' THEN 1 ELSE 2 END, s.starts_on NULLS LAST, s.created_at
                """,
            new { projectId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SprintRecord?> FindAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SprintRecord>(new CommandDefinition(
            Select + " WHERE s.project_id = @projectId AND s.id = @id AND s.deleted_at IS NULL GROUP BY s.id",
            new { projectId, id },
            cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(Guid projectId, SprintDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var id = Guid.CreateVersion7();
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO sprint (id, project_id, name, goal, starts_on, ends_on)
            VALUES (@id, @projectId, @Name, @Goal, @starts::date, @ends::date)
            """,
            new { id, projectId, draft.Name, draft.Goal, starts = Text(draft.StartsOn), ends = Text(draft.EndsOn) },
            cancellationToken: cancellationToken));
        return id;
    }

    public async Task<bool> UpdateAsync(Guid projectId, Guid id, SprintDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sprint SET name = @Name, goal = @Goal, starts_on = @starts::date, ends_on = @ends::date
            WHERE id = @id AND project_id = @projectId AND deleted_at IS NULL
            """,
            new { id, projectId, draft.Name, draft.Goal, starts = Text(draft.StartsOn), ends = Text(draft.EndsOn) },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<SprintChange> StartAsync(Guid projectId, Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken);
            var changed = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sprint SET state = 'active', started_at = now(), starts_on = coalesce(starts_on, current_date)
                WHERE id = @id AND project_id = @projectId AND state = 'planned' AND deleted_at IS NULL
                """,
                new { id, projectId },
                cancellationToken: cancellationToken));
            return changed > 0 ? SprintChange.Changed : SprintChange.WrongState;
        }
        catch (Exception exception) when (Database.IsUniqueViolation(exception))
        {
            return SprintChange.AnotherIsActive;
        }
    }

    public async Task<SprintChange> CompleteAsync(Guid projectId, Guid id, Guid? moveOpenTo, CancellationToken cancellationToken = default) =>
        await database.InTransactionAsync(async (connection, transaction) =>
        {
            var changed = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE sprint SET state = 'completed', completed_at = now(), ends_on = coalesce(ends_on, current_date)
                WHERE id = @id AND project_id = @projectId AND state = 'active' AND deleted_at IS NULL
                """,
                new { id, projectId },
                transaction,
                cancellationToken: cancellationToken));
            if (changed == 0)
            {
                return SprintChange.WrongState;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE work_item SET sprint_id = (
                    SELECT t.id FROM sprint t WHERE t.id = @moveOpenTo AND t.project_id = @projectId AND t.state = 'planned' AND t.deleted_at IS NULL),
                    status = CASE WHEN @moveOpenTo::uuid IS NULL THEN 'backlog' ELSE status END,
                    updated_at = now()
                WHERE sprint_id = @id AND status <> 'done' AND deleted_at IS NULL
                """,
                new { id, projectId, moveOpenTo },
                transaction,
                cancellationToken: cancellationToken));
            return SprintChange.Changed;
        }, cancellationToken);

    private static string? Text(DateOnly? date) => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
