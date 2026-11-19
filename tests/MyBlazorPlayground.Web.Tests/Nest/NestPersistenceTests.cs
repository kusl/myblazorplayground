using MyBlazorPlayground.Data.Nest;
using MyBlazorPlayground.Web.Infrastructure;
using MyBlazorPlayground.Web.Nest;

namespace MyBlazorPlayground.Web.Tests.Nest;

public sealed class NestPersistenceTests
{
    private const string Zillow = "https://www.zillow.com/homedetails/29747113_zpid/";
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly ManualTimeProvider _time = new(Start);
    private readonly RecordingJournal _journal = new();

    [Fact]
    public void JournalsEveryAcceptedEntry()
    {
        var nest = Create();
        var link = nest.Submit(Drafts.Link(Zillow), "a").Entry!;
        var comment = nest.Submit(Drafts.Comment("nice yard", "zillow:29747113"), "a").Entry!;
        Assert.Equal([link.Id, comment.Id], _journal.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void DoesNotJournalWhatItLoads()
    {
        var nest = Create();
        var stored = Stored(Start.AddDays(-1), "loaded");
        nest.Load([stored], []);
        Assert.Single(nest.Entries);
        Assert.Empty(_journal.Entries);
    }

    [Fact]
    public void LoadSkipsDeletedEntriesAndRefusesTheirOffers()
    {
        var nest = Create();
        var kept = Stored(Start.AddDays(-2), "kept");
        var gone = Stored(Start.AddDays(-1), "gone");
        nest.Load([kept, gone], [gone.Id]);
        Assert.Equal([kept.Id], nest.Entries.Select(entry => entry.Id));
        Assert.Equal(0, nest.Offer([gone]));
        Assert.Equal([kept.Id], nest.Entries.Select(entry => entry.Id));
    }

    [Fact]
    public void RemoveTellsSubscribersToPurge()
    {
        var nest = Create();
        var entry = nest.Submit(Drafts.Comment("spam", "zillow:1"), "a").Entry!;
        var recorder = new NestRecorder();
        nest.Subscribe(recorder);
        Assert.True(nest.Remove(entry.Id));
        var delta = recorder.Updates[^1];
        Assert.Equal("delta", delta.Kind);
        Assert.Equal([entry.Id], delta.Deleted);
        Assert.Equal([entry.Id], delta.Removed);
        Assert.Empty(nest.Entries);

        var late = new NestRecorder();
        nest.Subscribe(late);
        Assert.Equal([entry.Id], late.Updates[0].Deleted);
    }

    [Fact]
    public void RestoreBringsAnEntryBack()
    {
        var nest = Create();
        var entry = Stored(Start.AddDays(-1), "back");
        nest.Load([], [entry.Id]);
        Assert.Equal(0, nest.Offer([entry]));
        nest.Restore(entry);
        Assert.Equal([entry.Id], nest.Entries.Select(item => item.Id));
    }

    [Fact]
    public void StorageRoundTripsEntries()
    {
        var entry = new Entry(
            Uuid7.After(Guid.Empty, Start.AddDays(-1)),
            EntryKinds.Fact,
            ["address:13777-1-main-st"],
            null,
            [new FieldValue("street", "1 Main St"), new FieldValue("zip", "13777")],
            null);
        var row = NestStorage.ToRow(entry);
        var record = new NestEntryRecord { Id = row.Id, Kind = row.Kind, Keys = row.Keys, Url = row.Url, Fields = row.Fields, Text = row.Text };
        var back = NestStorage.ToEntry(record);
        Assert.NotNull(back);
        Assert.Equal(entry.Id, back.Id);
        Assert.Equal(entry.Fields, back.Fields);
        Assert.Equal(entry.Keys, back.Keys);
    }

    [Fact]
    public void StorageRejectsRowsThatAreNotInNormalForm()
    {
        var record = new NestEntryRecord { Id = Guid.CreateVersion7(), Kind = EntryKinds.Comment, Keys = ["zillow:1"], Text = "  padded  " };
        Assert.Null(NestStorage.ToEntry(record));
        Assert.Null(NestStorage.ToEntry(new NestEntryRecord { Id = Guid.CreateVersion7(), Kind = EntryKinds.Fact, Keys = ["zillow:1"], Fields = "{not json" }));
    }

    private NestService Create() => new(_time, Limits.Create(), _journal);

    private static Entry Stored(DateTimeOffset at, string text) =>
        new(Uuid7.After(Guid.Empty, at), EntryKinds.Comment, ["zillow:29747113"], null, null, text);

    private sealed class RecordingJournal : IJournal<Entry>
    {
        public List<Entry> Entries { get; } = [];

        public void Append(IReadOnlyList<Entry> entries) => Entries.AddRange(entries);
    }
}
