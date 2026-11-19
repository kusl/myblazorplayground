using MyBlazorPlayground.Web.Infrastructure;

namespace MyBlazorPlayground.Web.Chaos.Drawing;

internal sealed class CanvasSession(ICanvasSubscriber subscriber, TokenBucket bucket)
{
    public ICanvasSubscriber Subscriber { get; } = subscriber;

    public TokenBucket Bucket { get; } = bucket;

    public Stroke? Open { get; set; }

    public int LastClientId { get; set; }

    public Guid LastStroke { get; set; }
}
