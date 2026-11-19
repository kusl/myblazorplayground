namespace MyBlazorPlayground.Domain.Access;

public static class AccessPolicy
{
    public static bool CanView(Actor? actor, string? pageRole) =>
        actor is not null && (actor.IsAdministrator || PageRoles.IsKnown(pageRole));

    public static bool CanEdit(Actor? actor, string? pageRole) => CanView(actor, pageRole);

    public static bool CanSoftDelete(Actor? actor, string? pageRole) =>
        actor is not null && (actor.IsAdministrator || string.Equals(pageRole, PageRoles.Editor, StringComparison.Ordinal));

    public static bool CanRestore(Actor? actor, string? pageRole) => CanSoftDelete(actor, pageRole);

    public static bool CanHardDelete(Actor? actor) => actor is { IsAdministrator: true };

    public static bool CanManageMembers(Actor? actor, string? pageRole) => CanSoftDelete(actor, pageRole);

    public static bool CanAdminister(Actor? actor) => actor is { IsAdministrator: true };

    public static bool CanEditComment(Actor? actor, string? pageRole, Guid authorId) =>
        actor is not null && (actor.PersonId == authorId || CanSoftDelete(actor, pageRole));
}
