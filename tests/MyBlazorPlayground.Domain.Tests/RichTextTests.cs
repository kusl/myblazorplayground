using System.Text.RegularExpressions;
using MyBlazorPlayground.Domain.Text;

namespace MyBlazorPlayground.Domain.Tests;

public sealed class RichTextTests
{
    private static readonly string[] AllowedTags = ["p", "br", "strong", "em", "del", "code", "pre", "a", "img", "h3", "h4", "h5", "h6", "hr", "ul", "ol", "li", "blockquote", "input"];
    private static readonly string[] AllowedAttributes = ["href", "src", "alt", "rel", "target", "loading", "class", "type", "disabled", "checked"];

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("hello", "<p>hello</p>\n")]
    [InlineData("a\nb", "<p>a<br>\nb</p>\n")]
    [InlineData("a\n\nb", "<p>a</p>\n<p>b</p>\n")]
    [InlineData("**bold** and *it* and ~~old~~", "<p><strong>bold</strong> and <em>it</em> and <del>old</del></p>\n")]
    [InlineData("snake_case_name", "<p>snake_case_name</p>\n")]
    [InlineData("_lean_", "<p><em>lean</em></p>\n")]
    [InlineData("`<b>`", "<p><code>&lt;b&gt;</code></p>\n")]
    [InlineData("# Title", "<h3>Title</h3>\n")]
    [InlineData("### Deep ###", "<h5>Deep</h5>\n")]
    [InlineData("---", "<hr>\n")]
    [InlineData("- one\n- two", "<ul>\n<li>one</li>\n<li>two</li>\n</ul>\n")]
    [InlineData("1. one\n2. two", "<ol>\n<li>one</li>\n<li>two</li>\n</ol>\n")]
    [InlineData("- [x] done\n- [ ] open", "<ul>\n<li><input type=\"checkbox\" disabled checked> done</li>\n<li><input type=\"checkbox\" disabled> open</li>\n</ul>\n")]
    [InlineData("> quoted", "<blockquote>\n<p>quoted</p>\n</blockquote>\n")]
    [InlineData("```js\nlet a = '<x>';\n```", "<pre><code class=\"language-js\">let a = &#39;&lt;x&gt;&#39;;</code></pre>\n")]
    [InlineData("\\*not italic\\*", "<p>*not italic*</p>\n")]
    public void RendersTheSupportedSubset(string? markdown, string expected)
    {
        Assert.Equal(expected, RichText.ToHtml(markdown));
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("![x](javascript:alert(1))")]
    [InlineData("[x](data:text/html,hi)")]
    [InlineData("\" onmouseover=\"alert(1)")]
    [InlineData("[a](https://example.com\" onclick=\"x)")]
    public void NeverEmitsActiveContent(string markdown)
    {
        var html = RichText.ToHtml(markdown);
        foreach (Match tag in Regex.Matches(html, "<(/?)([a-z0-9]+)([^>]*)>"))
        {
            Assert.Contains(tag.Groups[2].Value, AllowedTags);
            foreach (Match attribute in Regex.Matches(tag.Groups[3].Value, "\\s([a-z-]+)(?:=\"([^\"]*)\")?"))
            {
                Assert.Contains(attribute.Groups[1].Value, AllowedAttributes);
                var value = attribute.Groups[2].Value;
                Assert.False(value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase));
                Assert.False(value.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
            }

            Assert.Equal(tag.Groups[3].Value, Regex.Replace(tag.Groups[3].Value, "\\s[a-z-]+(=\"[^\"]*\")?", m => m.Value));
        }

        Assert.Equal(html.Count(c => c == '<'), Regex.Matches(html, "<(/?)([a-z0-9]+)([^>]*)>").Count);
    }

    [Fact]
    public void LinksOpenSafely()
    {
        Assert.Equal(
            "<p><a href=\"https://example.com/a?b=1&amp;c=2\" rel=\"noopener noreferrer nofollow\" target=\"_blank\">site</a></p>\n",
            RichText.ToHtml("[site](https://example.com/a?b=1&c=2)"));
        Assert.Equal("<p><a href=\"/work/A/items/1\">one</a></p>\n", RichText.ToHtml("[one](/work/A/items/1)"));
    }

    [Fact]
    public void AutoLinksBareUrls()
    {
        Assert.Equal(
            "<p>see <a href=\"https://example.com/x\" rel=\"noopener noreferrer nofollow\" target=\"_blank\">https://example.com/x</a>.</p>\n",
            RichText.ToHtml("see https://example.com/x."));
    }

    [Fact]
    public void EmbedsOnlyLocalImages()
    {
        Assert.Equal("<p><img src=\"/work/files/1/a.png\" alt=\"shot\" loading=\"lazy\"></p>\n", RichText.ToHtml("![shot](/work/files/1/a.png)"));
        Assert.Equal(
            "<p><a href=\"https://example.com/a.png\" rel=\"noopener noreferrer nofollow\" target=\"_blank\">remote</a></p>\n",
            RichText.ToHtml("![remote](https://example.com/a.png)"));
        Assert.Equal("<p>sneaky</p>\n", RichText.ToHtml("![sneaky](//evil.example/a.png)"));
    }

    [Fact]
    public void LinksItemNumbersWhenAsked()
    {
        var html = RichText.ToHtml("fixes #12 but not x#3 or #0", number => $"/work/ABC/items/{number}");
        Assert.Equal("<p>fixes <a href=\"/work/ABC/items/12\">#12</a> but not x#3 or #0</p>\n", html);
        Assert.Equal("<p>#12</p>\n", RichText.ToHtml("#12"));
    }

    [Fact]
    public void LimitsLength()
    {
        var html = RichText.ToHtml(new string('a', RichText.MaxLength + 500));
        Assert.Equal(RichText.MaxLength + "<p></p>\n".Length, html.Length);
    }

    [Theory]
    [InlineData("/work/a", true)]
    [InlineData("//evil", false)]
    [InlineData("/\\evil", false)]
    [InlineData("/a b", false)]
    [InlineData("work", false)]
    [InlineData("/", false)]
    public void RecognizesLocalPaths(string url, bool expected)
    {
        Assert.Equal(expected, RichText.IsLocalPath(url));
    }
}
