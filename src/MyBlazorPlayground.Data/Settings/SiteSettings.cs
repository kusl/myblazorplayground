using Dapper;

namespace MyBlazorPlayground.Data.Settings;

public sealed class SiteSettings(Database database)
{
    public const string RegistrationOpen = "registration_open";

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT value FROM site_setting WHERE key = @key", new { key }, cancellationToken: cancellationToken));
    }

    public async Task<bool> GetFlagAsync(string key, bool fallback, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        return value is null ? fallback : string.Equals(value, "true", StringComparison.Ordinal);
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO site_setting (key, value) VALUES (@key, @value)
            ON CONFLICT (key) DO UPDATE SET value = excluded.value, updated_at = now()
            """,
            new { key, value },
            cancellationToken: cancellationToken));
    }
}
