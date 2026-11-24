using MyBlazorPlayground.Web.Chaos;
using MyBlazorPlayground.Web.Components;
using MyBlazorPlayground.Web.Identity;
using MyBlazorPlayground.Web.Nest;
using MyBlazorPlayground.Web.Persistence;
using MyBlazorPlayground.Web.Work;
using MyBlazorPlayground.Web.Administration;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var publishedWebRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Directory.Exists(publishedWebRoot) ? publishedWebRoot : null,
});

builder.AddSite();
builder.Services
    .AddPersistence(builder.Configuration)
    .AddPlaygroundIdentity()
    .AddChaos(builder.Configuration)
    .AddNest(builder.Configuration)
    .AddWork(builder.Configuration)
    .AddScoped<TrashService>();

var app = builder.Build();

await app.Services.GetRequiredService<StartupTasks>().RunAsync(app.Lifetime.ApplicationStopping);

app.UseForwardedHeaders();
app.UseSecurityHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseResponseCompression();
app.UseRateLimiter();
app.UseWebSockets();
app.UseHubGuard();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseChaos();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
app.MapChaos();
app.MapAccountEndpoints();
app.MapWorkEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(server =>
    {
        server.DisableWebSocketCompression = true;
        server.ConfigureConnection = HubGuard.ConfigureConnection;
    });

await app.RunAsync();

public partial class Program;
