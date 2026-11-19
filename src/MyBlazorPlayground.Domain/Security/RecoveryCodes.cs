using System.Security.Cryptography;
using System.Text;

namespace MyBlazorPlayground.Domain.Security;

public static class RecoveryCodes
{
    public const int Count = 10;
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
    private const int GroupLength = 5;

    public static IReadOnlyList<string> Create(int count = Count)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        while (codes.Count < count)
        {
            var builder = new StringBuilder(GroupLength * 2 + 1);
            for (var i = 0; i < GroupLength * 2; i++)
            {
                if (i == GroupLength)
                {
                    builder.Append('-');
                }

                builder.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
            }

            codes.Add(builder.ToString());
        }

        return [.. codes];
    }

    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var compact = new string([.. input.Where(character => !char.IsWhiteSpace(character) && character != '-').Select(char.ToLowerInvariant)]);
        return compact.Length == GroupLength * 2 && compact.All(character => Alphabet.Contains(character, StringComparison.Ordinal))
            ? compact
            : null;
    }

    public static string? Hash(string? input) =>
        Normalize(input) is { } code ? Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(code))) : null;
}
