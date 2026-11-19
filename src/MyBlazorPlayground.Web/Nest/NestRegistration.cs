using Microsoft.Extensions.Options;

namespace MyBlazorPlayground.Web.Nest;

internal static class NestRegistration
{
    public static IServiceCollection AddNest(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<NestOptions>()
            .Bind(configuration.GetSection(NestOptions.SectionName))
            .Validate(nest => nest.IsValid(), "Nest limits are out of range.")
            .ValidateOnStart();
        services.AddSingleton<NestJournal>();
        services.AddSingleton<INestJournal>(provider => provider.GetRequiredService<NestJournal>());
        services.AddHostedService(provider => provider.GetRequiredService<NestJournal>());
        services.AddSingleton(provider => new NestService(
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<IOptions<NestOptions>>(),
            provider.GetRequiredService<INestJournal>()));
        services.AddSingleton<NestLoader>();
        services.AddScoped<NestModeration>();
        return services;
    }
}
