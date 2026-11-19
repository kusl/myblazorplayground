using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MyBlazorPlayground.Data;

namespace MyBlazorPlayground.Web.Persistence;

internal sealed class DatabaseHealthCheck(Database database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await database.OpenAsync(cancellationToken);
            await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is Npgsql.NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("The database is not reachable.");
        }
    }
}
