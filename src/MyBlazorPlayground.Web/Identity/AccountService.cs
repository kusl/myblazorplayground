using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Data.Settings;
using MyBlazorPlayground.Domain.Accounts;
using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Web.Identity;

internal sealed record AccountResult(string? Problem, PersonRecord? Person = null, bool NeedsSecondFactor = false)
{
    public bool Succeeded => Problem is null && Person is not null;
}

internal sealed class AccountService(
    PersonStore people,
    SiteSettings settings,
    AuditLog audit,
    PasswordHasher hasher,
    SignInThrottle throttle,
    SetupGate setup,
    TwoFactorService twoFactor)
{
    private const string Invalid = "That user name and password don't match an account.";
    private const string Expired = "That sign in took too long. Start again.";

    public Task<bool> IsRegistrationOpenAsync(CancellationToken cancellationToken = default) =>
        settings.GetFlagAsync(SiteSettings.RegistrationOpen, true, cancellationToken);

    public async Task<AccountResult> CreateFirstAdministratorAsync(string? code, string? userName, string? displayName, string? password, string client, CancellationToken cancellationToken = default)
    {
        if (!throttle.TryTake(client, "setup"))
        {
            return new AccountResult("Too many attempts. Wait a minute and try again.");
        }

        if (setup.IsComplete)
        {
            return new AccountResult("Setup is already done.");
        }

        if (!setup.Accepts(code))
        {
            return new AccountResult("That setup code is not right. It is printed in the server log.");
        }

        var result = await CreateAsync(userName, displayName, password, true, cancellationToken);
        if (result.Succeeded)
        {
            setup.MarkComplete();
            await audit.WriteAsync(new AuditEntry(result.Person!.Id, result.Person.UserName, "setup", "person", result.Person.Id.ToString(), "Created the first administrator", client), cancellationToken);
        }
        else if (result.Problem == "Setup is already done.")
        {
            setup.MarkComplete();
        }

        return result;
    }

    public async Task<AccountResult> RegisterAsync(string? userName, string? displayName, string? password, string client, CancellationToken cancellationToken = default)
    {
        if (!setup.IsComplete)
        {
            return new AccountResult("This site is not set up yet.");
        }

        if (!await IsRegistrationOpenAsync(cancellationToken))
        {
            return new AccountResult("Registration is closed. Ask an administrator.");
        }

        if (!throttle.TryTake(client, "register"))
        {
            return new AccountResult("Too many attempts. Wait a minute and try again.");
        }

        var result = await CreateAsync(userName, displayName, password, false, cancellationToken);
        if (result.Succeeded)
        {
            await audit.WriteAsync(new AuditEntry(result.Person!.Id, result.Person.UserName, "register", "person", result.Person.Id.ToString(), "Registered", client), cancellationToken);
        }

        return result;
    }

    public async Task<AccountResult> SignInAsync(string? userName, string? password, string client, CancellationToken cancellationToken = default)
    {
        var name = AccountRules.NormalizeUserName(userName);
        if (!throttle.TryTake(client, "sign-in") || !throttle.TryTake(name, "sign-in-user"))
        {
            return new AccountResult("Too many attempts. Wait a minute and try again.");
        }

        var person = name.Length == 0 ? null : await people.FindByUserNameAsync(name, cancellationToken);
        var check = hasher.Verify(person?.PasswordHash ?? DummyHash.Value, password);
        if (person is null || check == PasswordCheck.Failed)
        {
            if (person is not null)
            {
                await audit.WriteAsync(new AuditEntry(person.Id, person.UserName, "sign-in-failed", "person", person.Id.ToString(), "Wrong password", client), cancellationToken);
            }

            return new AccountResult(Invalid);
        }

        if (person.DisabledAt is not null)
        {
            return new AccountResult("This account is disabled.");
        }

        var rehash = check == PasswordCheck.SucceededNeedsRehash ? hasher.Hash(password!) : null;
        if (person.HasTwoFactor)
        {
            if (rehash is not null)
            {
                await people.UpdatePasswordHashAsync(person.Id, rehash, cancellationToken);
            }

            return new AccountResult(null, person, NeedsSecondFactor: true);
        }

        await people.RecordSignInAsync(person.Id, rehash, cancellationToken);
        return new AccountResult(null, person);
    }

    public async Task<AccountResult> CompleteSecondFactorAsync(Guid personId, string? stamp, string? code, string client, CancellationToken cancellationToken = default)
    {
        if (!throttle.TryTake(client, "second-factor") || !throttle.TryTake(personId.ToString(), "second-factor-person"))
        {
            return new AccountResult("Too many attempts. Wait a minute and try again.");
        }

        var person = await people.FindAsync(personId, cancellationToken);
        if (person is null
            || person.DisabledAt is not null
            || !person.HasTwoFactor
            || !string.Equals(person.SecurityStamp.ToString("D", System.Globalization.CultureInfo.InvariantCulture), stamp, StringComparison.Ordinal))
        {
            return new AccountResult(Expired);
        }

        var outcome = await twoFactor.VerifyAsync(person.Id, code, cancellationToken);
        if (outcome == SecondFactor.Failed)
        {
            await audit.WriteAsync(new AuditEntry(person.Id, person.UserName, "sign-in-failed", "person", person.Id.ToString(), "Wrong two-step code", client), cancellationToken);
            return new AccountResult("That code is not right. Try the newest code from your app, or a recovery code.");
        }

        if (outcome == SecondFactor.RecoveryCode)
        {
            await audit.WriteAsync(new AuditEntry(person.Id, person.UserName, "recovery-code-used", "person", person.Id.ToString(), "Signed in with a recovery code", client), cancellationToken);
        }

        await people.RecordSignInAsync(person.Id, null, cancellationToken);
        return new AccountResult(null, person);
    }

    public async Task<AccountResult> ChangePasswordAsync(Guid personId, string? current, string? next, string client, CancellationToken cancellationToken = default)
    {
        if (!throttle.TryTake(client, "password"))
        {
            return new AccountResult("Too many attempts. Wait a minute and try again.");
        }

        var person = await people.FindAsync(personId, cancellationToken);
        if (person is null || hasher.Verify(person.PasswordHash, current) == PasswordCheck.Failed)
        {
            return new AccountResult("The current password is not right.");
        }

        if (AccountRules.PasswordProblem(next, person.UserName) is { } problem)
        {
            return new AccountResult(problem);
        }

        await people.UpdatePasswordAsync(person.Id, hasher.Hash(next!), cancellationToken);
        await audit.WriteAsync(new AuditEntry(person.Id, person.UserName, "password-changed", "person", person.Id.ToString(), "Changed password", client), cancellationToken);
        return new AccountResult(null, await people.FindAsync(person.Id, cancellationToken));
    }

    public async Task<AccountResult> RenameAsync(Guid personId, string? displayName, CancellationToken cancellationToken = default)
    {
        var person = await people.FindAsync(personId, cancellationToken);
        if (person is null)
        {
            return new AccountResult("That account no longer exists.");
        }

        await people.UpdateDisplayNameAsync(person.Id, AccountRules.NormalizeDisplayName(displayName, person.UserName), cancellationToken);
        return new AccountResult(null, await people.FindAsync(person.Id, cancellationToken));
    }

    private async Task<AccountResult> CreateAsync(string? userName, string? displayName, string? password, bool firstAdministrator, CancellationToken cancellationToken)
    {
        if (AccountRules.UserNameProblem(userName) is { } nameProblem)
        {
            return new AccountResult(nameProblem);
        }

        var name = AccountRules.NormalizeUserName(userName);
        if (AccountRules.PasswordProblem(password, name) is { } passwordProblem)
        {
            return new AccountResult(passwordProblem);
        }

        var (outcome, id) = await people.CreateAsync(
            new NewPerson(name, AccountRules.NormalizeDisplayName(displayName, name), hasher.Hash(password!)),
            firstAdministrator,
            cancellationToken);
        return outcome switch
        {
            CreatePersonResult.Created => new AccountResult(null, await people.FindAsync(id, cancellationToken)),
            CreatePersonResult.UserNameTaken => new AccountResult("That user name is taken."),
            _ => new AccountResult("Setup is already done."),
        };
    }

    private static class DummyHash
    {
        public static readonly string Value = new PasswordHasher().Hash(Guid.NewGuid().ToString("N"));
    }
}
