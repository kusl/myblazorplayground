using MyBlazorPlayground.Data.People;

namespace MyBlazorPlayground.Data.Tests;

public sealed class TwoFactorStoreTests
{
    [Fact]
    public async Task EnrollEnableUseAndDisable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var people = new PersonStore(test.Database);
        var store = new TwoFactorStore(test.Database);
        var id = await test.PersonAsync("dana");
        var stamp = (await people.FindAsync(id, token))!.SecurityStamp;

        Assert.Null(await store.FindAsync(id, token));
        Assert.True(await store.BeginAsync(id, "secret-1", token));
        Assert.True(await store.BeginAsync(id, "secret-2", token));
        var pending = await store.FindAsync(id, token);
        Assert.Equal("secret-2", pending!.Secret);
        Assert.False(pending.IsEnabled);
        Assert.False((await people.FindAsync(id, token))!.HasTwoFactor);
        Assert.False(await store.TryUseStepAsync(id, 5, token));

        Assert.True(await store.EnableAsync(id, 10, ["h1", "h2"], TestDatabase.Audit(id, "two-factor-enabled"), token));
        Assert.False(await store.EnableAsync(id, 11, ["h3"], TestDatabase.Audit(id), token));
        var person = await people.FindAsync(id, token);
        Assert.True(person!.HasTwoFactor);
        Assert.NotEqual(stamp, person.SecurityStamp);
        Assert.False(await store.BeginAsync(id, "secret-3", token));
        Assert.Equal("secret-2", (await store.FindAsync(id, token))!.Secret);
        Assert.Equal(2, (await store.FindAsync(id, token))!.RecoveryCodesLeft);

        Assert.False(await store.TryUseStepAsync(id, 10, token));
        Assert.True(await store.TryUseStepAsync(id, 11, token));
        Assert.False(await store.TryUseStepAsync(id, 11, token));

        Assert.True(await store.TryUseRecoveryCodeAsync(id, "h1", token));
        Assert.False(await store.TryUseRecoveryCodeAsync(id, "h1", token));
        Assert.False(await store.TryUseRecoveryCodeAsync(id, "nope", token));
        Assert.Equal(1, (await store.FindAsync(id, token))!.RecoveryCodesLeft);

        Assert.True(await store.ReplaceRecoveryCodesAsync(id, ["a", "b", "c"], TestDatabase.Audit(id), token));
        Assert.Equal(3, (await store.FindAsync(id, token))!.RecoveryCodesLeft);
        Assert.False(await store.TryUseRecoveryCodeAsync(id, "h2", token));

        Assert.True(await store.DisableAsync(id, TestDatabase.Audit(id, "two-factor-disabled"), token));
        Assert.False(await store.DisableAsync(id, TestDatabase.Audit(id), token));
        Assert.Null(await store.FindAsync(id, token));
        Assert.False((await people.FindAsync(id, token))!.HasTwoFactor);
        Assert.False(await store.ReplaceRecoveryCodesAsync(id, ["x"], TestDatabase.Audit(id), token));
        Assert.Equal(0L, await test.ScalarAsync<long>("SELECT count(*) FROM person_recovery_code WHERE person_id = @id", new { id }));
        Assert.Equal(2L, await test.ScalarAsync<long>("SELECT count(*) FROM audit_event WHERE action IN ('two-factor-enabled', 'two-factor-disabled')"));
    }

    [Fact]
    public async Task CancelOnlyRemovesPendingSetups()
    {
        var token = TestContext.Current.CancellationToken;
        await using var test = await TestDatabase.CreateAsync();
        var store = new TwoFactorStore(test.Database);
        var id = await test.PersonAsync("erin");
        await store.BeginAsync(id, "s", token);
        Assert.True(await store.CancelAsync(id, token));
        Assert.Null(await store.FindAsync(id, token));
        await store.BeginAsync(id, "s", token);
        await store.EnableAsync(id, 1, ["h"], TestDatabase.Audit(id), token);
        Assert.False(await store.CancelAsync(id, token));
        Assert.NotNull(await store.FindAsync(id, token));
    }
}
