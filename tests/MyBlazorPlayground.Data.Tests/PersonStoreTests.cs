using MyBlazorPlayground.Data.People;

namespace MyBlazorPlayground.Data.Tests;

public sealed class PersonStoreTests
{
    [Fact]
    public async Task OnlyOneFirstAdministrator()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        Assert.False(await people.HasAdministratorAsync());
        var (first, id) = await people.CreateAsync(new NewPerson("alice", "Alice", "hash"), true);
        var (second, _) = await people.CreateAsync(new NewPerson("bob", "Bob", "hash"), true);
        Assert.Equal(CreatePersonResult.Created, first);
        Assert.Equal(CreatePersonResult.SetupAlreadyDone, second);
        Assert.True(await people.HasAdministratorAsync());
        var alice = await people.FindByUserNameAsync("alice");
        Assert.NotNull(alice);
        Assert.Equal(id, alice.Id);
        Assert.True(alice.IsAdministrator);
        Assert.Null(await people.FindByUserNameAsync("bob"));
    }

    [Fact]
    public async Task UserNamesAreUnique()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        Assert.Equal(CreatePersonResult.Created, (await people.CreateAsync(new NewPerson("carol", "Carol", "hash"), false)).Result);
        Assert.Equal(CreatePersonResult.UserNameTaken, (await people.CreateAsync(new NewPerson("carol", "Other", "hash"), false)).Result);
    }

    [Fact]
    public async Task KeepsAtLeastOneActiveAdministrator()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        var admin = await test.PersonAsync("root", true);
        var other = await test.PersonAsync("other");
        Assert.False(await people.SetAdministratorAsync(admin, false, admin));
        Assert.False(await people.SetDisabledAsync(admin, true));
        Assert.True(await people.SetAdministratorAsync(other, true, admin));
        Assert.True(await people.SetAdministratorAsync(admin, false, other));
        Assert.True(await people.SetDisabledAsync(admin, true));
        var disabled = await people.FindAsync(admin);
        Assert.NotNull(disabled?.DisabledAt);
        Assert.True(await people.SetDisabledAsync(admin, false));
    }

    [Fact]
    public async Task PasswordChangesRotateTheStamp()
    {
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        var id = await test.PersonAsync("dave");
        var before = (await people.FindAsync(id))!.SecurityStamp;
        await people.UpdatePasswordAsync(id, "new-hash");
        var after = (await people.FindAsync(id))!;
        Assert.NotEqual(before, after.SecurityStamp);
        Assert.Equal("new-hash", after.PasswordHash);
        await people.RecordSignInAsync(id, null);
        Assert.NotNull((await people.FindAsync(id))!.LastSignInAt);
        Assert.Single(await people.SearchAsync("DAV", 10));
    }
}
