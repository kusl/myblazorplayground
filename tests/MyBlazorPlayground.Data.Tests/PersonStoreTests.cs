using MyBlazorPlayground.Data.People;

namespace MyBlazorPlayground.Data.Tests;

public sealed class PersonStoreTests
{
    [Fact]
    public async Task OnlyOneFirstAdministrator()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        Assert.False(await people.HasAdministratorAsync(TestContext.Current.CancellationToken));
        var (first, id) = await people.CreateAsync(new NewPerson("alice", "Alice", "hash"), true, TestContext.Current.CancellationToken);
        var (second, _) = await people.CreateAsync(new NewPerson("bob", "Bob", "hash"), true, TestContext.Current.CancellationToken);
        Assert.Equal(CreatePersonResult.Created, first);
        Assert.Equal(CreatePersonResult.SetupAlreadyDone, second);
        Assert.True(await people.HasAdministratorAsync(TestContext.Current.CancellationToken));
        var alice = await people.FindByUserNameAsync("alice", TestContext.Current.CancellationToken);
        Assert.NotNull(alice);
        Assert.Equal(id, alice.Id);
        Assert.True(alice.IsAdministrator);
        Assert.Null(await people.FindByUserNameAsync("bob", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UserNamesAreUnique()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        Assert.Equal(CreatePersonResult.Created, (await people.CreateAsync(new NewPerson("carol", "Carol", "hash"), false, TestContext.Current.CancellationToken)).Result);
        Assert.Equal(CreatePersonResult.UserNameTaken, (await people.CreateAsync(new NewPerson("carol", "Other", "hash"), false, TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task KeepsAtLeastOneActiveAdministrator()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        var admin = await test.PersonAsync("root", true);
        var other = await test.PersonAsync("other");
        Assert.False(await people.SetAdministratorAsync(admin, false, admin, TestContext.Current.CancellationToken));
        Assert.False(await people.SetDisabledAsync(admin, true, TestContext.Current.CancellationToken));
        Assert.True(await people.SetAdministratorAsync(other, true, admin, TestContext.Current.CancellationToken));
        Assert.True(await people.SetAdministratorAsync(admin, false, other, TestContext.Current.CancellationToken));
        Assert.True(await people.SetDisabledAsync(admin, true, TestContext.Current.CancellationToken));
        var disabled = await people.FindAsync(admin, TestContext.Current.CancellationToken);
        Assert.NotNull(disabled?.DisabledAt);
        Assert.True(await people.SetDisabledAsync(admin, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PasswordChangesRotateTheStamp()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        var id = await test.PersonAsync("dave");
        var before = (await people.FindAsync(id, TestContext.Current.CancellationToken))!.SecurityStamp;
        await people.UpdatePasswordAsync(id, "new-hash", TestContext.Current.CancellationToken);
        var after = (await people.FindAsync(id, TestContext.Current.CancellationToken))!;
        Assert.NotEqual(before, after.SecurityStamp);
        Assert.Equal("new-hash", after.PasswordHash);
        await people.RecordSignInAsync(id, null, TestContext.Current.CancellationToken);
        Assert.NotNull((await people.FindAsync(id, TestContext.Current.CancellationToken))!.LastSignInAt);
        Assert.Single(await people.SearchAsync("DAV", 10, TestContext.Current.CancellationToken));
    }
}
