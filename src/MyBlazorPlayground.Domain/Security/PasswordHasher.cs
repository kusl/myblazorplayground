using System.Globalization;
using System.Security.Cryptography;

namespace MyBlazorPlayground.Domain.Security;

public enum PasswordCheck
{
    Failed,
    Succeeded,
    SucceededNeedsRehash,
}

public sealed class PasswordHasher(int iterations = PasswordHasher.DefaultIterations)
{
    public const int DefaultIterations = 210_000;
    public const int MinimumIterations = 100_000;
    private const string Algorithm = "pbkdf2-sha512";
    private const int SaltBytes = 16;
    private const int HashBytes = 64;

    public int Iterations { get; } = iterations >= MinimumIterations
        ? iterations
        : throw new ArgumentOutOfRangeException(nameof(iterations), iterations, "Too few iterations.");

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA512, HashBytes);
        return string.Create(CultureInfo.InvariantCulture, $"{Algorithm}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}");
    }

    public PasswordCheck Verify(string? stored, string? password)
    {
        if (string.IsNullOrEmpty(stored) || password is null)
        {
            return PasswordCheck.Failed;
        }

        var parts = stored.Split('$');
        if (parts.Length != 4
            || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var rounds)
            || rounds < MinimumIterations)
        {
            return PasswordCheck.Failed;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }

        if (expected.Length != HashBytes)
        {
            return PasswordCheck.Failed;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, rounds, HashAlgorithmName.SHA512, HashBytes);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            return PasswordCheck.Failed;
        }

        return rounds < Iterations ? PasswordCheck.SucceededNeedsRehash : PasswordCheck.Succeeded;
    }
}
