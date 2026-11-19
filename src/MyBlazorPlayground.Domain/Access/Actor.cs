namespace MyBlazorPlayground.Domain.Access;

public sealed record Actor(Guid PersonId, string UserName, string DisplayName, bool IsAdministrator)
{
    public static Actor? Anonymous => null;
}
