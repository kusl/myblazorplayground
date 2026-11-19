namespace MyBlazorPlayground.Web.Work;

internal static class WorkRegistration
{
    public static IServiceCollection AddWork(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WorkOptions>()
            .Bind(configuration.GetSection(WorkOptions.SectionName))
            .Validate(work => work.IsValid(), "Work limits are out of range.")
            .ValidateOnStart();
        services.AddScoped<WorkService>();
        return services;
    }
}
