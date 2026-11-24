namespace MyBlazorPlayground.Web.Infrastructure;

internal sealed class SiteOptions
{
    public const string SectionName = "Site";

    public string Name { get; set; } = "my blazor playground";

    public string SourceUrl { get; set; } = "https://github.com/kusl/myblazorplayground";

    public string TimeZone { get; set; } = "UTC";

    public IList<string> AllowedOrigins { get; } = [];

    public int MaxConnectionsPerClient { get; set; } = 64;

    public int MaxConnections { get; set; } = 400;

    public int RequestsPerSecond { get; set; } = 20;

    public int RequestBurst { get; set; } = 240;

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(Name)
        && TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _)
        && Uri.TryCreate(SourceUrl, UriKind.Absolute, out var source)
        && (source.Scheme == Uri.UriSchemeHttps || source.Scheme == Uri.UriSchemeHttp)
        && MaxConnectionsPerClient > 0
        && MaxConnections >= MaxConnectionsPerClient
        && RequestsPerSecond > 0
        && RequestBurst > 0;
}
