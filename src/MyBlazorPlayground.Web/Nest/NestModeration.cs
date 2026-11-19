using MyBlazorPlayground.Data.Nest;
using MyBlazorPlayground.Data.Pages;
using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Web.Administration;

namespace MyBlazorPlayground.Web.Nest;

internal sealed record HomeModeration(string Key, string Title, IReadOnlyList<string> Keys, IReadOnlyList<NestEntryRecord> Entries, bool CanModerate);

internal sealed class NestModeration(NestService nest, NestEntryStore entries, PageEditorStore editors, TrashService trash)
{
    public const string PagePrefix = "home:";

    public static string PageKey(string listingKey) => PagePrefix + listingKey;

    public async Task<HomeModeration> LoadAsync(string key, Actor? actor, CancellationToken cancellationToken = default)
    {
        var listing = nest.FindListing(key);
        var keys = listing?.Keys ?? [key];
        var stored = await entries.ForKeysAsync(keys.ToArray(), cancellationToken);
        return new HomeModeration(key, listing?.Title ?? ListingKey.Label(key), keys, stored, await CanModerateAsync(actor, keys, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Actor? actor, Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await entries.FindAsync(id, cancellationToken);
        if (actor is null || entry is null || !await CanModerateAsync(actor, entry.Keys, cancellationToken))
        {
            return false;
        }

        return await trash.SoftDeleteAsync(actor, Subject.NestEntry, id, Summary(entry), cancellationToken);
    }

    public async Task<bool> RestoreAsync(Actor? actor, Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await entries.FindAsync(id, cancellationToken);
        if (actor is null || entry is null || !await CanModerateAsync(actor, entry.Keys, cancellationToken))
        {
            return false;
        }

        return await trash.RestoreAsync(actor, Subject.NestEntry, id, cancellationToken);
    }

    public static string Summary(NestEntryRecord entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var text = entry.Text ?? entry.Url ?? entry.Fields ?? string.Join(' ', entry.Keys);
        return $"{entry.Kind}: {(text.Length > 80 ? text[..80] : text)}";
    }

    private async Task<bool> CanModerateAsync(Actor? actor, IReadOnlyCollection<string> keys, CancellationToken cancellationToken) =>
        actor is not null
        && (actor.IsAdministrator
            || await editors.IsEditorAsync(actor.PersonId, keys.Select(PageKey).ToArray(), cancellationToken));
}
