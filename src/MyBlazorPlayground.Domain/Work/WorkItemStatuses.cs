namespace MyBlazorPlayground.Domain.Work;

public static class WorkItemStatuses
{
    public const string Backlog = "backlog";
    public const string ToDo = "todo";
    public const string InProgress = "in_progress";
    public const string Review = "review";
    public const string Done = "done";

    public static readonly IReadOnlyList<string> All = [Backlog, ToDo, InProgress, Review, Done];

    public static readonly IReadOnlyList<string> Board = [ToDo, InProgress, Review, Done];

    public static bool IsKnown(string? status) => All.Contains(status, StringComparer.Ordinal);

    public static string Label(string? status) => status switch
    {
        Backlog => "Backlog",
        ToDo => "To do",
        InProgress => "In progress",
        Review => "In review",
        Done => "Done",
        _ => "Unknown",
    };
}
