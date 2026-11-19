using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace MyBlazorPlayground.Data;

public sealed partial class MigrationRunner(Database database, ILogger<MigrationRunner> logger)
{
    private const long LockKey = 0x6D79_626C_617A_6F72;

    public static IReadOnlyList<Migration> Embedded()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        var migrations = new List<Migration>();
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var match = ResourcePattern().Match(name);
            if (!match.Success)
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException($"Missing resource {name}.");
            using var reader = new StreamReader(stream);
            migrations.Add(new Migration(int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), match.Groups[2].Value, reader.ReadToEnd()));
        }

        var ordered = migrations.OrderBy(migration => migration.Version).ToList();
        if (ordered.Select(migration => migration.Version).Distinct().Count() != ordered.Count)
        {
            throw new InvalidOperationException("Two migrations share a version number.");
        }

        return ordered;
    }

    public async Task RunAsync(int attempts, TimeSpan delay, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ApplyAsync(cancellationToken);
                return;
            }
            catch (NpgsqlException exception) when (attempt < attempts && IsTransient(exception))
            {
                LogWaiting(logger, attempt, attempts, exception.Message);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(NpgsqlException exception) =>
        exception is not PostgresException postgres
        || postgres.SqlState is PostgresErrorCodes.CannotConnectNow or PostgresErrorCodes.AdminShutdown;

    private async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_lock(@key)", new { key = LockKey }, cancellationToken: cancellationToken));
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE IF NOT EXISTS schema_migration (
                    version integer PRIMARY KEY,
                    name text NOT NULL,
                    applied_at timestamptz NOT NULL DEFAULT now()
                )
                """,
                cancellationToken: cancellationToken));
            var applied = (await connection.QueryAsync<int>(new CommandDefinition("SELECT version FROM schema_migration", cancellationToken: cancellationToken))).ToHashSet();
            foreach (var migration in Embedded().Where(migration => !applied.Contains(migration.Version)))
            {
                await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(migration.Sql, transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO schema_migration (version, name) VALUES (@Version, @Name)",
                    new { migration.Version, migration.Name },
                    transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);
                LogApplied(logger, migration.Version, migration.Name);
            }
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_unlock(@key)", new { key = LockKey }, cancellationToken: CancellationToken.None));
        }
    }

    [GeneratedRegex(@"^Migrations/(\d{4})_([a-z0-9_]+)\.sql$")]
    private static partial Regex ResourcePattern();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Database not reachable yet (attempt {Attempt} of {Attempts}): {Reason}")]
    private static partial void LogWaiting(ILogger logger, int attempt, int attempts, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied migration {Version} {Name}")]
    private static partial void LogApplied(ILogger logger, int version, string name);
}

public sealed record Migration(int Version, string Name, string Sql);
