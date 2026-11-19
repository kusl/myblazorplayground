using Microsoft.Extensions.Logging.Abstractions;
using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos;
using MyBlazorPlayground.Web.Chaos.Drawing;
using MyBlazorPlayground.Web.Chaos.Latency;
using MyBlazorPlayground.Web.Chaos.Messages;
using MyBlazorPlayground.Web.Chaos.Postcards;
using MyBlazorPlayground.Web.Chaos.Slots;
using MyBlazorPlayground.Web.Chaos.Storage;
using MyBlazorPlayground.Web.Infrastructure;

namespace MyBlazorPlayground.Web.Tests.Chaos;

public sealed class ToyPersistenceTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
    private readonly ManualTimeProvider _time = new(Start);
    private readonly List<IDisposable> _disposables = [];

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public void BoardJournalsPostsAndOffersButNotLoads()
    {
        var journal = new RecordingJournal<BoardMessage>();
        var board = new BoardService(_time, Limits.Create(), journal);
        var stored = Enumerable.Range(1, 3).Select(i => new BoardMessage(Uuid7.After(Guid.Empty, Start.AddMinutes(-i)), $"stored {i}")).ToList();
        Assert.Equal(3, board.Load([.. stored, new BoardMessage(Guid.NewGuid(), "not v7"), new BoardMessage(Guid.CreateVersion7(Start.AddMinutes(-9)), "  padded ")]));
        Assert.Empty(journal.Items);
        Assert.Equal(stored.Select(message => message.Id), board.Messages.Select(message => message.Id));

        var posted = board.Submit("hello", "a", null).Message!;
        var offered = new BoardMessage(Guid.CreateVersion7(Start.AddMinutes(-30)), "from a browser");
        Assert.Equal(1, board.Offer([offered, stored[0]]));
        Assert.Equal([posted.Id, offered.Id], journal.Items.Select(message => message.Id));
    }

    [Fact]
    public void BoardLoadKeepsTheNewestWithinCapacity()
    {
        var board = new BoardService(_time, Limits.Create(o => o.BoardCapacity = 2));
        var stored = Enumerable.Range(1, 5).Select(i => new BoardMessage(Guid.CreateVersion7(Start.AddMinutes(-i)), $"m{i}")).ToList();
        Assert.Equal(2, board.Load(stored));
        Assert.Equal(["m1", "m2"], board.Messages.Select(message => message.Text));
    }

    [Fact]
    public void SlotsJournalSpinsAndLoadAllThreeLists()
    {
        var journal = new RecordingJournal<BoardMessage>();
        var slots = new SlotService(_time, Limits.Create(o => o.SlotCapacity = 2), journal);
        var older = Start.AddHours(-1);
        BoardMessage Spin(int minutes, string text) => new(Guid.CreateVersion7(older.AddMinutes(-minutes)), text);
        var plain = Spin(1, "0A1B2");
        var winner = Spin(2, "AAA12");
        var jackpot = Spin(3, "77777");
        var loaded = slots.Load([plain, winner, jackpot, Spin(4, "bad")], [plain, winner, jackpot], [winner, jackpot]);
        Assert.Equal(3, loaded);
        Assert.Equal([plain.Id, winner.Id], slots.Messages.Select(message => message.Id));
        Assert.Equal([winner.Id, jackpot.Id], slots.Winners.Select(message => message.Id));
        Assert.Equal([jackpot.Id], slots.Jackpots.Select(message => message.Id));
        Assert.Empty(journal.Items);

        var press = slots.Press("a");
        Assert.Equal(press.Id, Assert.Single(journal.Items).Id);
    }

    [Fact]
    public async Task LatencyJournalsRunsAndRebuildsLeadersOnLoad()
    {
        var journal = new RecordingJournal<BoardMessage>();
        var count = LatencyCatalog.Endpoints.Count;
        using var latency = new LatencyService(_time, Limits.Create(o => o.LatencyCapacity = 1), new FakeProbe(endpoints => [.. endpoints.Select(_ => (int?)5)]), NullLogger<LatencyService>.Instance, journal);
        var fast = new BoardMessage(Guid.CreateVersion7(Start.AddHours(-2)), LatencyRun.Compose(LatencyCatalog.Endpoints, [.. Enumerable.Repeat((int?)3, count)], new int?[count]));
        var slow = new BoardMessage(Guid.CreateVersion7(Start.AddHours(-1)), LatencyRun.Compose(LatencyCatalog.Endpoints, [.. Enumerable.Repeat((int?)300, count)], new int?[count]));
        Assert.Equal(2, latency.Load([slow], [fast, slow, new BoardMessage(Guid.CreateVersion7(Start.AddHours(-3)), "nonsense")]));
        Assert.Equal([slow.Id], latency.Messages.Select(message => message.Id));
        Assert.Equal([fast.Id], latency.Leaders.FastJackpots.Select(run => run.Id));
        Assert.Equal([slow.Id], latency.Leaders.SlowJackpots.Select(run => run.Id));
        Assert.Equal(fast.Id, latency.Leaders.BrowserFastest[0].Id);
        Assert.Empty(journal.Items);

        var start = latency.Begin();
        latency.Finish(start.Id, [.. Enumerable.Repeat(4, count)]);
        await latency.WhenIdle().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Assert.Equal(start.Id, Assert.Single(journal.Items).Id);
    }

    [Fact]
    public void CanvasJournalsFinishedStrokesAndResets()
    {
        var journal = new RecordingJournal<CanvasEvent>();
        var canvas = new CanvasService(_time, Limits.Create(o => o.ResetBurst = 10), journal);
        var session = canvas.OpenSession(new CanvasRecorder());
        canvas.Draw(session, canvas.Epoch, 1, [1, 1, 2, 2], false);
        Assert.Empty(journal.Items);
        canvas.Draw(session, canvas.Epoch, 1, [3, 3], true);
        var stroke = Assert.Single(journal.Items);
        Assert.Equal([1, 1, 2, 2, 3, 3], stroke.Points!);

        canvas.Draw(session, canvas.Epoch, 2, [4, 4], false);
        canvas.Abandon(session);
        Assert.Equal(2, journal.Items.Count);

        Assert.Equal(ResetResult.Accepted, canvas.Reset("a"));
        Assert.Null(journal.Items[^1].Points);
        Assert.Equal(canvas.ResetId, journal.Items[^1].Id);
    }

    [Fact]
    public void CanvasLoadsStrokesAfterTheResetUpToTheLimit()
    {
        var journal = new RecordingJournal<CanvasEvent>();
        var canvas = new CanvasService(_time, Limits.Create(o => o.CanvasMaxPoints = CanvasService.MaxStrokePoints), journal);
        var reset = Guid.CreateVersion7(Start.AddHours(-3));
        var beforeReset = new StrokeOffer(Guid.CreateVersion7(Start.AddHours(-4)), [1, 1]);
        var old = new StrokeOffer(Guid.CreateVersion7(Start.AddHours(-2)), [.. Enumerable.Repeat(1, 2 * CanvasService.MaxStrokePoints)]);
        var newest = new StrokeOffer(Guid.CreateVersion7(Start.AddHours(-1)), [5, 5, 6, 6]);
        Assert.Equal(1, canvas.Load(reset, [beforeReset, old, newest, new StrokeOffer(Guid.CreateVersion7(Start), [1])]));
        Assert.Equal(reset, canvas.ResetId);
        Assert.Equal(1, canvas.StrokeCount);
        Assert.Equal(2, canvas.PointCount);
        Assert.Empty(journal.Items);

        var session = canvas.OpenSession(new CanvasRecorder());
        canvas.Draw(session, canvas.Epoch, 1, [1, 1], true);
        Assert.True(session.LastStroke.CompareTo(newest.Id) > 0);
    }

    [Fact]
    public void PostcardsJournalEveryAcceptedCardAndLoadTheNewest()
    {
        var journal = new RecordingJournal<StoredPostcard>();
        var postcards = new PostcardService(_time, Limits.Create(), journal);
        _disposables.Add(postcards);
        var id = Guid.CreateVersion7(Start.AddHours(-1));
        Assert.True(postcards.Load(id, "kept", [TestImages.Png()], [new MetaEntry("latitude", "1"), new MetaEntry("longitude", "2")]));
        Assert.False(postcards.Load(Guid.CreateVersion7(Start.AddHours(-2)), "older", [], null));
        Assert.False(postcards.Load(Guid.CreateVersion7(Start), "", [], null));
        Assert.Equal(id, postcards.Current!.Id);
        Assert.True(postcards.TryGetImage(id, 0, out var image, out var bytes));
        Assert.Equal(ImageSanitizer.Png, image!.Type);
        Assert.False(bytes.IsEmpty);
        Assert.Empty(journal.Items);

        var reply = postcards.Submit("new", [TestImages.Png()], null, "a");
        Assert.Equal(PostcardResult.Accepted, reply.Result);
        var stored = Assert.Single(journal.Items);
        Assert.Equal(reply.Card!.Id, stored.Card.Id);
        Assert.Single(stored.Images);
    }

    [Fact]
    public void RowsCarryWhatTheQueriesNeed()
    {
        Assert.Equal((true, true), Flags(ToyStorage.ToSlotRow(new BoardMessage(Guid.Empty, "77777"))));
        Assert.Equal((true, false), Flags(ToyStorage.ToSlotRow(new BoardMessage(Guid.Empty, "AAA12"))));
        Assert.Equal((false, false), Flags(ToyStorage.ToSlotRow(new BoardMessage(Guid.Empty, "0A1B2"))));

        var count = LatencyCatalog.Endpoints.Count;
        var run = ToyStorage.ToLatencyRow(new BoardMessage(Guid.Empty, LatencyRun.Compose(LatencyCatalog.Endpoints, [.. Enumerable.Repeat((int?)8, count)], new int?[count])));
        Assert.Equal(8, run.BrowserMedian);
        Assert.Null(run.ServerMedian);

        var meta = new[] { new MetaEntry("latitude", "1") };
        var row = ToyStorage.ToBoardRow(new BoardMessage(Guid.Empty, "x", meta));
        Assert.True(ToyStorage.TryMetaFromJson(row.Meta, out var back));
        Assert.Equal(meta, back);
        Assert.Null(ToyStorage.ToBoardRow(new BoardMessage(Guid.Empty, "x")).Meta);
        Assert.Null(ToyStorage.ToMessage(new ToyMessageRecord { Id = Guid.Empty, Text = "x", Meta = "{broken" }));

        static (bool, bool) Flags(SlotSpinRow spin) => (spin.Winner, spin.Jackpot);
    }

    private sealed class RecordingJournal<T> : IJournal<T>
    {
        public List<T> Items { get; } = [];

        public void Append(IReadOnlyList<T> items) => Items.AddRange(items);
    }
}
