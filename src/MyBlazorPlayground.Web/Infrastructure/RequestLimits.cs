using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace MyBlazorPlayground.Web.Infrastructure;

internal static class RequestLimits
{
    public static readonly PathString HealthPath = new("/health");

    public static void Configure(RateLimiterOptions limiter, SiteOptions site)
    {
        ArgumentNullException.ThrowIfNull(limiter);
        ArgumentNullException.ThrowIfNull(site);

        limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        limiter.OnRejected = static async (context, cancellationToken) =>
        {
            context.HttpContext.Response.Headers.RetryAfter = "1";
            context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
            await context.HttpContext.Response.WriteAsync("Too many requests.", cancellationToken);
        };
        limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            context.Request.Path.StartsWithSegments(HealthPath)
                ? RateLimitPartition.GetNoLimiter("health")
                : RateLimitPartition.GetTokenBucketLimiter(
                    ClientAddress.Key(context.Connection.RemoteIpAddress),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = site.RequestBurst,
                        TokensPerPeriod = site.RequestsPerSecond,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0,
                        AutoReplenishment = true,
                    }));
    }
}
