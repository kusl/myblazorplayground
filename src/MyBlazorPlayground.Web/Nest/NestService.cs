using Microsoft.Extensions.Options;
using MyBlazorPlayground.Web.Infrastructure;

namespace MyBlazorPlayground.Web.Nest;

internal sealed class NestService
{
    public const int MaxFetch = 100;
    public const int RepeatWindow = 20;

    private readonly TimeProvider _time;
    private readonly KeyedTokenBuckets _posts;
    private readonly Lock _gate = new();
    private readonly HashSet<INestSubscriber> _subscribers = [];
    private readonly IJournal<Entry> _journal;
    private HashSet<Guid> _deleted = [];
    private Entry[] _entries = [];
    private Dictionary<Guid, Entry> _byId = new();
    private ListingCache? _listings;
    private long _version;

    public NestService(TimeProvider time, IOptions<NestOptions> options, IJournal<Entry>? journal = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(options);
        var limits = options.Value;
        _time = time;
        _journal = journal ?? NoJournal<Entry>.Instance;
        Capacity = limits.Capacity;
        _posts = new KeyedTokenBuckets(limits.PostBurst, limits.PostsPerSecond, time);
        StartedAt = time.GetUtcNow();
    }

    public int Capacity { get; }

    public DateTimeOffset StartedAt { get; }

    public IReadOnlyList<Entry> Entries => Volatile.Read(ref _entries);

    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public IReadOnlyList<Listing> Listings
    {
        get
        {
            var entries = Volatile.Read(ref _entries);
            var cache = Volatile.Read(ref _listings);
            if (cache is not null && ReferenceEquals(cache.Source, entries))
            {
                return cache.Listings;
            }

            var listings = ListingIndex.Build(entries);
            Volatile.Write(ref _listings, new ListingCache(entries, listings));
            return listings;
        }
    }

    public Listing? FindListing(string key) => ListingIndex.Find(Listings, key);

    public SubmitReply Submit(EntryDraft? draft, string client)
    {
        var candidate = draft is null ? null : EntryRules.Build(Guid.Empty, draft);
        if (candidate is null)
        {
            return new SubmitReply(SubmitResult.Invalid);
        }

        var key = KeyOf(candidate);
        if (!_posts.TryTake(client))
        {
            return new SubmitReply(SubmitResult.TooFast);
        }

        lock (_gate)
        {
            if (Exists(candidate))
            {
                return new SubmitReply(SubmitResult.Exists, Key: key);
            }

            if (IsRepeat(candidate))
            {
                return new SubmitReply(SubmitResult.Repeat);
            }

            var newest = _entries.Length > 0 ? _entries[0].Id : Guid.Empty;
            var entry = candidate with { Id = Uuid7.After(newest, _time.GetUtcNow()) };
            Merge([entry]);
            return new SubmitReply(SubmitResult.Accepted, entry, key);
        }
    }

    public int Offer(IEnumerable<Entry> offered)
    {
        ArgumentNullException.ThrowIfNull(offered);
        var now = _time.GetUtcNow();
        var valid = new List<Entry>();
        foreach (var entry in offered.Take(Capacity))
        {
            if (IsAcceptable(entry, now))
            {
                valid.Add(entry);
            }
        }

        if (valid.Count == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            return Merge(valid);
        }
    }

    public IReadOnlyList<Entry> Find(IEnumerable<Guid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var byId = Volatile.Read(ref _byId);
        var found = new List<Entry>();
        foreach (var id in ids.Take(MaxFetch))
        {
            if (byId.TryGetValue(id, out var entry))
            {
                found.Add(entry);
            }
        }

        return found;
    }

    public void Subscribe(INestSubscriber subscriber)
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (_gate)
        {
            if (subscriber.Deliver(NestUpdate.Snapshot(_version, Capacity, _entries, [.. _deleted])))
            {
                _subscribers.Add(subscriber);
            }
        }
    }

    public void Unsubscribe(INestSubscriber subscriber)
    {
        lock (_gate)
        {
            _subscribers.Remove(subscriber);
        }
    }

    public bool IsSubscribed(INestSubscriber subscriber)
    {
        lock (_gate)
        {
            return _subscribers.Contains(subscriber);
        }
    }

    private static string KeyOf(Entry entry) =>
        EntryRules.DerivedKey(entry) ?? entry.Keys.Order(ListingKey.Comparer).First();

    private bool IsAcceptable(Entry? entry, DateTimeOffset now) =>
        entry is not null
        && !Volatile.Read(ref _deleted).Contains(entry.Id)
        && EntryWindow.IsRestorable(entry.Id, now, StartedAt)
        && EntryRules.IsNormalized(entry);

    private bool Exists(Entry candidate)
    {
        if (!string.Equals(candidate.Kind, EntryKinds.Link, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var entry in _entries)
        {
            if (string.Equals(entry.Kind, EntryKinds.Link, StringComparison.Ordinal)
                && candidate.Keys.All(key => entry.Keys.Contains(key, StringComparer.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsRepeat(Entry candidate)
    {
        var window = Math.Min(RepeatWindow, _entries.Length);
        for (var i = 0; i < window; i++)
        {
            var entry = _entries[i];
            if (string.Equals(entry.Kind, candidate.Kind, StringComparison.Ordinal)
                && string.Equals(entry.Text, candidate.Text, StringComparison.Ordinal)
                && entry.Keys.SequenceEqual(candidate.Keys, StringComparer.Ordinal)
                && FieldsEqual(entry.Fields, candidate.Fields))
            {
                return true;
            }
        }

        return false;
    }

    private static bool FieldsEqual(IReadOnlyList<FieldValue>? a, IReadOnlyList<FieldValue>? b) =>
        a is null ? b is null : b is not null && a.SequenceEqual(b);

    private int Merge(IReadOnlyCollection<Entry> candidates)
    {
        var change = EntryWindow.Merge(_entries, candidates, Capacity);
        if (change is null)
        {
            return 0;
        }

        var previous = _version;
        _version++;
        Volatile.Write(ref _byId, change.Next.ToDictionary(entry => entry.Id));
        Volatile.Write(ref _entries, change.Next);
        Broadcast(NestUpdate.Delta(_version, previous, change.Added, change.Removed));
        _journal.Append(change.Added);
        return change.Added.Length;
    }

    public void Load(IEnumerable<Entry> entries, IEnumerable<Guid> deleted)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(deleted);
        lock (_gate)
        {
            var tombstones = deleted.ToHashSet();
            var next = entries
                .Where(entry => !tombstones.Contains(entry.Id))
                .DistinctBy(entry => entry.Id)
                .OrderByDescending(entry => entry.Id)
                .Take(Capacity)
                .ToArray();
            _version++;
            Volatile.Write(ref _deleted, tombstones);
            Volatile.Write(ref _byId, next.ToDictionary(entry => entry.Id));
            Volatile.Write(ref _entries, next);
            Broadcast(NestUpdate.Snapshot(_version, Capacity, next, [.. tombstones]));
        }
    }

    public bool Remove(Guid id)
    {
        lock (_gate)
        {
            var deleted = new HashSet<Guid>(_deleted) { id };
            Volatile.Write(ref _deleted, deleted);
            var previous = _version;
            _version++;
            var next = _entries.Where(entry => entry.Id != id).ToArray();
            var removed = next.Length != _entries.Length;
            Volatile.Write(ref _byId, next.ToDictionary(entry => entry.Id));
            Volatile.Write(ref _entries, next);
            Broadcast(NestUpdate.Delta(_version, previous, [], removed ? [id] : [], [id]));
            return removed;
        }
    }

    public void Restore(Entry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            var deleted = new HashSet<Guid>(_deleted);
            deleted.Remove(entry.Id);
            Volatile.Write(ref _deleted, deleted);
            var change = EntryWindow.Merge(_entries, [entry], Capacity);
            if (change is null)
            {
                return;
            }

            var previous = _version;
            _version++;
            Volatile.Write(ref _byId, change.Next.ToDictionary(item => item.Id));
            Volatile.Write(ref _entries, change.Next);
            Broadcast(NestUpdate.Delta(_version, previous, change.Added, change.Removed));
        }
    }

    private void Broadcast(NestUpdate update)
    {
        List<INestSubscriber>? dropped = null;
        foreach (var subscriber in _subscribers)
        {
            if (!subscriber.Deliver(update))
            {
                (dropped ??= []).Add(subscriber);
            }
        }

        if (dropped is not null)
        {
            _subscribers.ExceptWith(dropped);
        }
    }

    private sealed record ListingCache(Entry[] Source, IReadOnlyList<Listing> Listings);
}
