using Microsoft.Extensions.Options;
using MyBlazorPlayground.Web.Chaos.Drawing;
using MyBlazorPlayground.Web.Chaos.Latency;
using MyBlazorPlayground.Web.Chaos.Messages;
using MyBlazorPlayground.Web.Chaos.Postcards;
using MyBlazorPlayground.Web.Chaos.Reading;
using MyBlazorPlayground.Web.Chaos.Slots;
using MyBlazorPlayground.Web.Chaos.Storage;

namespace MyBlazorPlayground.Web.Chaos;

internal static class ChaosRegistration
{
    public static IServiceCollection AddChaos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChaosOptions>()
            .Bind(configuration.GetSection(ChaosOptions.SectionName))
            .Validate(chaos => chaos.IsValid(), "Chaos limits are out of range.")
            .ValidateOnStart();
        AddJournal<BoardJournal>(services);
        AddJournal<SlotJournal>(services);
        AddJournal<LatencyJournal>(services);
        AddJournal<CanvasJournal>(services);
        AddJournal<PostcardJournal>(services);
        services.AddSingleton(provider => new BoardService(Time(provider), Limits(provider), provider.GetRequiredService<BoardJournal>()));
        services.AddSingleton(provider => new PostcardService(Time(provider), Limits(provider), provider.GetRequiredService<PostcardJournal>()));
        services.AddSingleton(provider => new CanvasService(Time(provider), Limits(provider), provider.GetRequiredService<CanvasJournal>()));
        services.AddSingleton(provider => new SlotService(Time(provider), Limits(provider), provider.GetRequiredService<SlotJournal>()));
        services.AddSingleton<ILatencyProbe, HttpLatencyProbe>();
        services.AddSingleton(provider => new LatencyService(
            Time(provider),
            Limits(provider),
            provider.GetRequiredService<ILatencyProbe>(),
            provider.GetRequiredService<ILogger<LatencyService>>(),
            provider.GetRequiredService<LatencyJournal>()));
        services.AddSingleton<IStateLoader, BoardLoader>();
        services.AddSingleton<IStateLoader, SlotLoader>();
        services.AddSingleton<IStateLoader, LatencyLoader>();
        services.AddSingleton<IStateLoader, CanvasLoader>();
        services.AddSingleton<IStateLoader, PostcardLoader>();
        services.AddSingleton(provider => new ArticleLibrary(
            provider.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider,
            provider.GetRequiredService<IOptions<ChaosOptions>>(),
            provider.GetRequiredService<ILogger<ArticleLibrary>>()));
        return services;
    }

    private static TimeProvider Time(IServiceProvider provider) => provider.GetRequiredService<TimeProvider>();

    private static IOptions<ChaosOptions> Limits(IServiceProvider provider) => provider.GetRequiredService<IOptions<ChaosOptions>>();

    private static void AddJournal<TJournal>(IServiceCollection services)
        where TJournal : class, IHostedService
    {
        services.AddSingleton<TJournal>();
        services.AddHostedService(provider => provider.GetRequiredService<TJournal>());
    }

    public static IApplicationBuilder UseChaos(this IApplicationBuilder app) => app.UseRandomArticle();

    public static IEndpointRouteBuilder MapChaos(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/time", static (TimeProvider time, HttpResponse response) =>
        {
            response.Headers.CacheControl = "no-store";
            return Results.Json(ServerTime.Now(time));
        });
        endpoints.MapPostcardImages();
        return endpoints;
    }
}
