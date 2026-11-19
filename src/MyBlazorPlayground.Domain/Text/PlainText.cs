using System.Globalization;
using System.Text;

namespace MyBlazorPlayground.Domain.Text;

public static class PlainText
{
    public static string Line(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(rune.ToString());
        }

        return Cut(builder.ToString(), maxLength);
    }

    public static string Block(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var rune in value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').EnumerateRunes())
        {
            if (rune.Value is '\n' or '\t')
            {
                builder.Append((char)rune.Value);
            }
            else if (!Rune.IsControl(rune))
            {
                builder.Append(rune.ToString());
            }
        }

        var lines = builder.ToString().Split('\n').Select(line => line.TrimEnd());
        var joined = string.Join('\n', lines).Trim('\n');
        while (joined.Contains("\n\n\n\n", StringComparison.Ordinal))
        {
            joined = joined.Replace("\n\n\n\n", "\n\n\n", StringComparison.Ordinal);
        }

        return Cut(joined, maxLength);
    }

    public static string Cut(string value, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length <= maxLength)
        {
            return value;
        }

        var cut = 0;
        var enumerator = StringInfo.GetTextElementEnumerator(value);
        while (enumerator.MoveNext())
        {
            var end = enumerator.ElementIndex + enumerator.GetTextElement().Length;
            if (end > maxLength)
            {
                break;
            }

            cut = end;
        }

        return value[..cut].TrimEnd();
    }
}
