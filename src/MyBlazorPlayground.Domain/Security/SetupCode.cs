using System.Security.Cryptography;
using System.Text;

namespace MyBlazorPlayground.Domain.Security;

public sealed class SetupCode
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private readonly byte[] _digest;

    private SetupCode(string value)
    {
        Value = value;
        _digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    }

    public string Value { get; }

    public static SetupCode Create(int groups = 4, int groupLength = 4)
    {
        var builder = new StringBuilder();
        for (var g = 0; g < groups; g++)
        {
            if (g > 0)
            {
                builder.Append('-');
            }

            for (var k = 0; k < groupLength; k++)
            {
                builder.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
            }
        }

        return new SetupCode(builder.ToString());
    }

    public bool Matches(string? candidate)
    {
        var normalized = new string((candidate ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return CryptographicOperations.FixedTimeEquals(digest, _digest);
    }
}
