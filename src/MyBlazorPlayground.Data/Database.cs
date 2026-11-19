using Dapper;
using Npgsql;

namespace MyBlazorPlayground.Data;

public sealed class Database
{
    static Database()
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
    }

    private readonly NpgsqlDataSource _source;

    public Database(NpgsqlDataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    public NpgsqlConnection Open() => _source.OpenConnection();

    public ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken = default) =>
        _source.OpenConnectionAsync(cancellationToken);

    public async Task<T> InTransactionAsync<T>(Func<NpgsqlConnection, NpgsqlTransaction, Task<T>> work, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await work(connection, transaction);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public static bool IsUniqueViolation(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
