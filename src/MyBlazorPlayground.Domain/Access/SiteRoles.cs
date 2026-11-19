namespace MyBlazorPlayground.Domain.Access;

public static class SiteRoles
{
    public const string Administrator = "administrator";

    public static readonly IReadOnlyList<string> All = [Administrator];

    public static bool IsKnown(string? role) => All.Contains(role, StringComparer.Ordinal);
}
