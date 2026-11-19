using System.Security.Cryptography;
using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Data.Tests;

public sealed class WorkStoreTests
{
    private static WorkItemFields Fields(string title, string kind = WorkItemKinds.Story, Guid? sprint = null, Guid? assignee = null, Guid? parent = null) =>
        new(kind, title, "", WorkItemStatuses.Backlog, Priorities.Medium, null, assignee, sprint, parent);

    [Fact]
    public async Task ProjectsHaveUniqueKeysAndTheirCreatorAsEditor()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        var stranger = await test.PersonAsync("stranger");
        var projects = new ProjectStore(test.Database);
        var (created, id) = await projects.CreateAsync("PAMS", "Pams", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        Assert.Equal(CreateProjectResult.Created, created);
        Assert.Equal(CreateProjectResult.KeyTaken, (await projects.CreateAsync("PAMS", "Again", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken)).Result);
        var mine = Assert.Single(await projects.ListAsync(owner, false, TestContext.Current.CancellationToken));
        Assert.Equal(PageRoles.Editor, mine.Role);
        Assert.Equal(1, mine.MemberCount);
        Assert.Empty(await projects.ListAsync(stranger, false, TestContext.Current.CancellationToken));
        Assert.Single(await projects.ListAsync(stranger, true, TestContext.Current.CancellationToken));
        Assert.Null((await projects.FindAsync("PAMS", stranger, TestContext.Current.CancellationToken))!.Role);
        Assert.Equal(id, (await projects.FindByIdAsync(id, owner, TestContext.Current.CancellationToken))!.Id);
        Assert.Equal(1L, await test.ScalarAsync<long>("SELECT count(*) FROM audit_event WHERE subject_id = @id", new { id = id.ToString() }));
    }

    [Fact]
    public async Task ProjectsKeepAnEditor()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        await test.PersonAsync("helper");
        var projects = new ProjectStore(test.Database);
        var (_, id) = await projects.CreateAsync("TEAM", "Team", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        Assert.Equal(MemberChange.Changed, await projects.SetMemberAsync(id, "helper", PageRoles.Member, owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
        Assert.Equal(MemberChange.Unchanged, await projects.SetMemberAsync(id, "helper", PageRoles.Member, owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
        Assert.Equal(MemberChange.NotFound, await projects.SetMemberAsync(id, "nobody", PageRoles.Member, owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
        Assert.Equal(MemberChange.LastEditor, await projects.SetMemberAsync(id, "owner", PageRoles.Member, owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
        Assert.Equal(MemberChange.LastEditor, await projects.RemoveMemberAsync(id, owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
        var members = await projects.MembersAsync(id, TestContext.Current.CancellationToken);
        Assert.Equal(2, members.Count);
        var helper = members.Single(member => member.UserName == "helper");
        Assert.Equal(MemberChange.Changed, await projects.RemoveMemberAsync(id, helper.PersonId, TestDatabase.Audit(owner), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ItemsAreNumberedRankedAndTracked()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        var outsider = await test.PersonAsync("outsider");
        var projects = new ProjectStore(test.Database);
        var items = new WorkItemStore(test.Database);
        var (_, project) = await projects.CreateAsync("ITEM", "Items", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);

        var (epicId, epic) = await items.CreateAsync(project, Fields("Epic", WorkItemKinds.Epic), owner, TestContext.Current.CancellationToken);
        var (_, first) = await items.CreateAsync(project, Fields("First", parent: epicId, assignee: outsider), owner, TestContext.Current.CancellationToken);
        var (_, second) = await items.CreateAsync(project, Fields("Second"), owner, TestContext.Current.CancellationToken);
        Assert.Equal([1, 2, 3], new[] { epic, first, second });

        var created = (await items.FindAsync(project, first, cancellationToken: TestContext.Current.CancellationToken))!;
        Assert.Null(created.AssigneeId);
        Assert.Equal(epicId, created.ParentId);
        Assert.Equal("OWNER", created.ReporterName);

        Assert.True(await items.UpdateAsync(project, first, current => current with { Status = WorkItemStatuses.InProgress, AssigneeId = owner, Title = "First!" }, owner, TestContext.Current.CancellationToken));
        var updated = (await items.FindAsync(project, first, cancellationToken: TestContext.Current.CancellationToken))!;
        Assert.Equal(WorkItemStatuses.InProgress, updated.Status);
        Assert.Equal(owner, updated.AssigneeId);
        var history = await items.HistoryAsync(updated.Id, TestContext.Current.CancellationToken);
        Assert.Equal(["created", "title", "status", "assignee"], history.Select(change => change.Field));
        Assert.Equal("In progress", history.Single(change => change.Field == "status").NewValue);
        Assert.Equal("OWNER", history.Single(change => change.Field == "assignee").NewValue);

        Assert.True(await items.UpdateAsync(project, first, current => current, owner, TestContext.Current.CancellationToken));
        Assert.Equal(4, (await items.HistoryAsync(updated.Id, TestContext.Current.CancellationToken)).Count);

        Assert.Equal([epic, first, second], (await items.ListAsync(new WorkItemQuery(project), TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.True(await items.MoveAsync(project, second, true, TestContext.Current.CancellationToken));
        Assert.Equal([epic, second, first], (await items.ListAsync(new WorkItemQuery(project), TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.False(await items.MoveAsync(project, epic, true, TestContext.Current.CancellationToken));

        Assert.Equal([first], (await items.ListAsync(new WorkItemQuery(project, Text: "first!"), TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.Equal([second], (await items.ListAsync(new WorkItemQuery(project, Text: "#3"), TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.Equal([epic], (await items.EpicsAsync(project, TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.Equal([first], (await items.ChildrenAsync(epicId, TestContext.Current.CancellationToken)).Select(item => item.Number));
        Assert.Equal([first], (await items.AssignedToAsync(owner, 10, TestContext.Current.CancellationToken)).Select(item => item.Number));
    }

    [Fact]
    public async Task OneSprintIsActiveAndCompletingReturnsOpenWork()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        var projects = new ProjectStore(test.Database);
        var sprints = new SprintStore(test.Database);
        var items = new WorkItemStore(test.Database);
        var (_, project) = await projects.CreateAsync("SPRT", "Sprints", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        var one = await sprints.CreateAsync(project, new SprintDraft("One", "Goal", new DateOnly(2026, 11, 19), new DateOnly(2026, 12, 2)), TestContext.Current.CancellationToken);
        var two = await sprints.CreateAsync(project, new SprintDraft("Two", "", null, null), TestContext.Current.CancellationToken);
        var (_, open) = await items.CreateAsync(project, Fields("Open", sprint: one) with { Status = WorkItemStatuses.ToDo, Points = 3 }, owner, TestContext.Current.CancellationToken);
        var (_, done) = await items.CreateAsync(project, Fields("Done", sprint: one) with { Status = WorkItemStatuses.Done, Points = 5 }, owner, TestContext.Current.CancellationToken);

        Assert.Equal(SprintChange.Changed, await sprints.StartAsync(project, one, TestContext.Current.CancellationToken));
        Assert.Equal(SprintChange.AnotherIsActive, await sprints.StartAsync(project, two, TestContext.Current.CancellationToken));
        var active = (await sprints.FindAsync(project, one, TestContext.Current.CancellationToken))!;
        Assert.Equal(SprintStates.Active, active.State);
        Assert.Equal(new DateOnly(2026, 11, 19), active.Starts);
        Assert.Equal(2, active.ItemCount);
        Assert.Equal(1, active.DoneCount);
        Assert.Equal(8, active.Points);
        Assert.Equal(5, active.DonePoints);

        Assert.Equal(SprintChange.Changed, await sprints.CompleteAsync(project, one, null, TestContext.Current.CancellationToken));
        Assert.Equal(SprintChange.WrongState, await sprints.CompleteAsync(project, one, null, TestContext.Current.CancellationToken));
        var returned = (await items.FindAsync(project, open, cancellationToken: TestContext.Current.CancellationToken))!;
        Assert.Null(returned.SprintId);
        Assert.Equal(WorkItemStatuses.Backlog, returned.Status);
        Assert.Equal(one, (await items.FindAsync(project, done, cancellationToken: TestContext.Current.CancellationToken))!.SprintId);

        Assert.Equal(SprintChange.Changed, await sprints.StartAsync(project, two, TestContext.Current.CancellationToken));
        Assert.Equal(["Two", "One"], (await sprints.ListAsync(project, TestContext.Current.CancellationToken)).Select(sprint => sprint.Name));
        Assert.True(await sprints.UpdateAsync(project, two, new SprintDraft("Two b", "Ship", null, new DateOnly(2026, 12, 31)), TestContext.Current.CancellationToken));
        Assert.Equal("Two b", (await sprints.FindAsync(project, two, TestContext.Current.CancellationToken))!.Name);
    }

    [Fact]
    public async Task CommentsCanBeAddedAndEdited()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        var (_, project) = await new ProjectStore(test.Database).CreateAsync("CMNT", "Comments", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        var (item, _) = await new WorkItemStore(test.Database).CreateAsync(project, Fields("Talk"), owner, TestContext.Current.CancellationToken);
        var comments = new CommentStore(test.Database);
        var id = await comments.AddAsync(item, owner, "It's **done**; isn't it?", TestContext.Current.CancellationToken);
        Assert.True(await comments.EditAsync(item, id, "Edited", TestContext.Current.CancellationToken));
        Assert.False(await comments.EditAsync(item, id, "Edited", TestContext.Current.CancellationToken));
        var comment = Assert.Single(await comments.ListAsync(item, TestContext.Current.CancellationToken));
        Assert.Equal("Edited", comment.Body);
        Assert.NotNull(comment.EditedAt);
        Assert.Equal("OWNER", comment.AuthorName);
    }

    [Fact]
    public async Task AttachmentsAreStoredInChunks()
    {
        await using var test = await TestDatabase.CreateAsync();
        var owner = await test.PersonAsync("owner");
        var (_, project) = await new ProjectStore(test.Database).CreateAsync("FILE", "Files", "", owner, TestDatabase.Audit(owner), TestContext.Current.CancellationToken);
        var (item, _) = await new WorkItemStore(test.Database).CreateAsync(project, Fields("Files"), owner, TestContext.Current.CancellationToken);
        var attachments = new AttachmentStore(test.Database);
        var bytes = new byte[(AttachmentStore.ChunkSize * 2) + 1234];
        RandomNumberGenerator.Fill(bytes);
        bytes[0] = 0x89;
        "PNG\r\n\u001a\n"u8.CopyTo(bytes.AsSpan(1));

        var stored = await attachments.UploadAsync(new AttachmentUpload(project, item, owner, "shot.png"), new MemoryStream(bytes), bytes.Length, TestContext.Current.CancellationToken);
        Assert.Equal(UploadOutcome.Stored, stored.Outcome);
        Assert.Equal("image/png", stored.Attachment!.ContentType);
        Assert.Equal(bytes.Length, stored.Attachment.Length);
        Assert.Equal(3L, await test.ScalarAsync<long>("SELECT count(*) FROM attachment_chunk WHERE attachment_id = @id", new { id = stored.Attachment.Id }));

        var copy = new MemoryStream();
        await attachments.CopyToAsync(stored.Attachment.Id, copy, TestContext.Current.CancellationToken);
        Assert.Equal(bytes, copy.ToArray());
        Assert.Single(await attachments.ListAsync(item, TestContext.Current.CancellationToken));

        Assert.Equal(UploadOutcome.TooLarge, (await attachments.UploadAsync(new AttachmentUpload(project, item, owner, "big.bin"), new MemoryStream(bytes), 1000, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(UploadOutcome.Empty, (await attachments.UploadAsync(new AttachmentUpload(project, item, owner, "none.bin"), new MemoryStream(), 1000, TestContext.Current.CancellationToken)).Outcome);
        Assert.Equal(1L, await test.ScalarAsync<long>("SELECT count(*) FROM attachment"));
    }
}
