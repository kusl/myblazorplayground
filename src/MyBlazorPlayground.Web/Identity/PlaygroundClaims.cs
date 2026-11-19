using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using MyBlazorPlayground.Data.People;
using MyBlazorPlayground.Domain.Access;

namespace MyBlazorPlayground.Web.Identity;

public static class PlaygroundClaims
{
    public const string DisplayNameClaim = "playground:display_name";
    public const string StampClaim = "playground:stamp";
    public const string RememberClaim = "playground:remember";

    internal static ClaimsPrincipal Create(PersonRecord person)
    {
        ArgumentNullException.ThrowIfNull(person);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, person.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, person.UserName),
            new(DisplayNameClaim, person.DisplayName),
            new(StampClaim, person.SecurityStamp.ToString("D", CultureInfo.InvariantCulture)),
        };
        if (person.IsAdministrator)
        {
            claims.Add(new Claim(ClaimTypes.Role, SiteRoles.Administrator));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));
    }

    internal static ClaimsPrincipal Pending(PersonRecord person, bool remember)
    {
        ArgumentNullException.ThrowIfNull(person);
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, person.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(StampClaim, person.SecurityStamp.ToString("D", CultureInfo.InvariantCulture)),
            new(RememberClaim, remember ? "true" : "false"),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, IdentityRegistration.TwoFactorScheme));
    }

    public static Guid? PersonId(this ClaimsPrincipal? principal) =>
        Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static string UserName(this ClaimsPrincipal? principal) => principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public static string DisplayName(this ClaimsPrincipal? principal) =>
        principal?.FindFirstValue(DisplayNameClaim) is { Length: > 0 } name ? name : principal.UserName();

    public static bool IsAdministrator(this ClaimsPrincipal? principal) => principal?.IsInRole(SiteRoles.Administrator) ?? false;

    public static Actor? ToActor(this ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true && principal.PersonId() is { } id
            ? new Actor(id, principal.UserName(), principal.DisplayName(), principal.IsAdministrator())
            : null;
}
