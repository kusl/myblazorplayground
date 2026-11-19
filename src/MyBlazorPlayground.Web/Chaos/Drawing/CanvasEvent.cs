namespace MyBlazorPlayground.Web.Chaos.Drawing;

internal sealed record CanvasEvent(Guid Id, int[]? Points)
{
    public static CanvasEvent Reset(Guid id) => new(id, null);

    public static CanvasEvent Of(Stroke stroke)
    {
        ArgumentNullException.ThrowIfNull(stroke);
        return new CanvasEvent(stroke.Id, stroke.ToArray());
    }
}
