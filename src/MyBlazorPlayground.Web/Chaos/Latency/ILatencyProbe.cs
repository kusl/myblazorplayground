namespace MyBlazorPlayground.Web.Chaos.Latency;

internal interface ILatencyProbe
{
    Task<int?[]> MeasureAsync(IReadOnlyList<LatencyEndpoint> endpoints, TimeSpan budget, CancellationToken cancellationToken);
}
