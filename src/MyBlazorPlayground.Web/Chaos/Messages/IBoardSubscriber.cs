namespace MyBlazorPlayground.Web.Chaos.Messages;

internal interface IBoardSubscriber
{
    bool Deliver(BoardUpdate update);
}
