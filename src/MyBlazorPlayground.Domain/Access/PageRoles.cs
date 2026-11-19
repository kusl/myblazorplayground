namespace MyBlazorPlayground.Domain.Access;

public static class PageRoles
{
    public const string Editor = "editor";
    public const string Member = "member";

    public static readonly IReadOnlyList<string> All = [Editor, Member];

    public static bool IsKnown(string? role) => All.Contains(role, StringComparer.Ordinal);

    public static string Label(string? role) => role switch
    {
        Editor => "Editor",
        Member => "Member",
        _ => "None",
    };
}
