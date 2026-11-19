using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Domain.Access;
using MyBlazorPlayground.Domain.Security;

namespace MyBlazorPlayground.Web.Identity;

internal static class IdentityRegistration
{
    public const string AdministratorPolicy = "administrator";
    private const string ValidatedKey = ".playground.validated";
    private static readonly TimeSpan ValidationInterval = TimeSpan.FromSeconds(60);

    public static IServiceCollection AddPlaygroundIdentity(this IServiceCollection services)
    {
        services.AddSingleton(new PasswordHasher());
        services.AddSingleton<SignInThrottle>();
        services.AddSingleton<SetupGate>();
        services.AddScoped<AccountService>();
        services.AddCascadingAuthenticationState();
        services.AddAuthorizationBuilder()
            .AddPolicy(AdministratorPolicy, policy => policy.RequireAuthenticatedUser().RequireRole(SiteRoles.Administrator));
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(cookie =>
            {
                cookie.Cookie.Name = "playground.auth";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.LoginPath = "/account/sign-in";
                cookie.LogoutPath = "/account/sign-out";
                cookie.AccessDeniedPath = "/account/denied";
                cookie.ReturnUrlParameter = "returnUrl";
                cookie.ExpireTimeSpan = TimeSpan.FromDays(14);
                cookie.SlidingExpiration = true;
                cookie.Events.OnValidatePrincipal = ValidateAsync;
            });
        return services;
    }

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/account/sign-out", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            if (!await antiforgery.IsRequestValidAsync(context))
            {
                return Results.BadRequest();
            }

            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("~/");
        });
        return endpoints;
    }

    private static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var time = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var now = time.GetUtcNow().ToUnixTimeSeconds();
        if (context.Properties.Items.TryGetValue(ValidatedKey, out var stored)
            && long.TryParse(stored, NumberStyles.None, CultureInfo.InvariantCulture, out var validated)
            && now - validated < ValidationInterval.TotalSeconds)
        {
            return;
        }

        var principal = context.Principal;
        var people = context.HttpContext.RequestServices.GetRequiredService<PersonStore>();
        var person = principal.PersonId() is { } id ? await people.FindAsync(id, context.HttpContext.RequestAborted) : null;
        if (person is null
            || person.DisabledAt is not null
            || !string.Equals(person.SecurityStamp.ToString("D", CultureInfo.InvariantCulture), principal?.FindFirstValue(PlaygroundClaims.StampClaim), StringComparison.Ordinal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        if (person.IsAdministrator != principal.IsAdministrator()
            || !string.Equals(person.DisplayName, principal.DisplayName(), StringComparison.Ordinal))
        {
            context.ReplacePrincipal(PlaygroundClaims.Create(person));
        }

        context.Properties.Items[ValidatedKey] = now.ToString(CultureInfo.InvariantCulture);
        context.ShouldRenew = true;
    }
}
