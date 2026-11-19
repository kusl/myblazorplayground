using MyBlazorPlayground.Data;
using MyBlazorPlayground.Web.Identity;

namespace MyBlazorPlayground.Web.Persistence;

internal sealed class StartupTasks(MigrationRunner migrations, IEnumerable<IStateLoader> loaders, SetupGate setup)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await migrations.RunAsync(60, TimeSpan.FromSeconds(2), cancellationToken);
        foreach (var loader in loaders)
        {
            await loader.LoadAsync(cancellationToken);
        }

        await setup.InitializeAsync(cancellationToken);
    }
}
