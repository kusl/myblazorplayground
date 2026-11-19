using System.Text;
using System.Text.RegularExpressions;

namespace MyBlazorPlayground.Domain.Text;

public static partial class RichText
{
    public const int MaxLength = 100_000;
    private const int MaxQuoteDepth = 4;

    public static string ToHtml(string? markdown, Func<int, string?>? itemLink = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var text = PlainText.Block(markdown, MaxLength);
        var builder = new StringBuilder(text.Length * 2);
        RenderBlocks(text.Split('\n'), builder, itemLink, 0);
        return builder.ToString();
    }

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            AppendEscaped(builder, c);
        }

        return builder.ToString();
    }

    public static bool IsSafeLink(string url) =>
        IsLocalPath(url)
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeMailto));

    public static bool IsLocalPath(string url) =>
        url.Length > 1
        && url[0] == '/'
        && url[1] != '/'
        && !url.Contains('\\', StringComparison.Ordinal)
        && !url.Any(char.IsWhiteSpace)
        && !url.Any(char.IsControl);

    private static void RenderBlocks(IReadOnlyList<string> lines, StringBuilder html, Func<int, string?>? itemLink, int depth)
    {
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Trim().Length == 0)
            {
                i++;
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                i = RenderFence(lines, i, html);
                continue;
            }

            var heading = HeadingPattern().Match(line);
            if (heading.Success)
            {
                var level = Math.Min(heading.Groups[1].Length + 2, 6);
                html.Append("<h").Append(level).Append('>');
                RenderInline(heading.Groups[2].Value.Trim().TrimEnd('#').TrimEnd(), html, itemLink, true);
                html.Append("</h").Append(level).Append(">\n");
                i++;
                continue;
            }

            if (RulePattern().IsMatch(line))
            {
                html.Append("<hr>\n");
                i++;
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                var quoted = new List<string>();
                while (i < lines.Count && lines[i].TrimStart().StartsWith('>'))
                {
                    var content = lines[i].TrimStart()[1..];
                    quoted.Add(content.StartsWith(' ') ? content[1..] : content);
                    i++;
                }

                html.Append("<blockquote>\n");
                if (depth < MaxQuoteDepth)
                {
                    RenderBlocks(quoted, html, itemLink, depth + 1);
                }
                else
                {
                    RenderParagraph(quoted, html, itemLink);
                }

                html.Append("</blockquote>\n");
                continue;
            }

            if (BulletPattern().IsMatch(line) || OrderedPattern().IsMatch(line))
            {
                i = RenderList(lines, i, html, itemLink);
                continue;
            }

            var paragraph = new List<string>();
            while (i < lines.Count && lines[i].Trim().Length > 0 && !StartsBlock(lines[i]))
            {
                paragraph.Add(lines[i]);
                i++;
            }

            if (paragraph.Count == 0)
            {
                paragraph.Add(lines[i]);
                i++;
            }

            RenderParagraph(paragraph, html, itemLink);
        }
    }

    private static bool StartsBlock(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("```", StringComparison.Ordinal)
            || trimmed.StartsWith('>')
            || HeadingPattern().IsMatch(line)
            || RulePattern().IsMatch(line)
            || BulletPattern().IsMatch(line)
            || OrderedPattern().IsMatch(line);
    }

    private static int RenderFence(IReadOnlyList<string> lines, int start, StringBuilder html)
    {
        var opening = lines[start].TrimStart();
        var language = new string(opening[3..].Trim().TakeWhile(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '+').Take(24).ToArray()).ToLowerInvariant();
        var i = start + 1;
        var code = new List<string>();
        while (i < lines.Count && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
        {
            code.Add(lines[i]);
            i++;
        }

        html.Append("<pre><code");
        if (language.Length > 0)
        {
            html.Append(" class=\"language-").Append(language).Append('"');
        }

        html.Append('>').Append(Escape(string.Join('\n', code))).Append("</code></pre>\n");
        return Math.Min(i + 1, lines.Count);
    }

    private static int RenderList(IReadOnlyList<string> lines, int start, StringBuilder html, Func<int, string?>? itemLink)
    {
        var ordered = OrderedPattern().IsMatch(lines[start]);
        var items = new List<List<string>>();
        var i = start;
        while (i < lines.Count)
        {
            var line = lines[i];
            var match = ordered ? OrderedPattern().Match(line) : BulletPattern().Match(line);
            if (match.Success)
            {
                items.Add([match.Groups[1].Value]);
                i++;
                continue;
            }

            if (items.Count > 0 && line.Length > 0 && char.IsWhiteSpace(line[0]) && line.Trim().Length > 0
                && !BulletPattern().IsMatch(line) && !OrderedPattern().IsMatch(line))
            {
                items[^1].Add(line.Trim());
                i++;
                continue;
            }

            break;
        }

        var tag = ordered ? "ol" : "ul";
        html.Append('<').Append(tag).Append(">\n");
        foreach (var item in items)
        {
            html.Append("<li>");
            var first = item[0];
            var task = TaskPattern().Match(first);
            if (task.Success)
            {
                var done = task.Groups[1].Value is "x" or "X";
                html.Append(done ? "<input type=\"checkbox\" disabled checked> " : "<input type=\"checkbox\" disabled> ");
                item[0] = first[task.Length..];
            }

            for (var k = 0; k < item.Count; k++)
            {
                if (k > 0)
                {
                    html.Append("<br>");
                }

                RenderInline(item[k], html, itemLink, true);
            }

            html.Append("</li>\n");
        }

        html.Append("</").Append(tag).Append(">\n");
        return i;
    }

    private static void RenderParagraph(IReadOnlyList<string> lines, StringBuilder html, Func<int, string?>? itemLink)
    {
        html.Append("<p>");
        for (var k = 0; k < lines.Count; k++)
        {
            if (k > 0)
            {
                html.Append("<br>\n");
            }

            RenderInline(lines[k].Trim(), html, itemLink, true);
        }

        html.Append("</p>\n");
    }

    private static void RenderInline(string text, StringBuilder html, Func<int, string?>? itemLink, bool allowLinks)
    {
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < text.Length && char.IsAsciiLetterOrDigit(text[i + 1]) is false && !char.IsWhiteSpace(text[i + 1]))
            {
                AppendEscaped(html, text[i + 1]);
                i += 2;
                continue;
            }

            if (c == '`' && TryCode(text, i, html, out var afterCode))
            {
                i = afterCode;
                continue;
            }

            if (allowLinks && c == '!' && i + 1 < text.Length && text[i + 1] == '[' && TryLink(text, i + 1, out var alt, out var source, out var afterImage))
            {
                if (IsLocalPath(source))
                {
                    html.Append("<img src=\"").Append(Escape(source)).Append("\" alt=\"").Append(Escape(alt)).Append("\" loading=\"lazy\">");
                }
                else
                {
                    AppendLink(html, source, alt, itemLink);
                }

                i = afterImage;
                continue;
            }

            if (allowLinks && c == '[' && TryLink(text, i, out var label, out var target, out var afterLink))
            {
                AppendLink(html, target, label, itemLink);
                i = afterLink;
                continue;
            }

            if (c is '*' or '_' or '~' && TryEmphasis(text, i, html, itemLink, allowLinks, out var afterEmphasis))
            {
                i = afterEmphasis;
                continue;
            }

            if (allowLinks && c is 'h' or 'H' && IsWordStart(text, i) && TryAutoLink(text, i, html, out var afterAuto))
            {
                i = afterAuto;
                continue;
            }

            if (allowLinks && c == '#' && itemLink is not null && IsWordStart(text, i) && TryItemLink(text, i, html, itemLink, out var afterItem))
            {
                i = afterItem;
                continue;
            }

            AppendEscaped(html, c);
            i++;
        }
    }

    private static bool IsWordStart(string text, int index) =>
        index == 0 || char.IsWhiteSpace(text[index - 1]) || text[index - 1] is '(' or '[';

    private static bool TryCode(string text, int start, StringBuilder html, out int after)
    {
        var run = 0;
        while (start + run < text.Length && text[start + run] == '`')
        {
            run++;
        }

        var fence = new string('`', run);
        var close = text.IndexOf(fence, start + run, StringComparison.Ordinal);
        if (close < 0 || close == start + run)
        {
            after = start;
            return false;
        }

        html.Append("<code>").Append(Escape(text[(start + run)..close].Trim())).Append("</code>");
        after = close + run;
        return true;
    }

    private static bool TryLink(string text, int open, out string label, out string url, out int after)
    {
        label = string.Empty;
        url = string.Empty;
        after = open;
        var depth = 0;
        var close = -1;
        for (var k = open; k < text.Length; k++)
        {
            if (text[k] == '[')
            {
                depth++;
            }
            else if (text[k] == ']')
            {
                depth--;
                if (depth == 0)
                {
                    close = k;
                    break;
                }
            }
        }

        if (close < 0 || close + 1 >= text.Length || text[close + 1] != '(')
        {
            return false;
        }

        var end = text.IndexOf(')', close + 2);
        if (end < 0)
        {
            return false;
        }

        var inside = text[(close + 2)..end].Trim();
        var space = inside.IndexOf(' ', StringComparison.Ordinal);
        url = space < 0 ? inside : inside[..space];
        if (url.Length == 0)
        {
            return false;
        }

        label = text[(open + 1)..close];
        after = end + 1;
        return true;
    }

    private static void AppendLink(StringBuilder html, string url, string label, Func<int, string?>? itemLink)
    {
        if (!IsSafeLink(url))
        {
            RenderInline(label, html, itemLink, false);
            return;
        }

        html.Append("<a href=\"").Append(Escape(url)).Append('"');
        if (!IsLocalPath(url))
        {
            html.Append(" rel=\"noopener noreferrer nofollow\" target=\"_blank\"");
        }

        html.Append('>');
        RenderInline(label.Length == 0 ? url : label, html, itemLink, false);
        html.Append("</a>");
    }

    private static bool TryEmphasis(string text, int start, StringBuilder html, Func<int, string?>? itemLink, bool allowLinks, out int after)
    {
        after = start;
        var c = text[start];
        var doubled = start + 1 < text.Length && text[start + 1] == c;
        if (c == '~' && !doubled)
        {
            return false;
        }

        if (c == '_' && start > 0 && char.IsLetterOrDigit(text[start - 1]))
        {
            return false;
        }

        var marker = doubled ? new string(c, 2) : c.ToString();
        var contentStart = start + marker.Length;
        if (contentStart >= text.Length || char.IsWhiteSpace(text[contentStart]))
        {
            return false;
        }

        var close = contentStart;
        while (true)
        {
            close = text.IndexOf(marker, close + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                return false;
            }

            if (char.IsWhiteSpace(text[close - 1]))
            {
                continue;
            }

            if (!doubled && close + 1 < text.Length && text[close + 1] == c)
            {
                close++;
                continue;
            }

            if (c == '_' && close + marker.Length < text.Length && char.IsLetterOrDigit(text[close + marker.Length]))
            {
                continue;
            }

            break;
        }

        var tag = c == '~' ? "del" : doubled ? "strong" : "em";
        html.Append('<').Append(tag).Append('>');
        RenderInline(text[contentStart..close], html, itemLink, allowLinks);
        html.Append("</").Append(tag).Append('>');
        after = close + marker.Length;
        return true;
    }

    private static bool TryAutoLink(string text, int start, StringBuilder html, out int after)
    {
        after = start;
        var rest = text.AsSpan(start);
        if (!rest.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !rest.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var end = start;
        while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] is not '<' and not '>' and not '"')
        {
            end++;
        }

        while (end > start && text[end - 1] is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '\'')
        {
            end--;
        }

        var url = text[start..end];
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        html.Append("<a href=\"").Append(Escape(url)).Append("\" rel=\"noopener noreferrer nofollow\" target=\"_blank\">")
            .Append(Escape(url)).Append("</a>");
        after = end;
        return true;
    }

    private static bool TryItemLink(string text, int start, StringBuilder html, Func<int, string?> itemLink, out int after)
    {
        after = start;
        var end = start + 1;
        while (end < text.Length && char.IsAsciiDigit(text[end]) && end - start <= 9)
        {
            end++;
        }

        if (end == start + 1 || (end < text.Length && char.IsLetterOrDigit(text[end])))
        {
            return false;
        }

        if (!int.TryParse(text.AsSpan(start + 1, end - start - 1), out var number) || number <= 0)
        {
            return false;
        }

        var href = itemLink(number);
        if (href is null || !IsLocalPath(href))
        {
            return false;
        }

        html.Append("<a href=\"").Append(Escape(href)).Append("\">#").Append(number).Append("</a>");
        after = end;
        return true;
    }

    private static void AppendEscaped(StringBuilder builder, char c)
    {
        switch (c)
        {
            case '&':
                builder.Append("&amp;");
                break;
            case '<':
                builder.Append("&lt;");
                break;
            case '>':
                builder.Append("&gt;");
                break;
            case '"':
                builder.Append("&quot;");
                break;
            case '\'':
                builder.Append("&#39;");
                break;
            default:
                builder.Append(c);
                break;
        }
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^\s{0,3}([-*_])(\s*\1){2,}\s*$")]
    private static partial Regex RulePattern();

    [GeneratedRegex(@"^\s{0,3}[-*+]\s+(.*)$")]
    private static partial Regex BulletPattern();

    [GeneratedRegex(@"^\s{0,3}\d{1,9}[.)]\s+(.*)$")]
    private static partial Regex OrderedPattern();

    [GeneratedRegex(@"^\[([ xX])\]\s+")]
    private static partial Regex TaskPattern();
}
