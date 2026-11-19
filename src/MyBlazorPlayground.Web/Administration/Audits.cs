using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Domain.Access;

namespace MyBlazorPlayground.Web.Administration;

internal static class Audits
{
    public static AuditEntry For(Actor actor, string action, Subject subject, Guid id, string summary, string? client = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new AuditEntry(actor.PersonId, actor.UserName, action, SubjectNames.Of(subject), id.ToString(), summary, client);
    }

    public static AuditEntry For(Actor actor, string action, string subjectType, string subjectId, string summary, string? client = null)
    {
        ArgumentNullException.ThrowIfNull(actor);
        return new AuditEntry(actor.PersonId, actor.UserName, action, subjectType, subjectId, summary, client);
    }
}
