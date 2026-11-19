using Dapper;
using Npgsql;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Data.Work;

public sealed class WorkItemSummary
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public string ProjectKey { get; init; } = string.Empty;

    public int Number { get; init; }

    public string Kind { get; init; } = WorkItemKinds.Task;

    public string Title { get; init; } = string.Empty;

    public string Status { get; init; } = WorkItemStatuses.Backlog;

    public int Priority { get; init; }

    public int? Points { get; init; }

    public Guid? AssigneeId { get; init; }

    public string? AssigneeName { get; init; }

    public Guid? SprintId { get; init; }

    public string? SprintName { get; init; }

    public int? ParentNumber { get; init; }

    public string? ParentTitle { get; init; }

    public long Rank { get; init; }

    public DateTime UpdatedAt { get; init; }

    public long CommentCount { get; init; }

    public long AttachmentCount { get; init; }
}

public sealed class WorkItemRecord
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public int Number { get; init; }

    public string Kind { get; init; } = WorkItemKinds.Task;

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Status { get; init; } = WorkItemStatuses.Backlog;

    public int Priority { get; init; }

    public int? Points { get; init; }

    public Guid? AssigneeId { get; init; }

    public string? AssigneeName { get; init; }

    public Guid? ReporterId { get; init; }

    public string? ReporterName { get; init; }

    public Guid? SprintId { get; init; }

    public string? SprintName { get; init; }

    public Guid? ParentId { get; init; }

    public int? ParentNumber { get; init; }

    public string? ParentTitle { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public DateTime? DeletedAt { get; init; }

    public WorkItemFields Fields => new(Kind, Title, Description, Status, Priority, Points, AssigneeId, SprintId, ParentId);
}

public sealed class WorkItemEventRecord
{
    public DateTime OccurredAt { get; init; }

    public string? ActorName { get; init; }

    public string Field { get; init; } = string.Empty;

    public string? OldValue { get; init; }

    public string? NewValue { get; init; }
}

public sealed record WorkItemFields(
    string Kind,
    string Title,
    string Description,
    string Status,
    int Priority,
    int? Points,
    Guid? AssigneeId,
    Guid? SprintId,
    Guid? ParentId);

public sealed record WorkItemQuery(
    Guid ProjectId,
    Guid? SprintId = null,
    bool BacklogOnly = false,
    bool IncludeDone = true,
    string? Kind = null,
    Guid? AssigneeId = null,
    string? Text = null,
    int Limit = 500);

public sealed class WorkItemStore(Database database)
{
    private const long RankStep = 1024;

    private const string SummarySelect =
        """
        SELECT w.id, w.project_id, p.key AS project_key, w.number, w.kind, w.title, w.status, w.priority, w.points,
               w.assignee_id, a.display_name AS assignee_name,
               s.id AS sprint_id, s.name AS sprint_name,
               pw.number AS parent_number, pw.title AS parent_title,
               w.rank, w.updated_at,
               (SELECT count(*) FROM work_item_comment c WHERE c.work_item_id = w.id AND c.deleted_at IS NULL) AS comment_count,
               (SELECT count(*) FROM attachment f WHERE f.work_item_id = w.id AND f.deleted_at IS NULL AND f.complete) AS attachment_count
        FROM work_item w
        JOIN project p ON p.id = w.project_id
        LEFT JOIN person a ON a.id = w.assignee_id
        LEFT JOIN sprint s ON s.id = w.sprint_id AND s.deleted_at IS NULL
        LEFT JOIN work_item pw ON pw.id = w.parent_id AND pw.deleted_at IS NULL
        """;

    private const string DetailSelect =
        """
        SELECT w.id, w.project_id, w.number, w.kind, w.title, w.description, w.status, w.priority, w.points,
               w.assignee_id, a.display_name AS assignee_name,
               w.reporter_id, r.display_name AS reporter_name,
               s.id AS sprint_id, s.name AS sprint_name,
               pw.id AS parent_id, pw.number AS parent_number, pw.title AS parent_title,
               w.created_at, w.updated_at, w.deleted_at
        FROM work_item w
        LEFT JOIN person a ON a.id = w.assignee_id
        LEFT JOIN person r ON r.id = w.reporter_id
        LEFT JOIN sprint s ON s.id = w.sprint_id AND s.deleted_at IS NULL
        LEFT JOIN work_item pw ON pw.id = w.parent_id AND pw.deleted_at IS NULL
        """;

    public async Task<(Guid Id, int Number)> CreateAsync(Guid projectId, WorkItemFields fields, Guid reporterId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var id = Guid.CreateVersion7();
        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            var number = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "UPDATE project SET next_item_number = next_item_number + 1, updated_at = now() WHERE id = @projectId RETURNING next_item_number - 1",
                new { projectId },
                transaction,
                cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO work_item (id, project_id, number, kind, title, description, status, priority, points,
                                       assignee_id, reporter_id, sprint_id, parent_id, rank)
                VALUES (@id, @projectId, @number, @Kind, @Title, @Description, @Status, @Priority, @Points,
                        (SELECT person_id FROM project_member WHERE project_id = @projectId AND person_id = @AssigneeId),
                        @reporterId,
                        (SELECT id FROM sprint WHERE id = @SprintId AND project_id = @projectId AND deleted_at IS NULL AND state <> 'completed'),
                        (SELECT id FROM work_item WHERE id = @ParentId AND project_id = @projectId AND kind = 'epic' AND deleted_at IS NULL),
                        (SELECT coalesce(max(rank), 0) + @step FROM work_item WHERE project_id = @projectId))
                """,
                new
                {
                    id,
                    projectId,
                    number,
                    fields.Kind,
                    fields.Title,
                    fields.Description,
                    fields.Status,
                    fields.Priority,
                    fields.Points,
                    fields.AssigneeId,
                    reporterId,
                    fields.SprintId,
                    ParentId = fields.Kind == WorkItemKinds.Epic ? null : fields.ParentId,
                    step = RankStep,
                },
                transaction,
                cancellationToken: cancellationToken));
            await RecordAsync(connection, transaction, id, reporterId, "created", null, $"{WorkItemKinds.Label(fields.Kind)} #{number}", cancellationToken);
            return (id, number);
        }, cancellationToken);
    }

    public async Task<WorkItemRecord?> FindAsync(Guid projectId, int number, bool includeDeleted = false, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<WorkItemRecord>(new CommandDefinition(
            DetailSelect + " WHERE w.project_id = @projectId AND w.number = @number AND (@includeDeleted OR w.deleted_at IS NULL)",
            new { projectId, number, includeDeleted },
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<WorkItemSummary>> ListAsync(WorkItemQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<WorkItemSummary>(new CommandDefinition(
            SummarySelect + """
                 WHERE w.project_id = @ProjectId AND w.deleted_at IS NULL
                  AND (@SprintId::uuid IS NULL OR s.id = @SprintId)
                  AND (NOT @BacklogOnly OR s.id IS NULL)
                  AND (@IncludeDone OR w.status <> 'done')
                  AND (@Kind::text IS NULL OR w.kind = @Kind)
                  AND (@AssigneeId::uuid IS NULL OR w.assignee_id = @AssigneeId)
                  AND (@pattern::text IS NULL OR lower(w.title) LIKE @pattern OR w.number::text = @Text)
                ORDER BY w.rank
                LIMIT @Limit
                """,
            new
            {
                query.ProjectId,
                query.SprintId,
                query.BacklogOnly,
                query.IncludeDone,
                query.Kind,
                query.AssigneeId,
                Text = query.Text?.TrimStart('#'),
                pattern = string.IsNullOrWhiteSpace(query.Text) ? null : "%" + People.PersonStore.Like(query.Text.Trim().ToLowerInvariant()) + "%",
                query.Limit,
            },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<WorkItemSummary>> AssignedToAsync(Guid personId, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<WorkItemSummary>(new CommandDefinition(
            SummarySelect + """
                 WHERE w.assignee_id = @personId AND w.deleted_at IS NULL AND p.deleted_at IS NULL AND w.status <> 'done'
                  AND EXISTS (SELECT 1 FROM project_member m WHERE m.project_id = w.project_id AND m.person_id = @personId)
                ORDER BY w.priority, w.updated_at DESC
                LIMIT @limit
                """,
            new { personId, limit },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<WorkItemSummary>> ChildrenAsync(Guid parentId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<WorkItemSummary>(new CommandDefinition(
            SummarySelect + " WHERE w.parent_id = @parentId AND w.deleted_at IS NULL ORDER BY w.rank",
            new { parentId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<WorkItemSummary>> EpicsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<WorkItemSummary>(new CommandDefinition(
            SummarySelect + " WHERE w.project_id = @projectId AND w.kind = 'epic' AND w.deleted_at IS NULL ORDER BY w.rank",
            new { projectId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<WorkItemEventRecord>> HistoryAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<WorkItemEventRecord>(new CommandDefinition(
            """
            SELECT e.occurred_at, p.display_name AS actor_name, e.field, e.old_value, e.new_value
            FROM work_item_event e LEFT JOIN person p ON p.id = e.actor_id
            WHERE e.work_item_id = @itemId
            ORDER BY e.seq
            """,
            new { itemId },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task<bool> UpdateAsync(Guid projectId, int number, Func<WorkItemFields, WorkItemFields> change, Guid actorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        return await database.InTransactionAsync(async (connection, transaction) =>
        {
            var current = await connection.QuerySingleOrDefaultAsync<WorkItemRecord>(new CommandDefinition(
                DetailSelect + " WHERE w.project_id = @projectId AND w.number = @number AND w.deleted_at IS NULL FOR UPDATE OF w",
                new { projectId, number },
                transaction,
                cancellationToken: cancellationToken));
            if (current is null)
            {
                return false;
            }

            var before = current.Fields;
            var requested = change(before);
            var after = await ResolveAsync(connection, transaction, projectId, current.Id, requested, cancellationToken);
            if (after == before)
            {
                return true;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE work_item SET kind = @Kind, title = @Title, description = @Description, status = @Status,
                    priority = @Priority, points = @Points, assignee_id = @AssigneeId, sprint_id = @SprintId,
                    parent_id = @ParentId, updated_at = now()
                WHERE id = @id
                """,
                new
                {
                    id = current.Id,
                    after.Kind,
                    after.Title,
                    after.Description,
                    after.Status,
                    after.Priority,
                    after.Points,
                    after.AssigneeId,
                    after.SprintId,
                    after.ParentId,
                },
                transaction,
                cancellationToken: cancellationToken));

            var labels = await LabelsAsync(connection, transaction, before, after, cancellationToken);
            foreach (var (field, oldValue, newValue) in Diff(before, after, labels))
            {
                await RecordAsync(connection, transaction, current.Id, actorId, field, oldValue, newValue, cancellationToken);
            }

            return true;
        }, cancellationToken);
    }

    public async Task<bool> MoveAsync(Guid projectId, int number, bool up, CancellationToken cancellationToken = default) =>
        await database.InTransactionAsync(async (connection, transaction) =>
        {
            var current = await connection.QuerySingleOrDefaultAsync<(Guid Id, long Rank, Guid? SprintId)>(new CommandDefinition(
                """
                SELECT w.id, w.rank, s.id AS sprint_id FROM work_item w
                LEFT JOIN sprint s ON s.id = w.sprint_id AND s.deleted_at IS NULL
                WHERE w.project_id = @projectId AND w.number = @number AND w.deleted_at IS NULL
                FOR UPDATE OF w
                """,
                new { projectId, number },
                transaction,
                cancellationToken: cancellationToken));
            if (current.Id == Guid.Empty)
            {
                return false;
            }

            var neighbour = await connection.QuerySingleOrDefaultAsync<(Guid Id, long Rank)>(new CommandDefinition(
                up
                    ? """
                      SELECT w.id, w.rank FROM work_item w LEFT JOIN sprint s ON s.id = w.sprint_id AND s.deleted_at IS NULL
                      WHERE w.project_id = @projectId AND w.deleted_at IS NULL AND w.rank < @rank
                        AND s.id IS NOT DISTINCT FROM @sprintId
                      ORDER BY w.rank DESC LIMIT 1 FOR UPDATE OF w
                      """
                    : """
                      SELECT w.id, w.rank FROM work_item w LEFT JOIN sprint s ON s.id = w.sprint_id AND s.deleted_at IS NULL
                      WHERE w.project_id = @projectId AND w.deleted_at IS NULL AND w.rank > @rank
                        AND s.id IS NOT DISTINCT FROM @sprintId
                      ORDER BY w.rank LIMIT 1 FOR UPDATE OF w
                      """,
                new { projectId, rank = current.Rank, sprintId = current.SprintId },
                transaction,
                cancellationToken: cancellationToken));
            if (neighbour.Id == Guid.Empty)
            {
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE work_item SET rank = CASE WHEN id = @a THEN @rankB ELSE @rankA END WHERE id IN (@a, @b)",
                new { a = current.Id, b = neighbour.Id, rankA = current.Rank, rankB = neighbour.Rank },
                transaction,
                cancellationToken: cancellationToken));
            return true;
        }, cancellationToken);

    private static async Task<WorkItemFields> ResolveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid itemId, WorkItemFields requested, CancellationToken cancellationToken)
    {
        var resolved = await connection.QuerySingleAsync<(Guid? AssigneeId, Guid? SprintId, Guid? ParentId)>(new CommandDefinition(
            """
            SELECT (SELECT person_id FROM project_member WHERE project_id = @projectId AND person_id = @AssigneeId) AS assignee_id,
                   (SELECT id FROM sprint WHERE id = @SprintId AND project_id = @projectId AND deleted_at IS NULL) AS sprint_id,
                   (SELECT id FROM work_item WHERE id = @ParentId AND id <> @itemId AND project_id = @projectId AND kind = 'epic' AND deleted_at IS NULL) AS parent_id
            """,
            new { projectId, itemId, requested.AssigneeId, requested.SprintId, requested.ParentId },
            transaction,
            cancellationToken: cancellationToken));
        return requested with
        {
            AssigneeId = resolved.AssigneeId,
            SprintId = resolved.SprintId,
            ParentId = requested.Kind == WorkItemKinds.Epic ? null : resolved.ParentId,
        };
    }

    private static async Task<Labels> LabelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, WorkItemFields before, WorkItemFields after, CancellationToken cancellationToken) =>
        await connection.QuerySingleAsync<Labels>(new CommandDefinition(
            """
            SELECT (SELECT display_name FROM person WHERE id = @oldAssignee) AS old_assignee,
                   (SELECT display_name FROM person WHERE id = @newAssignee) AS new_assignee,
                   (SELECT name FROM sprint WHERE id = @oldSprint) AS old_sprint,
                   (SELECT name FROM sprint WHERE id = @newSprint) AS new_sprint,
                   (SELECT '#' || number || ' ' || title FROM work_item WHERE id = @oldParent) AS old_parent,
                   (SELECT '#' || number || ' ' || title FROM work_item WHERE id = @newParent) AS new_parent
            """,
            new
            {
                oldAssignee = before.AssigneeId,
                newAssignee = after.AssigneeId,
                oldSprint = before.SprintId,
                newSprint = after.SprintId,
                oldParent = before.ParentId,
                newParent = after.ParentId,
            },
            transaction,
            cancellationToken: cancellationToken));

    private static IEnumerable<(string Field, string? Old, string? New)> Diff(WorkItemFields before, WorkItemFields after, Labels labels)
    {
        if (before.Kind != after.Kind)
        {
            yield return ("kind", WorkItemKinds.Label(before.Kind), WorkItemKinds.Label(after.Kind));
        }

        if (before.Title != after.Title)
        {
            yield return ("title", before.Title, after.Title);
        }

        if (before.Description != after.Description)
        {
            yield return ("description", null, null);
        }

        if (before.Status != after.Status)
        {
            yield return ("status", WorkItemStatuses.Label(before.Status), WorkItemStatuses.Label(after.Status));
        }

        if (before.Priority != after.Priority)
        {
            yield return ("priority", Priorities.Label(before.Priority), Priorities.Label(after.Priority));
        }

        if (before.Points != after.Points)
        {
            yield return ("points", before.Points?.ToString(System.Globalization.CultureInfo.InvariantCulture), after.Points?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        if (before.AssigneeId != after.AssigneeId)
        {
            yield return ("assignee", labels.OldAssignee, labels.NewAssignee);
        }

        if (before.SprintId != after.SprintId)
        {
            yield return ("sprint", labels.OldSprint, labels.NewSprint);
        }

        if (before.ParentId != after.ParentId)
        {
            yield return ("epic", labels.OldParent, labels.NewParent);
        }
    }

    private static Task RecordAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itemId, Guid actorId, string field, string? oldValue, string? newValue, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO work_item_event (id, work_item_id, actor_id, field, old_value, new_value)
            VALUES (@id, @itemId, @actorId, @field, @oldValue, @newValue)
            """,
            new { id = Guid.CreateVersion7(), itemId, actorId, field, oldValue, newValue },
            transaction,
            cancellationToken: cancellationToken));

    private sealed class Labels
    {
        public string? OldAssignee { get; init; }

        public string? NewAssignee { get; init; }

        public string? OldSprint { get; init; }

        public string? NewSprint { get; init; }

        public string? OldParent { get; init; }

        public string? NewParent { get; init; }
    }
}
