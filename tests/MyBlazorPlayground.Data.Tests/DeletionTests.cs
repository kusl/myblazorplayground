using MyBlazorPlayground.Data.Nest;
using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Data.Tests;

public sealed class DeletionTests
{
    [Fact]
    public async Task SoftDeleteRestoreAndPurgeAreAudited()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner", true);
        var (_, project) = await new ProjectStore(test.Database).CreateAsync("TRSH", "Trash", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        var items = new WorkItemStore(test.Database);
        var (item, number) = await items.CreateAsync(project, new WorkItemFields(WorkItemKinds.Bug, "Doomed", "", WorkItemStatuses.Backlog, 2, null, null, null, null), owner, TestContext.Current.CancellationToken);
        var deletions = new Deletions(test.Database);

        Assert.False(await deletions.HardDeleteAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "hard-delete"), TestContext.Current.CancellationToken));
        Assert.True(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete"), TestContext.Current.CancellationToken));
        Assert.False(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete"), TestContext.Current.CancellationToken));
        Assert.Null(await items.FindAsync(project, number, cancellationToken: TestContext.Current.CancellationToken));
        Assert.NotNull(await items.FindAsync(project, number, includeDeleted: true, TestContext.Current.CancellationToken));

        var trashed = Assert.Single(await deletions.ListAsync(null, 10, TestContext.Current.CancellationToken));
        Assert.Equal(Subject.WorkItem, trashed.Subject);
        Assert.Equal("TRSH", trashed.ProjectKey);
        Assert.Equal(project, trashed.ProjectId);
        Assert.Equal("#1 Doomed", trashed.Title);
        Assert.Single(await deletions.ListAsync(project, 10, TestContext.Current.CancellationToken));
        Assert.NotNull(await deletions.FindAsync(Subject.WorkItem, item, TestContext.Current.CancellationToken));

        Assert.True(await deletions.RestoreAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "restore"), TestContext.Current.CancellationToken));
        Assert.NotNull(await items.FindAsync(project, number, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete"), TestContext.Current.CancellationToken));
        Assert.True(await deletions.HardDeleteAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "hard-delete"), TestContext.Current.CancellationToken));
        Assert.Null(await items.FindAsync(project, number, includeDeleted: true, TestContext.Current.CancellationToken));
        Assert.Empty(await deletions.ListAsync(null, 10, TestContext.Current.CancellationToken));
        Assert.Equal(["soft-delete", "restore", "soft-delete", "hard-delete"],
            await AuditActionsAsync(test));
    }

    [Fact]
    public async Task PurgedHomeEntriesStayAsTombstones()
    {
        await using var test = await TestDatabase.CreateAsync();
        var admin = await test.PersonAsync("admin", true);
        var store = new NestEntryStore(test.Database);
        var first = new NestEntryRow(Guid.CreateVersion7(), "comment", ["zillow:1"], null, null, "spam");
        var second = new NestEntryRow(Guid.CreateVersion7(), "fact", ["zillow:1", "redfin:2"], null, "[{\"key\":\"price\",\"value\":\"450000\"}]", null);
        Assert.Equal(2, await store.InsertAsync([first, second], TestContext.Current.CancellationToken));
        Assert.Equal(0, await store.InsertAsync([first], TestContext.Current.CancellationToken));

        var deletions = new Deletions(test.Database);
        Assert.True(await deletions.SoftDeleteAsync(Subject.NestEntry, first.Id, admin, TestDatabase.Audit(admin), TestContext.Current.CancellationToken));
        Assert.Single(await deletions.ListAsync(null, 10, TestContext.Current.CancellationToken));
        Assert.True(await deletions.HardDeleteAsync(Subject.NestEntry, first.Id, TestDatabase.Audit(admin), TestContext.Current.CancellationToken));
        Assert.Empty(await deletions.ListAsync(null, 10, TestContext.Current.CancellationToken));
        Assert.False(await deletions.RestoreAsync(Subject.NestEntry, first.Id, TestDatabase.Audit(admin), TestContext.Current.CancellationToken));

        var loaded = await store.LoadAsync(100, TestContext.Current.CancellationToken);
        Assert.Equal(2, loaded.Count);
        var tombstone = loaded.Single(record => record.Id == first.Id);
        Assert.NotNull(tombstone.DeletedAt);
        Assert.Null(tombstone.Text);
        var live = loaded.Single(record => record.Id == second.Id);
        Assert.Equal(["zillow:1", "redfin:2"], live.Keys);
        Assert.Contains("450000", live.Fields!);

        Assert.Single(await store.ForKeysAsync(["redfin:2"], TestContext.Current.CancellationToken));
        Assert.Single(await store.ForKeysAsync(["zillow:1"], TestContext.Current.CancellationToken));
        Assert.NotNull(await store.FindAsync(second.Id, TestContext.Current.CancellationToken));
    }

    private static async Task<string[]> AuditActionsAsync(TestDatabase test)
    {
        var log = new Audit.AuditLog(test.Database);
        return [.. (await log.ListAsync(100, null, TestContext.Current.CancellationToken)).Where(entry => entry.SubjectType == "test" && entry.Action != "test").Reverse().Select(entry => entry.Action)];
    }
}
