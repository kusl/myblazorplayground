using Microsoft.Extensions.Logging.Abstractions;

namespace MyBlazorPlayground.Data.Tests;

public sealed class MigrationTests
{
    [Fact]
    public void EmbedsNumberedMigrationsInOrder()
    {
        var migrations = MigrationRunner.Embedded();
        Assert.NotEmpty(migrations);
        Assert.Equal(1, migrations[0].Version);
        Assert.Equal(migrations.Select(migration => migration.Version).Order(), migrations.Select(migration => migration.Version));
        Assert.All(migrations, migration => Assert.False(string.IsNullOrWhiteSpace(migration.Sql)));
    }

    [Fact]
    public async Task AppliesOnceAndIsIdempotent()
    {
        await using var test = await TestDatabase.CreateAsync();
        await new MigrationRunner(test.Database, NullLogger<MigrationRunner>.Instance).RunAsync(1, TimeSpan.Zero, TestContext.Current.CancellationToken);
        Assert.Equal(MigrationRunner.Embedded().Count, (int)await test.ScalarAsync<long>("SELECT count(*) FROM schema_migration"));
    }
}
