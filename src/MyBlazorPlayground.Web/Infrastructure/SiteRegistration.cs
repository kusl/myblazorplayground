using System.IO.Compression;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Options;

namespace MyBlazorPlayground.Web.Infrastructure;

internal static class SiteRegistration
{
    public static WebApplicationBuilder AddSite(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            if (int.TryParse(Environment.GetEnvironmentVariable("PORT"), out var port))
            {
                kestrel.ListenAnyIP(port);
            }
        });

        var services = builder.Services;
        services.Configure<HostOptions>(host => host.ShutdownTimeout = TimeSpan.FromSeconds(10));
        services.Configure<ForwardedHeadersOptions>(forwarded =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwarded.KnownIPNetworks.Clear();
            forwarded.KnownProxies.Clear();
        });
        services.AddOptions<SiteOptions>()
            .Bind(builder.Configuration.GetSection(SiteOptions.SectionName))
            .Validate(site => site.IsValid(), "Site settings are out of range.")
            .ValidateOnStart();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ConnectionTracker>();
        services.AddSingleton<LocalTime>();
        services.AddHealthChecks();

        services.AddResponseCompression(compression =>
        {
            compression.EnableForHttps = true;
            compression.Providers.Add<BrotliCompressionProvider>();
            compression.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(brotli => brotli.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(gzip => gzip.Level = CompressionLevel.Fastest);

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<SiteOptions>>((limiter, site) => RequestLimits.Configure(limiter, site.Value));

        services.AddRazorComponents()
            .AddInteractiveServerComponents(circuit =>
            {
                circuit.DisconnectedCircuitMaxRetained = 40;
                circuit.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(1);
                circuit.PersistedCircuitInMemoryMaxRetained = 40;
                circuit.PersistedCircuitInMemoryRetentionPeriod = TimeSpan.FromMinutes(5);
                circuit.MaxBufferedUnacknowledgedRenderBatches = 2;
                circuit.JSInteropDefaultCallTimeout = TimeSpan.FromSeconds(30);
            })
            .AddHubOptions(hub => hub.MaximumReceiveMessageSize = 32 * 1024);
        return builder;
    }
}
