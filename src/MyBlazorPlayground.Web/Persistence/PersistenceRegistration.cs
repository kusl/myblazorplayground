using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using MyBlazorPlayground.Data;
using MyBlazorPlayground.Data.Audit;
using MyBlazorPlayground.Data.Nest;
using MyBlazorPlayground.Data.Pages;
using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Data.Protection;
using MyBlazorPlayground.Data.Settings;
using MyBlazorPlayground.Data.Toys;
using MyBlazorPlayground.Data.Trash;
using MyBlazorPlayground.Data.Work;
using Npgsql;

namespace MyBlazorPlayground.Web.Persistence;

internal static class PersistenceRegistration
{
    public const string ConnectionStringName = "Playground";

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException($"Set ConnectionStrings__{ConnectionStringName}.");
        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.ConnectionStringBuilder.ApplicationName ??= "myblazorplayground";
            return builder.Build();
        });
        services.AddSingleton<Database>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<PersonStore>();
        services.AddSingleton<TwoFactorStore>();
        services.AddSingleton<SiteSettings>();
        services.AddSingleton<AuditLog>();
        services.AddSingleton<DataProtectionKeyStore>();
        services.AddSingleton<PageEditorStore>();
        services.AddSingleton<ProjectStore>();
        services.AddSingleton<SprintStore>();
        services.AddSingleton<WorkItemStore>();
        services.AddSingleton<CommentStore>();
        services.AddSingleton<AttachmentStore>();
        services.AddSingleton<NestEntryStore>();
        services.AddSingleton<BoardStore>();
        services.AddSingleton<SlotStore>();
        services.AddSingleton<LatencyStore>();
        services.AddSingleton<CanvasStore>();
        services.AddSingleton<PostcardStore>();
        services.AddSingleton<Deletions>();
        services.AddSingleton<StartupTasks>();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        services.AddDataProtection().SetApplicationName("myblazorplayground");
        services.AddOptions<KeyManagementOptions>()
            .Configure<IServiceProvider>((options, provider) =>
                options.XmlRepository = new DatabaseXmlRepository(provider.GetRequiredService<DataProtectionKeyStore>()));
        return services;
    }
}
