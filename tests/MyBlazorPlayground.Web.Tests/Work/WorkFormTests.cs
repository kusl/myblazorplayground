using MyBlazorPlayground.Domain.Work;
using MyBlazorPlayground.Web.Identity;
using MyBlazorPlayground.Web.Work;

namespace MyBlazorPlayground.Web.Tests.Work;

public sealed class WorkFormTests
{
    [Fact]
    public void PlannedItemsLeaveTheBacklogStatus()
    {
        var fields = new ItemForm { Title = "Ship it", Status = WorkItemStatuses.Backlog, SprintId = Guid.NewGuid() }.ToFields();
        Assert.Equal(WorkItemStatuses.ToDo, fields.Status);
    }

    [Fact]
    public void MissingFormValuesFallBackToDefaults()
    {
        var form = new ItemForm { Kind = null!, Status = "", Priority = 0, Title = "x" };
        Assert.Equal(WorkItemKinds.Story, form.Kind);
        Assert.Equal(WorkItemStatuses.Backlog, form.Status);
        Assert.Equal(Priorities.Medium, form.Priority);
        Assert.Null(form.Problem());
    }

    [Fact]
    public void EpicsHaveNoParent()
    {
        var fields = new ItemForm { Title = "Big", Kind = WorkItemKinds.Epic, ParentId = Guid.NewGuid() }.ToFields();
        Assert.Null(fields.ParentId);
    }

    [Fact]
    public void ReportsProblems()
    {
        Assert.NotNull(new ItemForm { Title = " " }.Problem());
        Assert.NotNull(new ItemForm { Title = "x", Kind = "saga" }.Problem());
        Assert.NotNull(new ItemForm { Title = "x", Points = -1 }.Problem());
        Assert.NotNull(new ItemForm { Title = "x", Priority = 9 }.Problem());
        Assert.Null(new ItemForm { Title = "x", Points = 8 }.Problem());
    }

    [Fact]
    public void TrimsTitlesAndDescriptions()
    {
        var fields = new ItemForm { Title = "  two   spaces \n", Description = "line\r\n\r\n\r\n\r\n\r\nnext  " }.ToFields();
        Assert.Equal("two spaces", fields.Title);
        Assert.Equal("line\n\n\nnext", fields.Description);
    }

    [Fact]
    public void SprintsNeedANameAndOrderedDates()
    {
        Assert.NotNull(new SprintForm { Name = "" }.Problem());
        Assert.NotNull(new SprintForm { Name = "S1", StartsOn = new DateOnly(2026, 11, 20), EndsOn = new DateOnly(2026, 11, 19) }.Problem());
        Assert.Null(new SprintForm { Name = "S1", StartsOn = new DateOnly(2026, 11, 19), EndsOn = new DateOnly(2026, 12, 3) }.Problem());
    }

    [Theory]
    [InlineData("/work/ABC", "/work/ABC")]
    [InlineData("/", "/")]
    [InlineData(null, "/work")]
    [InlineData("", "/work")]
    [InlineData("//evil.example", "/work")]
    [InlineData("/\\evil.example", "/work")]
    [InlineData("https://evil.example/", "/work")]
    [InlineData("work", "/work")]
    public void OnlyFollowsLocalReturnUrls(string? url, string expected)
    {
        Assert.Equal(expected, LocalUrl.Or(url, "/work"));
    }
}
