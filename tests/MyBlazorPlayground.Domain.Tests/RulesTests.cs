using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Domain.Accounts;
using MyBlazorPlayground.Domain.Files;
using MyBlazorPlayground.Domain.Text;
using MyBlazorPlayground.Domain.Work;

namespace MyBlazorPlayground.Domain.Tests;

public sealed class AccountRulesTests
{
    [Theory]
    [InlineData("kushal", null)]
    [InlineData("Kushal.H", null)]
    [InlineData("ab", "length")]
    [InlineData("-dash", "pattern")]
    [InlineData("has space", "pattern")]
    [InlineData("ümlaut", "pattern")]
    public void ChecksUserNames(string name, string? problem)
    {
        Assert.Equal(problem is null, AccountRules.UserNameProblem(name) is null);
    }

    [Fact]
    public void ChecksPasswords()
    {
        Assert.NotNull(AccountRules.PasswordProblem("short", "a"));
        Assert.NotNull(AccountRules.PasswordProblem("aaaaaaaaaaaaaaaa", "a"));
        Assert.NotNull(AccountRules.PasswordProblem("kushalkushal", "KushalKushal"));
        Assert.NotNull(AccountRules.PasswordProblem(new string('x', 300) + "abcd", "a"));
        Assert.Null(AccountRules.PasswordProblem("correct horse battery staple", "kushal"));
    }

    [Fact]
    public void FallsBackToTheUserNameForDisplay()
    {
        Assert.Equal("kushal", AccountRules.NormalizeDisplayName("   ", "Kushal"));
        Assert.Equal("Kushal H", AccountRules.NormalizeDisplayName("  Kushal \n H ", "kushal"));
    }
}

public sealed class AccessPolicyTests
{
    private static readonly Actor Member = new(Guid.NewGuid(), "m", "M", false);
    private static readonly Actor Admin = new(Guid.NewGuid(), "a", "A", true);

    [Fact]
    public void AnonymousPeopleCannotDeleteOrEdit()
    {
        Assert.False(AccessPolicy.CanView(null, PageRoles.Editor));
        Assert.False(AccessPolicy.CanSoftDelete(null, PageRoles.Editor));
        Assert.False(AccessPolicy.CanHardDelete(null));
    }

    [Fact]
    public void MembersEditButDoNotDelete()
    {
        Assert.True(AccessPolicy.CanEdit(Member, PageRoles.Member));
        Assert.False(AccessPolicy.CanSoftDelete(Member, PageRoles.Member));
        Assert.False(AccessPolicy.CanEdit(Member, null));
    }

    [Fact]
    public void EditorsSoftDeleteOnlyAdministratorsHardDelete()
    {
        Assert.True(AccessPolicy.CanSoftDelete(Member, PageRoles.Editor));
        Assert.False(AccessPolicy.CanHardDelete(Member));
        Assert.True(AccessPolicy.CanSoftDelete(Admin, null));
        Assert.True(AccessPolicy.CanHardDelete(Admin));
        Assert.True(AccessPolicy.CanView(Admin, null));
    }

    [Fact]
    public void AuthorsAndEditorsEditComments()
    {
        Assert.True(AccessPolicy.CanEditComment(Member, PageRoles.Member, Member.PersonId));
        Assert.False(AccessPolicy.CanEditComment(Member, PageRoles.Member, Guid.NewGuid()));
        Assert.True(AccessPolicy.CanEditComment(Member, PageRoles.Editor, Guid.NewGuid()));
    }
}

public sealed class WorkRulesTests
{
    [Theory]
    [InlineData("PAMS", true)]
    [InlineData("pams", true)]
    [InlineData("P1", true)]
    [InlineData("1P", false)]
    [InlineData("P", false)]
    [InlineData("TOOLONGKEY1", false)]
    [InlineData("PA-MS", false)]
    public void ChecksProjectKeys(string key, bool ok)
    {
        Assert.Equal(ok, WorkRules.ProjectKeyProblem(key) is null);
    }

    [Fact]
    public void KnowsItsVocabulary()
    {
        Assert.All(WorkItemKinds.All, kind => Assert.True(WorkItemKinds.IsKnown(kind)));
        Assert.All(WorkItemStatuses.Board, status => Assert.True(WorkItemStatuses.IsKnown(status)));
        Assert.DoesNotContain(WorkItemStatuses.Backlog, WorkItemStatuses.Board);
        Assert.False(Priorities.IsKnown(0));
        Assert.Equal("In progress", WorkItemStatuses.Label(WorkItemStatuses.InProgress));
    }
}

public sealed class FileRulesTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("C:\\temp\\a.txt", "a.txt")]
    [InlineData("a\"b<c>.png", "a_b_c_.png")]
    [InlineData("...", "file")]
    [InlineData("", "file")]
    public void CleansFileNames(string name, string expected)
    {
        Assert.Equal(expected, FileRules.SafeFileName(name));
    }

    [Fact]
    public void KeepsExtensionsWhenCutting()
    {
        var name = FileRules.SafeFileName(new string('a', 300) + ".png");
        Assert.Equal(FileRules.MaxFileName, name.Length);
        Assert.EndsWith(".png", name);
    }

    [Fact]
    public void SniffsImagesFromBytes()
    {
        Assert.Equal("image/png", FileRules.SniffImage([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]));
        Assert.Equal("image/jpeg", FileRules.SniffImage([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal("image/gif", FileRules.SniffImage("GIF89a.."u8));
        Assert.Equal("image/webp", FileRules.SniffImage("RIFF0000WEBPVP8 "u8));
        Assert.Null(FileRules.SniffImage("<svg xmlns"u8));
    }

    [Fact]
    public void DescribesSizes()
    {
        Assert.Equal("512 B", FileRules.Size(512));
        Assert.Equal("1.5 KB", FileRules.Size(1536));
        Assert.Equal("95 MB", FileRules.Size(95L * 1024 * 1024));
    }
}

public sealed class PlainTextTests
{
    [Fact]
    public void LinesCollapseWhitespaceAndControls()
    {
        Assert.Equal("a b c", PlainText.Line(" a \t b\u0007\nc ", 50));
        Assert.Equal("", PlainText.Line(null, 50));
    }

    [Fact]
    public void CutsOnGraphemeBoundaries()
    {
        Assert.Equal("ab", PlainText.Cut("ab👍🏽", 3));
        Assert.Equal("ab👍🏽", PlainText.Cut("ab👍🏽", 6));
    }
}
