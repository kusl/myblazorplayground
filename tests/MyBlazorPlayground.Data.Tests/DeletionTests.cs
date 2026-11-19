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
        var (_, project) = await new ProjectStore(test.Database).CreateAsync("TRSH", "Trash", "", owner, TestDatabase.Audit(owner));
        var items = new WorkItemStore(test.Database);
        var (item, number) = await items.CreateAsync(project, new WorkItemFields(WorkItemKinds.Bug, "Doomed", "", WorkItemStatuses.Backlog, 2, null, null, null, null), owner);
        var deletions = new Deletions(test.Database);

        Assert.False(await deletions.HardDeleteAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "hard-delete")));
        Assert.True(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete")));
        Assert.False(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete")));
        Assert.Null(await items.FindAsync(project, number));
        Assert.NotNull(await items.FindAsync(project, number, includeDeleted: true));

        var trashed = Assert.Single(await deletions.ListAsync(null, 10));
        Assert.Equal(Subject.WorkItem, trashed.Subject);
        Assert.Equal("TRSH", trashed.ProjectKey);
        Assert.Equal(project, trashed.ProjectId);
        Assert.Equal("#1 Doomed", trashed.Title);
        Assert.Single(await deletions.ListAsync(project, 10));
        Assert.NotNull(await deletions.FindAsync(Subject.WorkItem, item));

        Assert.True(await deletions.RestoreAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "restore")));
        Assert.NotNull(await items.FindAsync(project, number));
        Assert.True(await deletions.SoftDeleteAsync(Subject.WorkItem, item, owner, TestDatabase.Audit(owner, "soft-delete")));
        Assert.True(await deletions.HardDeleteAsync(Subject.WorkItem, item, TestDatabase.Audit(owner, "hard-delete")));
        Assert.Null(await items.FindAsync(project, number, includeDeleted: true));
        Assert.Empty(await deletions.ListAsync(null, 10));
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
        Assert.Equal(2, await store.InsertAsync([first, second]));
        Assert.Equal(0, await store.InsertAsync([first]));

        var deletions = new Deletions(test.Database);
        Assert.True(await deletions.SoftDeleteAsync(Subject.NestEntry, first.Id, admin, TestDatabase.Audit(admin)));
        Assert.Single(await deletions.ListAsync(null, 10));
        Assert.True(await deletions.HardDeleteAsync(Subject.NestEntry, first.Id, TestDatabase.Audit(admin)));
        Assert.Empty(await deletions.ListAsync(null, 10));
        Assert.False(await deletions.RestoreAsync(Subject.NestEntry, first.Id, TestDatabase.Audit(admin)));

        var loaded = await store.LoadAsync(100);
        Assert.Equal(2, loaded.Count);
        var tombstone = loaded.Single(record => record.Id == first.Id);
        Assert.NotNull(tombstone.DeletedAt);
        Assert.Null(tombstone.Text);
        var live = loaded.Single(record => record.Id == second.Id);
        Assert.Equal(["zillow:1", "redfin:2"], live.Keys);
        Assert.Contains("450000", live.Fields!);

        Assert.Single(await store.ForKeysAsync(["redfin:2"]));
        Assert.Single(await store.ForKeysAsync(["zillow:1"]));
        Assert.NotNull(await store.FindAsync(second.Id));
    }

    private static async Task<string[]> AuditActionsAsync(TestDatabase test)
    {
        var log = new Audit.AuditLog(test.Database);
        return [.. (await log.ListAsync(100, null)).Where(entry => entry.SubjectType == "test" && entry.Action != "test").Reverse().Select(entry => entry.Action)];
    }
}
