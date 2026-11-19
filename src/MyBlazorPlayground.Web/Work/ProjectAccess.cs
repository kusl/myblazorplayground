using MyBlazorPlayground.Data.Work;
using MyBlazorPlayground.Domain.Access;

namespace MyBlazorPlayground.Web.Work;

internal sealed record ProjectAccess(ProjectRecord Project, Actor Actor)
{
    public string Key => Project.Key;

    public string? Role => Project.Role;

    public bool CanEdit => AccessPolicy.CanEdit(Actor, Role);

    public bool CanDelete => AccessPolicy.CanSoftDelete(Actor, Role);

    public bool CanManage => AccessPolicy.CanManageMembers(Actor, Role);

    public bool CanHardDelete => AccessPolicy.CanHardDelete(Actor);

    public string Path(string? rest = null) => rest is null ? $"work/{Key}" : $"work/{Key}/{rest}";

    public string ItemPath(int number) => $"work/{Key}/items/{number}";

    public string? ItemLink(int number) => $"/work/{Key}/items/{number}";

    public string RenderRich(string? markdown) => MyBlazorPlayground.Domain.Text.RichText.ToHtml(markdown, ItemLink);
}
