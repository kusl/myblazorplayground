using System.Text;

namespace MyBlazorPlayground.Domain.Files;

public static class FileRules
{
    public const int MaxFileName = 180;

    public static string SafeFileName(string? name)
    {
        var raw = Path.GetFileName((name ?? string.Empty).Replace('\\', '/'));
        var builder = new StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsControl(c) || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|' or ';')
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        var cleaned = builder.ToString().Trim().Trim('.');
        if (cleaned.Length == 0)
        {
            cleaned = "file";
        }

        if (cleaned.Length <= MaxFileName)
        {
            return cleaned;
        }

        var extension = Path.GetExtension(cleaned);
        if (extension.Length > 16)
        {
            extension = string.Empty;
        }

        return cleaned[..(MaxFileName - extension.Length)] + extension;
    }

    public static string? SniffImage(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 8 && head[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }

        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return "image/jpeg";
        }

        if (head.Length >= 6 && (head[..6].SequenceEqual("GIF87a"u8) || head[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }

        if (head.Length >= 12 && head[..4].SequenceEqual("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }

    public static string Size(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.##} GB",
    };
}
