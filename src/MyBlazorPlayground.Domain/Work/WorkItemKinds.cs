namespace MyBlazorPlayground.Domain.Work;

public static class WorkItemKinds
{
    public const string Epic = "epic";
    public const string Story = "story";
    public const string Task = "task";
    public const string Bug = "bug";

    public static readonly IReadOnlyList<string> All = [Story, Task, Bug, Epic];

    public static bool IsKnown(string? kind) => All.Contains(kind, StringComparer.Ordinal);

    public static string Label(string? kind) => kind switch
    {
        Epic => "Epic",
        Story => "Story",
        Task => "Task",
        Bug => "Bug",
        _ => "Item",
    };
}
