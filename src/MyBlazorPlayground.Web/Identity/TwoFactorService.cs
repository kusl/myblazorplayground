using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Domain.Graphics;
using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Web.Identity;

internal enum TwoFactorState
{
    Off,
    Pending,
    On,
}

internal sealed record TwoFactorStatus(TwoFactorState State, string? Key = null, string? Uri = null, string? QrSvg = null, int RecoveryCodesLeft = 0, DateTime? Since = null)
{
    public static TwoFactorStatus Off { get; } = new(TwoFactorState.Off);
}

internal sealed record TwoFactorResult(string? Problem, IReadOnlyList<string>? RecoveryCodes = null)
{
    public bool Succeeded => Problem is null;
}

internal enum SecondFactor
{
    Failed,
    Code,
    RecoveryCode,
}

internal sealed class TwoFactorService(
    TwoFactorStore store,
    PersonStore people,
    PasswordHasher hasher,
    SignInThrottle throttle,
    IDataProtectionProvider protection,
    TimeProvider time,
    IOptions<SiteOptions> site)
{
    public const string Purpose = "playground.totp.v1";
    private const string WrongPassword = "The password is not right.";
    private const string TooMany = "Too many attempts. Wait a minute and try again.";

    private readonly IDataProtector _protector = protection.CreateProtector(Purpose);

    public async Task<TwoFactorStatus> StatusAsync(Guid personId, string userName, CancellationToken cancellationToken = default)
    {
        var record = await store.FindAsync(personId, cancellationToken);
        if (record is null)
        {
            return TwoFactorStatus.Off;
        }

        if (record.IsEnabled)
        {
            return new TwoFactorStatus(TwoFactorState.On, RecoveryCodesLeft: record.RecoveryCodesLeft, Since: record.EnabledAt);
        }

        if (Unprotect(record.Secret) is not { } secret)
        {
            await store.CancelAsync(personId, cancellationToken);
            return TwoFactorStatus.Off;
        }

        var uri = Totp.Uri(site.Value.Name, userName, secret);
        return new TwoFactorStatus(
            TwoFactorState.Pending,
            Base32.Group(Base32.Encode(secret)),
            uri,
            QrCode.Encode(uri)?.ToSvg("QR code for your authenticator app"));
    }

    public async Task BeginAsync(Guid personId, CancellationToken cancellationToken = default) =>
        await store.BeginAsync(personId, _protector.Protect(Base32.Encode(Totp.NewSecret())), cancellationToken);

    public Task CancelAsync(Guid personId, CancellationToken cancellationToken = default) => store.CancelAsync(personId, cancellationToken);

    public async Task<TwoFactorResult> ConfirmAsync(Actor actor, string? code, string client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!throttle.TryTake(actor.PersonId.ToString(), "totp-confirm"))
        {
            return new TwoFactorResult(TooMany);
        }

        var record = await store.FindAsync(actor.PersonId, cancellationToken);
        if (record is null || record.IsEnabled || Unprotect(record.Secret) is not { } secret)
        {
            return new TwoFactorResult("Start the setup again.");
        }

        if (Totp.Match(secret, code, time.GetUtcNow(), record.LastStep) is not { } step)
        {
            return new TwoFactorResult("That code is not right. Check the time on your phone and try the newest code.");
        }

        var codes = RecoveryCodes.Create();
        var enabled = await store.EnableAsync(
            actor.PersonId,
            step,
            [.. codes.Select(value => RecoveryCodes.Hash(value)!)],
            new AuditEntry(actor.PersonId, actor.UserName, "two-factor-enabled", "person", actor.PersonId.ToString(), "Turned on two-step sign in", client),
            cancellationToken);
        return enabled ? new TwoFactorResult(null, codes) : new TwoFactorResult("Start the setup again.");
    }

    public async Task<TwoFactorResult> DisableAsync(Actor actor, string? password, string client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (await CheckPasswordAsync(actor.PersonId, password, client, cancellationToken) is { } problem)
        {
            return new TwoFactorResult(problem);
        }

        var disabled = await store.DisableAsync(
            actor.PersonId,
            new AuditEntry(actor.PersonId, actor.UserName, "two-factor-disabled", "person", actor.PersonId.ToString(), "Turned off two-step sign in", client),
            cancellationToken);
        return disabled ? new TwoFactorResult(null) : new TwoFactorResult("Two-step sign in is already off.");
    }

    public async Task<TwoFactorResult> RenewRecoveryCodesAsync(Actor actor, string? password, string client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (await CheckPasswordAsync(actor.PersonId, password, client, cancellationToken) is { } problem)
        {
            return new TwoFactorResult(problem);
        }

        var codes = RecoveryCodes.Create();
        var replaced = await store.ReplaceRecoveryCodesAsync(
            actor.PersonId,
            [.. codes.Select(value => RecoveryCodes.Hash(value)!)],
            new AuditEntry(actor.PersonId, actor.UserName, "recovery-codes-renewed", "person", actor.PersonId.ToString(), "Made new recovery codes", client),
            cancellationToken);
        return replaced ? new TwoFactorResult(null, codes) : new TwoFactorResult("Two-step sign in is off.");
    }

    public async Task<bool> ResetAsync(Actor administrator, PersonRecord person, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(administrator);
        ArgumentNullException.ThrowIfNull(person);
        return await store.DisableAsync(
            person.Id,
            new AuditEntry(administrator.PersonId, administrator.UserName, "two-factor-reset", "person", person.Id.ToString(), $"Turned off two-step sign in for {person.UserName}"),
            cancellationToken);
    }

    public async Task<SecondFactor> VerifyAsync(Guid personId, string? input, CancellationToken cancellationToken = default)
    {
        var record = await store.FindAsync(personId, cancellationToken);
        if (record is not { IsEnabled: true })
        {
            return SecondFactor.Failed;
        }

        if (Totp.Normalize(input) is not null)
        {
            return Unprotect(record.Secret) is { } secret
                && Totp.Match(secret, input, time.GetUtcNow(), record.LastStep) is { } step
                && await store.TryUseStepAsync(personId, step, cancellationToken)
                ? SecondFactor.Code
                : SecondFactor.Failed;
        }

        return RecoveryCodes.Hash(input) is { } hash && await store.TryUseRecoveryCodeAsync(personId, hash, cancellationToken)
            ? SecondFactor.RecoveryCode
            : SecondFactor.Failed;
    }

    private async Task<string?> CheckPasswordAsync(Guid personId, string? password, string client, CancellationToken cancellationToken)
    {
        if (!throttle.TryTake(client, "two-factor") || !throttle.TryTake(personId.ToString(), "two-factor-person"))
        {
            return TooMany;
        }

        var person = await people.FindAsync(personId, cancellationToken);
        return person is null || hasher.Verify(person.PasswordHash, password) == PasswordCheck.Failed ? WrongPassword : null;
    }

    private byte[]? Unprotect(string stored)
    {
        try
        {
            return Base32.Decode(_protector.Unprotect(stored));
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
