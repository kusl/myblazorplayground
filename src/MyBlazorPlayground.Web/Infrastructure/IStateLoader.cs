namespace MyBlazorPlayground.Web.Infrastructure;

internal interface IStateLoader
{
    Task LoadAsync(CancellationToken cancellationToken);
}
