using Microsoft.Extensions.Options;
using MyBlazorPlayground.Web.Chaos.Drawing;
using MyBlazorPlayground.Web.Chaos.Latency;
using MyBlazorPlayground.Web.Chaos.Messages;
using MyBlazorPlayground.Web.Chaos.Postcards;
using MyBlazorPlayground.Web.Chaos.Reading;
using MyBlazorPlayground.Web.Chaos.Slots;

namespace MyBlazorPlayground.Web.Chaos;

internal static class ChaosRegistration
{
    public static IServiceCollection AddChaos(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ChaosOptions>()
            .Bind(configuration.GetSection(ChaosOptions.SectionName))
            .Validate(chaos => chaos.IsValid(), "Chaos limits are out of range.")
            .ValidateOnStart();
        services.AddSingleton<BoardService>();
        services.AddSingleton<PostcardService>();
        services.AddSingleton<CanvasService>();
        services.AddSingleton<SlotService>();
        services.AddSingleton<ILatencyProbe, HttpLatencyProbe>();
        services.AddSingleton<LatencyService>();
        services.AddSingleton(provider => new ArticleLibrary(
            provider.GetRequiredService<IWebHostEnvironment>().WebRootFileProvider,
            provider.GetRequiredService<IOptions<ChaosOptions>>(),
            provider.GetRequiredService<ILogger<ArticleLibrary>>()));
        return services;
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
