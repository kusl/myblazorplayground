using System.Text.RegularExpressions;
using MyBlazorPlayground.Domain.Text;

namespace MyBlazorPlayground.Domain.Work;

public static partial class WorkRules
{
    public const int MaxProjectName = 80;
    public const int MaxProjectKey = 10;
    public const int MaxTitle = 200;
    public const int MaxSprintName = 80;
    public const int MaxGoal = 500;
    public const int MaxComment = 20_000;
    public const int MaxPoints = 999;

    public static string NormalizeProjectKey(string? key) => (key ?? string.Empty).Trim().ToUpperInvariant();

    public static string? ProjectKeyProblem(string? key) =>
        ProjectKeyPattern().IsMatch(NormalizeProjectKey(key))
            ? null
            : $"Keys are 2 to {MaxProjectKey} uppercase letters or digits, starting with a letter.";

    public static string? ProjectNameProblem(string? name) =>
        PlainText.Line(name, MaxProjectName).Length == 0 ? "Give the project a name." : null;

    public static string? TitleProblem(string? title) =>
        PlainText.Line(title, MaxTitle).Length == 0 ? "Give the item a title." : null;

    public static string? PointsProblem(int? points) =>
        points is null or (>= 0 and <= MaxPoints) ? null : $"Points are 0 to {MaxPoints}.";

    public static string? SprintDatesProblem(DateOnly? starts, DateOnly? ends) =>
        starts is { } s && ends is { } e && e < s ? "A sprint cannot end before it starts." : null;

    public static string Description(string? text) => PlainText.Block(text, RichText.MaxLength);

    public static string Comment(string? text) => PlainText.Block(text, MaxComment);

    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,9}\z")]
    private static partial Regex ProjectKeyPattern();
}
