using Microsoft.Extensions.Options;
using TCSched.Api.Configuration;

namespace TCSched.Api.Tests;

public class StoreClockTests
{
    private static StoreClock ClockFor(string timeZone) => new(
        Options.Create(new StoreOptions { Name = "Test", Address = "Test", TimeZone = timeZone }),
        TimeProvider.System);

    [Theory]
    [InlineData("America/Los_Angeles", "2027-01-16T02:00:00Z", "PST")]
    [InlineData("America/Los_Angeles", "2027-07-16T01:00:00Z", "PDT")]
    [InlineData("America/Los_Angeles", "2026-11-01T08:30:00Z", "PDT")] // 1:30 AM, first time round
    [InlineData("America/Los_Angeles", "2026-11-01T09:30:00Z", "PST")] // 1:30 AM, second time round
    [InlineData("America/New_York", "2027-01-15T23:00:00Z", "EST")]
    [InlineData("America/New_York", "2027-07-15T22:00:00Z", "EDT")]
    public void Zone_abbreviation_follows_daylight_saving_at_that_instant(string timeZone, string utc, string expected)
    {
        var instant = DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(expected, ClockFor(timeZone).ZoneAbbreviation(instant));
    }

    [Fact]
    public void Ambiguous_fall_back_time_resolves_to_standard_time()
    {
        // 1:30 AM on Nov 1 2026 happens twice in Los Angeles; the later one is PST.
        var clock = ClockFor("America/Los_Angeles");

        Assert.True(clock.TryLocalToUtc("2026-11-01T01:30", out var utc, out _));
        Assert.Equal(new DateTime(2026, 11, 1, 9, 30, 0, DateTimeKind.Utc), utc);
        Assert.Equal("PST", clock.ZoneAbbreviation(utc));
    }
}
