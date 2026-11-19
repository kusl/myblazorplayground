using MyBlazorPlayground.Web.Chaos.Latency;

namespace MyBlazorPlayground.Web.Infrastructure;

internal static class SecurityHeaders
{
    public static readonly string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; font-src 'self'; connect-src 'self' "
        + string.Join(' ', LatencyCatalog.Origins)
        + "; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";

    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(self), payment=(), usb=()";

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            context.Response.OnStarting(Apply, context);
            return next(context);
        });

    public static string? WithNoTransform(string? contentType, string? cacheControl)
    {
        if (contentType is null || !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(cacheControl))
        {
            return "no-cache, no-transform";
        }

        return cacheControl.Contains("no-transform", StringComparison.OrdinalIgnoreCase) ? null : cacheControl + ", no-transform";
    }

    private static Task Apply(object state)
    {
        var response = ((HttpContext)state).Response;
        var headers = response.Headers;
        if (WithNoTransform(response.ContentType, headers.CacheControl) is { } cacheControl)
        {
            headers.CacheControl = cacheControl;
        }

        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        headers.XContentTypeOptions = "nosniff";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = PermissionsPolicy;
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        return Task.CompletedTask;
    }
}
