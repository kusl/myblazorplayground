namespace MyBlazorPlayground.Web.Nest;

internal interface INestSubscriber
{
    bool Deliver(NestUpdate update);
}
