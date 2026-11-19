using System.Globalization;
using System.Security.Cryptography;

namespace MyBlazorPlayground.Domain.Security;

public static class Totp
{
    public const int Digits = 6;
    public const int PeriodSeconds = 30;
    public const int SecretBytes = 20;
    public const int Window = 1;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(SecretBytes);

    public static long StepAt(DateTimeOffset time) => time.ToUnixTimeSeconds() / PeriodSeconds;

    public static string Code(ReadOnlySpan<byte> secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counter[i] = (byte)(step & 0xFF);
            step >>= 8;
        }

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(secret, counter, hash);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public static string? Normalize(string? input)
    {
        if (input is null)
        {
            return null;
        }

        var digits = new string([.. input.Where(character => !char.IsWhiteSpace(character) && character != '-')]);
        return digits.Length == Digits && digits.All(char.IsAsciiDigit) ? digits : null;
    }

    public static long? Match(ReadOnlySpan<byte> secret, string? input, DateTimeOffset now, long lastUsedStep)
    {
        if (Normalize(input) is not { } code)
        {
            return null;
        }

        var current = StepAt(now);
        long? matched = null;
        for (var step = current - Window; step <= current + Window; step++)
        {
            var candidate = System.Text.Encoding.ASCII.GetBytes(Code(secret, step));
            var expected = System.Text.Encoding.ASCII.GetBytes(code);
            if (CryptographicOperations.FixedTimeEquals(candidate, expected) && step > lastUsedStep)
            {
                matched ??= step;
            }
        }

        return matched;
    }

    public static string Uri(string issuer, string account, ReadOnlySpan<byte> secret)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(account);
        var label = System.Uri.EscapeDataString(issuer) + ":" + System.Uri.EscapeDataString(account);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={System.Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits={Digits}&period={PeriodSeconds}");
    }
}
