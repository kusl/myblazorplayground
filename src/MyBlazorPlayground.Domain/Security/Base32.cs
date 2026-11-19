using System.Text;

namespace MyBlazorPlayground.Domain.Security;

public static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                builder.Append(Alphabet[(buffer >> bits) & 31]);
            }
        }

        if (bits > 0)
        {
            builder.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        }

        return builder.ToString();
    }

    public static byte[]? Decode(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var output = new List<byte>(text.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var character in text)
        {
            if (character is ' ' or '-' or '=')
            {
                continue;
            }

            var index = Alphabet.IndexOf(char.ToUpperInvariant(character), StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. output];
    }

    public static string Group(string text, int size = 4)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Join(' ', text.Chunk(size).Select(chunk => new string(chunk)));
    }
}
