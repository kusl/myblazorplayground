using MyBlazorPlayground.Data;
using MyBlazorPlayground.Web.Identity;
using MyBlazorPlayground.Web.Nest;

namespace MyBlazorPlayground.Web.Persistence;

internal sealed class StartupTasks(MigrationRunner migrations, NestLoader nest, SetupGate setup)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await migrations.RunAsync(60, TimeSpan.FromSeconds(2), cancellationToken);
        await nest.LoadAsync(cancellationToken);
        await setup.InitializeAsync(cancellationToken);
    }
}
