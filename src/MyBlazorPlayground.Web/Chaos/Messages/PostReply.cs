namespace MyBlazorPlayground.Web.Chaos.Messages;

internal sealed record PostReply(PostResult Result, BoardMessage? Message = null);
