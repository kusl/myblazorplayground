namespace MyBlazorPlayground.Domain.Work;

public static class SprintStates
{
    public const string Planned = "planned";
    public const string Active = "active";
    public const string Completed = "completed";

    public static string Label(string? state) => state switch
    {
        Planned => "Planned",
        Active => "Active",
        Completed => "Completed",
        _ => "Unknown",
    };
}
