using System.Globalization;
using Microsoft.Extensions.Options;

namespace MyBlazorPlayground.Web.Infrastructure;

internal sealed class LocalTime(IOptions<SiteOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public string Format(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public string Iso(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture);

    public DateOnly Today(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);
    }
}
