namespace MyBlazorPlayground.Web.Chaos.Postcards;

internal interface IPostcardSubscriber
{
    bool Deliver(PostcardUpdate update);
}
