using System.Text.RegularExpressions;
using MyBlazorPlayground.Domain.Text;

namespace MyBlazorPlayground.Domain.Accounts;

public static partial class AccountRules
{
    public const int MinUserName = 3;
    public const int MaxUserName = 32;
    public const int MaxDisplayName = 80;
    public const int MinPassword = 12;
    public const int MaxPassword = 256;

    public static string NormalizeUserName(string? userName) => (userName ?? string.Empty).Trim().ToLowerInvariant();

    public static string? UserNameProblem(string? userName)
    {
        var normalized = NormalizeUserName(userName);
        if (normalized.Length is < MinUserName or > MaxUserName)
        {
            return $"User names are {MinUserName} to {MaxUserName} characters.";
        }

        return UserNamePattern().IsMatch(normalized)
            ? null
            : "Use lowercase letters, digits, dots, dashes or underscores, starting with a letter or digit.";
    }

    public static string NormalizeDisplayName(string? displayName, string userName)
    {
        var normalized = PlainText.Line(displayName, MaxDisplayName);
        return normalized.Length == 0 ? NormalizeUserName(userName) : normalized;
    }

    public static string? PasswordProblem(string? password, string? userName)
    {
        if (password is null || password.Length < MinPassword)
        {
            return $"Passwords need at least {MinPassword} characters.";
        }

        if (password.Length > MaxPassword)
        {
            return $"Passwords are at most {MaxPassword} characters.";
        }

        if (string.Equals(password.Trim(), NormalizeUserName(userName), StringComparison.OrdinalIgnoreCase))
        {
            return "The password cannot be the user name.";
        }

        return password.Distinct().Count() < 4 ? "Use a less repetitive password." : null;
    }

    [GeneratedRegex(@"^[a-z0-9][a-z0-9._-]*\z")]
    private static partial Regex UserNamePattern();
}
