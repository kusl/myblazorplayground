namespace MyBlazorPlayground.Web.Chaos.Latency;

internal interface ILatencySubscriber
{
    bool Deliver(LatencyUpdate update);
}
