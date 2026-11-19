namespace MyBlazorPlayground.Domain.Work;

public static class Priorities
{
    public const int Highest = 1;
    public const int High = 2;
    public const int Medium = 3;
    public const int Low = 4;

    public static readonly IReadOnlyList<int> All = [Highest, High, Medium, Low];

    public static bool IsKnown(int priority) => priority is >= Highest and <= Low;

    public static string Label(int priority) => priority switch
    {
        Highest => "Highest",
        High => "High",
        Medium => "Medium",
        Low => "Low",
        _ => "None",
    };
}
