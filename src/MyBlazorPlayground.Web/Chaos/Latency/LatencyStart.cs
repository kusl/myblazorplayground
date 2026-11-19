using System.Globalization;

namespace MyBlazorPlayground.Web.Chaos.Latency;

internal enum StartResult
{
    Accepted,
    Busy,
}

internal sealed record LatencyStart(StartResult Result, Guid Id)
{
    public static LatencyStart Busy { get; } = new(StartResult.Busy, Guid.Empty);

    public static LatencyStart Started(Guid id) => new(StartResult.Accepted, id);

    public string Reply => Result == StartResult.Accepted
        ? string.Create(CultureInfo.InvariantCulture, $"ok {Id:D}")
        : "busy";
}
