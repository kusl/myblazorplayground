using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Data.People;
using Npgsql;

namespace MyBlazorPlayground.Data.Tests;

internal sealed partial class TestDatabase : IAsyncDisposable
{
    public const string Variable = "PLAYGROUND_TEST_DATABASE";
    private readonly string _admin;
    private readonly string _name;
    private readonly NpgsqlDataSource _source;

    private TestDatabase(string admin, string name, NpgsqlDataSource source)
    {
        _admin = admin;
        _name = name;
        _source = source;
        Database = new Database(source);
    }

    public Database Database { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var admin = Environment.GetEnvironmentVariable(Variable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(admin), $"Set {Variable} to a PostgreSQL connection string that may create databases.");
        var name = "playground_test_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(admin!, $"CREATE DATABASE {name}");
        var source = new NpgsqlDataSourceBuilder(DatabaseName().Replace(admin!, "Database=" + name)).Build();
        var database = new TestDatabase(admin!, name, source);
        await new MigrationRunner(database.Database, NullLogger<MigrationRunner>.Instance).RunAsync(1, TimeSpan.Zero, TestContext.Current.CancellationToken);
        return database;
    }

    public async Task<Guid> PersonAsync(string userName, bool administrator = false)
    {
        var (_, id) = await new PersonStore(Database).CreateAsync(new NewPerson(userName, userName.ToUpperInvariant(), "pbkdf2-sha512$100000$AAAA$AAAA"), administrator);
        return id;
    }

    public static AuditEntry Audit(Guid actor, string action = "test") => new(actor, "tester", action, "test", "1", "summary");

    public async Task<T?> ScalarAsync<T>(string sql, object? parameters = null)
    {
        await using var connection = await Database.OpenAsync();
        return await connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parameters));
    }

    public async ValueTask DisposeAsync()
    {
        await _source.DisposeAsync();
        await ExecuteAsync(_admin, $"DROP DATABASE IF EXISTS {_name} WITH (FORCE)");
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var source = new NpgsqlDataSourceBuilder(connectionString).Build();
        await using var connection = await source.OpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(sql));
    }

    [GeneratedRegex("Database=[^;]*", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseName();
}
