namespace MyBlazorPlayground.Web.Nest;

internal sealed record NestUpdate(string Kind, long Version, long Previous, int Capacity, IReadOnlyList<Guid> Ids, IReadOnlyList<Entry> Added, IReadOnlyList<Guid> Removed, IReadOnlyList<Guid> Deleted)
{
    public static NestUpdate Snapshot(long version, int capacity, IReadOnlyList<Entry> entries, IReadOnlyList<Guid>? deleted = null)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return new("snapshot", version, version, capacity, entries.Select(entry => entry.Id).ToArray(), [], [], deleted ?? []);
    }

    public static NestUpdate Delta(long version, long previous, IReadOnlyList<Entry> added, IReadOnlyList<Guid> removed, IReadOnlyList<Guid>? deleted = null) =>
        new("delta", version, previous, 0, [], added, removed, deleted ?? []);
}
