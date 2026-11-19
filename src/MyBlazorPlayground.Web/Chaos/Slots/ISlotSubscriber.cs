namespace MyBlazorPlayground.Web.Chaos.Slots;

internal interface ISlotSubscriber
{
    bool Deliver(SlotUpdate update);
}
