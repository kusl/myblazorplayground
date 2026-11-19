using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Text;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Web.Work;

public sealed class ItemForm
{
    private string _kind = WorkItemKinds.Story;
    private string _status = WorkItemStatuses.Backlog;
    private int _priority = Priorities.Medium;

    public string Kind
    {
        get => _kind;
        set => _kind = string.IsNullOrEmpty(value) ? WorkItemKinds.Story : value;
    }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public string Status
    {
        get => _status;
        set => _status = string.IsNullOrEmpty(value) ? WorkItemStatuses.Backlog : value;
    }

    public int Priority
    {
        get => _priority;
        set => _priority = value == 0 ? Priorities.Medium : value;
    }

    public int? Points { get; set; }

    public Guid? AssigneeId { get; set; }

    public Guid? SprintId { get; set; }

    public Guid? ParentId { get; set; }

    public static ItemForm From(WorkItemRecord item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemForm
        {
            Kind = item.Kind,
            Title = item.Title,
            Description = item.Description,
            Status = item.Status,
            Priority = item.Priority,
            Points = item.Points,
            AssigneeId = item.AssigneeId,
            SprintId = item.SprintId,
            ParentId = item.ParentId,
        };
    }

    public string? Problem() =>
        !WorkItemKinds.IsKnown(Kind) ? "Pick a kind."
        : !WorkItemStatuses.IsKnown(Status) ? "Pick a status."
        : !Priorities.IsKnown(Priority) ? "Pick a priority."
        : WorkRules.TitleProblem(Title) ?? WorkRules.PointsProblem(Points);

    public WorkItemFields ToFields()
    {
        var status = SprintId is not null && Status == WorkItemStatuses.Backlog ? WorkItemStatuses.ToDo : Status;
        return new WorkItemFields(
            Kind,
            PlainText.Line(Title, WorkRules.MaxTitle),
            WorkRules.Description(Description),
            status,
            Priority,
            Points,
            AssigneeId,
            SprintId,
            Kind == WorkItemKinds.Epic ? null : ParentId);
    }
}

public sealed class SprintForm
{
    public string? Name { get; set; }

    public string? Goal { get; set; }

    public DateOnly? StartsOn { get; set; }

    public DateOnly? EndsOn { get; set; }

    public string? Problem() =>
        PlainText.Line(Name, WorkRules.MaxSprintName).Length == 0 ? "Give the sprint a name." : WorkRules.SprintDatesProblem(StartsOn, EndsOn);

    public SprintDraft ToDraft() =>
        new(PlainText.Line(Name, WorkRules.MaxSprintName), PlainText.Block(Goal, WorkRules.MaxGoal), StartsOn, EndsOn);
}

public sealed class ProjectForm
{
    public string? Key { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }
}
