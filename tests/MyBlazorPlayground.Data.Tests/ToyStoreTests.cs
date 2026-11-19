using MyBlazorPlayground.Data.Toys;

namespace MyBlazorPlayground.Data.Tests;

public sealed class ToyStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private static Guid At(int minutes) => Guid.CreateVersion7(Start.AddMinutes(minutes));

    [Fact]
    public async Task BoardMessagesRoundTripWithMeta()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new BoardStore(test.Database);
        BoardMessageRow[] rows = [new(At(1), "one", null), new(At(2), "two", """[{"key":"latitude","value":"1"}]"""), new(At(3), "three", null)];
        Assert.Equal(3, await store.InsertAsync(rows, token));
        Assert.Equal(0, await store.InsertAsync([rows[0]], token));
        var loaded = await store.LoadAsync(2, token);
        Assert.Equal(["three", "two"], loaded.Select(record => record.Text));
        Assert.Contains("\"latitude\"", loaded[1].Meta, StringComparison.Ordinal);
        Assert.Null(loaded[0].Meta);
    }

    [Fact]
    public async Task SlotsLoadRecentWinnersAndJackpotsSeparately()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new SlotStore(test.Database);
        await store.InsertAsync(
        [
            new(At(1), "77777", true, true),
            new(At(2), "AAA12", true, false),
            new(At(3), "0A1B2", false, false),
            new(At(4), "0A1B3", false, false),
        ], token);
        var history = await store.LoadAsync(2, 10, 10, token);
        Assert.Equal(["0A1B3", "0A1B2"], history.Recent.Select(record => record.Text));
        Assert.Equal(["AAA12", "77777"], history.Winners.Select(record => record.Text));
        Assert.Equal(["77777"], history.Jackpots.Select(record => record.Text));
    }

    [Fact]
    public async Task LatencyLoadsRecentRunsAndEveryLeaderCandidate()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new LatencyStore(test.Database);
        await store.InsertAsync(
        [
            new(At(1), "fast", 5, null),
            new(At(2), "slow", 400, 120),
            new(At(3), "middle", 50, 50),
            new(At(4), "uncounted", null, null),
            new(At(5), "newest", 60, 60),
        ], token);
        var history = await store.LoadAsync(1, 1, 5, 10, 100, token);
        Assert.Equal(["newest"], history.Recent.Select(record => record.Text));
        Assert.Equal(["fast", "slow", "middle"], history.Leaders.Select(record => record.Text));
    }

    [Fact]
    public async Task CanvasLoadsStrokesAfterTheLatestReset()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new CanvasStore(test.Database);
        var empty = await store.LoadAsync(10, token);
        Assert.Null(empty.Reset);
        Assert.Empty(empty.Strokes);

        var reset = At(2);
        await store.AppendAsync([new(At(1), [1, 1]), new(At(0), null), new(reset, null), new(At(3), [2, 2, 3, 3]), new(At(4), [4, 4])], token);
        var state = await store.LoadAsync(1, token);
        Assert.Equal(reset, state.Reset);
        var stroke = Assert.Single(state.Strokes);
        Assert.Equal([4, 4], stroke.Points);
        Assert.Equal(2, (await store.LoadAsync(10, token)).Strokes.Count);
    }

    [Fact]
    public async Task PostcardsKeepTheirImages()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new PostcardStore(test.Database);
        Assert.Null(await store.LatestAsync(token));
        var older = new PostcardRow(At(1), "older", null, []);
        var newer = new PostcardRow(At(2), "newer", """[{"key":"latitude","value":"1"}]""", [new("image/png", 4, 3, [1, 2, 3]), new("image/gif", 2, 2, [4, 5])]);
        Assert.Equal(2, await store.InsertAsync([older, newer], token));
        Assert.Equal(0, await store.InsertAsync([newer], token));
        var latest = await store.LatestAsync(token);
        Assert.NotNull(latest);
        Assert.Equal("newer", latest.Text);
        Assert.Contains("latitude", latest.Meta, StringComparison.Ordinal);
        Assert.Equal(["image/png", "image/gif"], latest.Images.Select(image => image.ContentType));
        Assert.Equal([1, 2, 3], latest.Images[0].Data);
        Assert.Equal((4, 3), (latest.Images[0].Width, latest.Images[0].Height));
    }
}
