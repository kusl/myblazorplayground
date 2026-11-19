namespace MyBlazorPlayground.Web.Identity;

public static class LocalUrl
{
    public static string Or(string? url, string fallback) =>
        !string.IsNullOrEmpty(url)
        && url[0] == '/'
        && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'))
        && !url.Contains("://", StringComparison.Ordinal)
        && !url.Any(char.IsControl)
            ? url
            : fallback;
}
