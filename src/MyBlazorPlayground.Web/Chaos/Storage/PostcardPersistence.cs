using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Web.Chaos.Postcards;

namespace MyBlazorPlayground.Web.Chaos.Storage;

internal sealed class PostcardJournal(PostcardStore store, ILogger<PostcardJournal> logger) : WriteBehindJournal<StoredPostcard>(logger)
{
    protected override string Subject => "postcards";

    protected override Task WriteAsync(IReadOnlyList<StoredPostcard> batch, CancellationToken cancellationToken) =>
        store.InsertAsync([.. batch.Select(ToyStorage.ToPostcardRow)], cancellationToken);
}

internal sealed partial class PostcardLoader(PostcardStore store, PostcardService postcards, ILogger<PostcardLoader> logger) : IStateLoader
{
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        var latest = await store.LatestAsync(cancellationToken);
        var loaded = latest is not null
            && ToyStorage.TryMetaFromJson(latest.Meta, out var meta)
            && postcards.Load(latest.Id, latest.Text, [.. latest.Images.Select(image => image.Data)], meta);
        LogLoaded(logger, loaded);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded the newest postcard: {Loaded}")]
    private static partial void LogLoaded(ILogger logger, bool loaded);
}
