using System.Globalization;
using Microsoft.Extensions.Options;

namespace TCSched.Api.Configuration;

/// <summary>
/// Converts between the store's local wall-clock time and UTC. Organizers type times as
/// they appear on the store's clock, so the browser's time zone never matters.
/// </summary>
public sealed class StoreClock(IOptions<StoreOptions> options, TimeProvider time)
{
    private static readonly string[] LocalFormats = ["yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"];

    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Parses "yyyy-MM-ddTHH:mm" (no offset) as store-local time and converts it to UTC.
    /// Fails for malformed input and for times skipped by a daylight-saving jump.
    /// Times that occur twice (fall back) resolve to standard time.
    /// </summary>
    public bool TryLocalToUtc(string? local, out DateTime utc, out string? error)
    {
        utc = default;
        if (!DateTime.TryParseExact(local, LocalFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wall))
        {
            error = "Start time must be a local date and time like 2026-11-07T18:00 (no time zone offset).";
            return false;
        }

        if (TimeZone.IsInvalidTime(wall))
        {
            error = $"{wall:yyyy-MM-dd HH:mm} doesn't exist in {TimeZone.Id} (daylight saving change). Pick another time.";
            return false;
        }

        utc = TimeZoneInfo.ConvertTimeToUtc(wall, TimeZone);
        error = null;
        return true;
    }
}
