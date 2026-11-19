using Dapper;
using MyBlazorPlayground.Data.Audit;
using Npgsql;

namespace MyBlazorPlayground.Data.People;

public sealed class TwoFactorRecord
{
    public Guid PersonId { get; init; }

    public string Secret { get; init; } = string.Empty;

    public long LastStep { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? EnabledAt { get; init; }

    public int RecoveryCodesLeft { get; init; }

    public bool IsEnabled => EnabledAt is not null;
}

public sealed class TwoFactorStore(Database database)
{
    public async Task<TwoFactorRecord?> FindAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<TwoFactorRecord>(new CommandDefinition(
            """
            SELECT t.person_id, t.secret, t.last_step, t.created_at, t.enabled_at,
                   (SELECT count(*)::int FROM person_recovery_code c WHERE c.person_id = t.person_id AND c.used_at IS NULL) AS recovery_codes_left
            FROM person_totp t
            WHERE t.person_id = @personId
            """,
            new { personId },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> BeginAsync(Guid personId, string secret, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO person_totp (person_id, secret) VALUES (@personId, @secret)
            ON CONFLICT (person_id) DO UPDATE SET secret = excluded.secret, last_step = 0, created_at = now()
            WHERE person_totp.enabled_at IS NULL
            """,
            new { personId, secret },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> CancelAsync(Guid personId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM person_totp WHERE person_id = @personId AND enabled_at IS NULL",
            new { personId },
            cancellationToken: cancellationToken)) > 0;
    }

    public Task<bool> EnableAsync(Guid personId, long step, IReadOnlyCollection<string> recoveryHashes, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveryHashes);
        ArgumentNullException.ThrowIfNull(audit);
        return database.InTransactionAsync(async (connection, transaction) =>
        {
            var enabled = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE person_totp SET enabled_at = now(), last_step = @step WHERE person_id = @personId AND enabled_at IS NULL",
                new { personId, step },
                transaction,
                cancellationToken: cancellationToken));
            if (enabled == 0)
            {
                return false;
            }

            await ReplaceCodesAsync(connection, transaction, personId, recoveryHashes, cancellationToken);
            await RotateStampAsync(connection, transaction, personId, cancellationToken);
            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public Task<bool> DisableAsync(Guid personId, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        return database.InTransactionAsync(async (connection, transaction) =>
        {
            var removed = await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM person_totp WHERE person_id = @personId AND enabled_at IS NOT NULL",
                new { personId },
                transaction,
                cancellationToken: cancellationToken));
            if (removed == 0)
            {
                return false;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM person_recovery_code WHERE person_id = @personId",
                new { personId },
                transaction,
                cancellationToken: cancellationToken));
            await RotateStampAsync(connection, transaction, personId, cancellationToken);
            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public Task<bool> ReplaceRecoveryCodesAsync(Guid personId, IReadOnlyCollection<string> recoveryHashes, AuditEntry audit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recoveryHashes);
        ArgumentNullException.ThrowIfNull(audit);
        return database.InTransactionAsync(async (connection, transaction) =>
        {
            var enabled = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM person_totp WHERE person_id = @personId AND enabled_at IS NOT NULL)",
                new { personId },
                transaction,
                cancellationToken: cancellationToken));
            if (!enabled)
            {
                return false;
            }

            await ReplaceCodesAsync(connection, transaction, personId, recoveryHashes, cancellationToken);
            await AuditLog.WriteAsync(connection, transaction, audit, cancellationToken);
            return true;
        }, cancellationToken);
    }

    public async Task<bool> TryUseStepAsync(Guid personId, long step, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person_totp SET last_step = @step WHERE person_id = @personId AND enabled_at IS NOT NULL AND last_step < @step",
            new { personId, step },
            cancellationToken: cancellationToken)) > 0;
    }

    public async Task<bool> TryUseRecoveryCodeAsync(Guid personId, string codeHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE person_recovery_code SET used_at = now()
            WHERE person_id = @personId AND code_hash = @codeHash AND used_at IS NULL
            AND EXISTS (SELECT 1 FROM person_totp t WHERE t.person_id = @personId AND t.enabled_at IS NOT NULL)
            """,
            new { personId, codeHash },
            cancellationToken: cancellationToken)) > 0;
    }

    private static async Task ReplaceCodesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid personId, IReadOnlyCollection<string> recoveryHashes, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM person_recovery_code WHERE person_id = @personId",
            new { personId },
            transaction,
            cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO person_recovery_code (person_id, code_hash) SELECT @personId, unnest(@hashes)",
            new { personId, hashes = recoveryHashes.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task RotateStampAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid personId, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person SET security_stamp = gen_random_uuid() WHERE id = @personId",
            new { personId },
            transaction,
            cancellationToken: cancellationToken));
}
