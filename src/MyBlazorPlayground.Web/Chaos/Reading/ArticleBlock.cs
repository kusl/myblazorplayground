namespace MyBlazorPlayground.Web.Chaos.Reading;

internal sealed record ArticleBlock(IReadOnlyList<string> Lines, bool IsList)
{
    public string Text => string.Join('\n', Lines);
}
