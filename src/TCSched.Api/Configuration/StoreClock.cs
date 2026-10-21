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

        utc = LocalToUtc(wall);
        error = null;
        return true;
    }

    /// <summary>Store-local wall time → UTC. Ambiguous (fall-back) times resolve to standard time.</summary>
    public DateTime LocalToUtc(DateTime wall) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(wall, DateTimeKind.Unspecified), TimeZone);

    /// <summary>
    /// Short zone name at an instant, e.g. "PST" in January and "PDT" in July. .NET has no
    /// abbreviations, so this uses the initials of the localized name ("Pacific Daylight Time").
    /// Falls back to the UTC offset ("UTC-07:00") when the name isn't plain words.
    /// </summary>
    public string ZoneAbbreviation(DateTime utc)
    {
        // Ask with the UTC instant: a local time like 1:30 AM on fall-back day is ambiguous,
        // and .NET would treat it as standard time.
        var instant = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var name = TimeZone.IsDaylightSavingTime(instant) ? TimeZone.DaylightName : TimeZone.StandardName;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.All(w => w.All(char.IsLetter)))
            return string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));

        var offset = TimeZone.GetUtcOffset(instant);
        return $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset:hh\\:mm}";
    }

    /// <summary>The current wall-clock time at the store.</summary>
    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone);

    /// <summary>Today's date on the store's clock.</summary>
    public DateOnly LocalToday => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(UtcNow, TimeZone));
}
