using Dapper;
using MyBlazorPlayground.Domain.Access;

namespace MyBlazorPlayground.Data.People;

public sealed class PersonRecord
{
    public Guid Id { get; init; }

    public string UserName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public Guid SecurityStamp { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? LastSignInAt { get; init; }

    public DateTime? DisabledAt { get; init; }

    public bool IsAdministrator { get; init; }

    public bool HasTwoFactor { get; init; }
}

public sealed record NewPerson(string UserName, string DisplayName, string PasswordHash);

public enum CreatePersonResult
{
    Created,
    UserNameTaken,
    SetupAlreadyDone,
}

public sealed class PersonStore(Database database)
{
    private const string Select =
        """
        SELECT p.id, p.user_name, p.display_name, p.password_hash, p.security_stamp, p.created_at,
               p.last_sign_in_at, p.disabled_at,
               EXISTS (SELECT 1 FROM site_role r WHERE r.person_id = p.id AND r.role = 'administrator') AS is_administrator,
               EXISTS (SELECT 1 FROM person_totp t WHERE t.person_id = p.id AND t.enabled_at IS NOT NULL) AS has_two_factor
        FROM person p
        """;

    public async Task<bool> HasAdministratorAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM site_role r JOIN person p ON p.id = r.person_id WHERE r.role = 'administrator' AND p.disabled_at IS NULL)",
            cancellationToken: cancellationToken));
    }

    public async Task<(CreatePersonResult Result, Guid Id)> CreateAsync(NewPerson person, bool firstAdministrator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(person);
        var id = Guid.CreateVersion7();
        try
        {
            return await database.InTransactionAsync(async (connection, transaction) =>
            {
                if (firstAdministrator)
                {
                    await connection.ExecuteAsync(new CommandDefinition("LOCK TABLE site_role IN EXCLUSIVE MODE", transaction: transaction, cancellationToken: cancellationToken));
                    var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                        "SELECT EXISTS (SELECT 1 FROM site_role WHERE role = 'administrator')", transaction: transaction, cancellationToken: cancellationToken));
                    if (exists)
                    {
                        return (CreatePersonResult.SetupAlreadyDone, Guid.Empty);
                    }
                }

                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO person (id, user_name, display_name, password_hash, security_stamp)
                    VALUES (@id, @UserName, @DisplayName, @PasswordHash, @stamp)
                    """,
                    new { id, person.UserName, person.DisplayName, person.PasswordHash, stamp = Guid.NewGuid() },
                    transaction,
                    cancellationToken: cancellationToken));
                if (firstAdministrator)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "INSERT INTO site_role (person_id, role, granted_by) VALUES (@id, @role, @id)",
                        new { id, role = SiteRoles.Administrator },
                        transaction,
                        cancellationToken: cancellationToken));
                }

                return (CreatePersonResult.Created, id);
            }, cancellationToken);
        }
        catch (Exception exception) when (Database.IsUniqueViolation(exception))
        {
            return (CreatePersonResult.UserNameTaken, Guid.Empty);
        }
    }

    public async Task<PersonRecord?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PersonRecord>(new CommandDefinition(
            Select + " WHERE p.user_name = @userName", new { userName }, cancellationToken: cancellationToken));
    }

    public async Task<PersonRecord?> FindAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<PersonRecord>(new CommandDefinition(
            Select + " WHERE p.id = @id", new { id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PersonRecord>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<PersonRecord>(new CommandDefinition(
            Select + " ORDER BY p.user_name", cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PersonRecord>> SearchAsync(string text, int limit, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        return (await connection.QueryAsync<PersonRecord>(new CommandDefinition(
            Select + " WHERE p.disabled_at IS NULL AND (p.user_name LIKE @pattern OR lower(p.display_name) LIKE @pattern) ORDER BY p.user_name LIMIT @limit",
            new { pattern = "%" + Like(text.ToLowerInvariant()) + "%", limit },
            cancellationToken: cancellationToken))).AsList();
    }

    public async Task RecordSignInAsync(Guid id, string? rehashedPassword, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person SET last_sign_in_at = now(), password_hash = coalesce(@rehashedPassword, password_hash) WHERE id = @id",
            new { id, rehashedPassword },
            cancellationToken: cancellationToken));
    }

    public async Task UpdatePasswordHashAsync(Guid id, string passwordHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person SET password_hash = @passwordHash WHERE id = @id",
            new { id, passwordHash },
            cancellationToken: cancellationToken));
    }

    public async Task<bool> SetAdministratorAsync(Guid id, bool administrator, Guid grantedBy, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var changed = administrator
            ? await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO site_role (person_id, role, granted_by) VALUES (@id, @role, @grantedBy) ON CONFLICT DO NOTHING",
                new { id, role = SiteRoles.Administrator, grantedBy },
                cancellationToken: cancellationToken))
            : await connection.ExecuteAsync(new CommandDefinition(
                """
                DELETE FROM site_role WHERE person_id = @id AND role = @role
                AND (SELECT count(*) FROM site_role r JOIN person p ON p.id = r.person_id WHERE r.role = @role AND p.disabled_at IS NULL) > 1
                """,
                new { id, role = SiteRoles.Administrator },
                cancellationToken: cancellationToken));
        return changed > 0;
    }

    public async Task<bool> SetDisabledAsync(Guid id, bool disabled, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        var changed = disabled
            ? await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE person SET disabled_at = now(), security_stamp = gen_random_uuid()
                WHERE id = @id AND disabled_at IS NULL
                AND NOT (EXISTS (SELECT 1 FROM site_role WHERE person_id = @id AND role = 'administrator')
                    AND (SELECT count(*) FROM site_role r JOIN person p ON p.id = r.person_id WHERE r.role = 'administrator' AND p.disabled_at IS NULL) <= 1)
                """,
                new { id },
                cancellationToken: cancellationToken))
            : await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE person SET disabled_at = NULL WHERE id = @id AND disabled_at IS NOT NULL",
                new { id },
                cancellationToken: cancellationToken));
        return changed > 0;
    }

    public async Task UpdatePasswordAsync(Guid id, string passwordHash, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person SET password_hash = @passwordHash, security_stamp = gen_random_uuid() WHERE id = @id",
            new { id, passwordHash },
            cancellationToken: cancellationToken));
    }

    public async Task UpdateDisplayNameAsync(Guid id, string displayName, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE person SET display_name = @displayName WHERE id = @id",
            new { id, displayName },
            cancellationToken: cancellationToken));
    }

    internal static string Like(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
