namespace MyBlazorPlayground.Web.Chaos.Drawing;

internal interface ICanvasSubscriber
{
    bool Deliver(CanvasUpdate update);
}
