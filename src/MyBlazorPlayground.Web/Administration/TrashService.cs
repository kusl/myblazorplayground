using MyBlazorPlayground.Data.Nest;
using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Web.Nest;

namespace MyBlazorPlayground.Web.Administration;

internal sealed class TrashService(Deletions deletions, NestEntryStore nestEntries, NestService nest)
{
    public Task<IReadOnlyList<TrashRecord>> ListAsync(Guid? projectId, CancellationToken cancellationToken = default) =>
        deletions.ListAsync(projectId, 500, cancellationToken);

    public async Task<bool> SoftDeleteAsync(Actor actor, Subject subject, Guid id, string summary, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var changed = await deletions.SoftDeleteAsync(subject, id, actor.PersonId, Audits.For(actor, "soft-delete", subject, id, summary), cancellationToken);
        if (changed && subject == Subject.NestEntry)
        {
            nest.Remove(id);
        }

        return changed;
    }

    public async Task<bool> RestoreAsync(Actor actor, Subject subject, Guid id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        var record = await deletions.FindAsync(subject, id, cancellationToken);
        if (record is null)
        {
            return false;
        }

        var changed = await deletions.RestoreAsync(subject, id, Audits.For(actor, "restore", subject, id, record.Title), cancellationToken);
        if (changed && subject == Subject.NestEntry
            && await nestEntries.FindAsync(id, cancellationToken) is { } stored
            && NestStorage.ToEntry(stored) is { } entry)
        {
            nest.Restore(entry);
        }

        return changed;
    }

    public async Task<bool> HardDeleteAsync(Actor actor, Subject subject, Guid id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!AccessPolicy.CanHardDelete(actor))
        {
            return false;
        }

        var record = await deletions.FindAsync(subject, id, cancellationToken);
        return record is not null
            && await deletions.HardDeleteAsync(subject, id, Audits.For(actor, "hard-delete", subject, id, record.Title), cancellationToken);
    }
}
