using System.Net;
using Ical.Net;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class IcsTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Downloads_an_importable_calendar_file()
    {
        // 6:00 PM Chicago in January = 00:00 UTC next day; MTG runs 240 minutes.
        var ev = await _client.CreateEventOkAsync(new("Friday Night Magic", "mtg", "Draft", "2027-01-15T18:00", 8));

        var res = await _client.GetAsync($"/api/events/{ev.Id}/calendar.ics");

        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/calendar", res.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", res.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("friday-night-magic-2027-01-15.ics", res.Content.Headers.ContentDisposition?.FileName);

        var text = await res.Content.ReadAsStringAsync();
        var vevent = Assert.Single(Calendar.Load(text)!.Events);
        Assert.Equal("Friday Night Magic", vevent.Summary);
        Assert.Equal(new DateTime(2027, 1, 16, 0, 0, 0, DateTimeKind.Utc), vevent.DtStart!.AsUtc);
        Assert.Equal(new DateTime(2027, 1, 16, 4, 0, 0, DateTimeKind.Utc), vevent.DtEnd!.AsUtc);
        Assert.Equal("Dragon's Hoard Games, 123 Main St, Springfield, IL 62701", vevent.Location);
        Assert.Equal($"{ev.Id}@tcsched", vevent.Uid);
        Assert.Contains("Magic: The Gathering · Draft", vevent.Description);
        Assert.Contains($"/e/{ev.Id}", vevent.Description);

        // Raw-format checks: times are written as UTC ("Z"), not as floating local times.
        Assert.Contains("DTSTART:20270116T000000Z", text);
        Assert.Contains("DTEND:20270116T040000Z", text);
        Assert.Contains("METHOD:PUBLISH", text);
    }

    [Fact]
    public async Task Summer_event_uses_daylight_time_offset()
    {
        // 6:00 PM Chicago in July is CDT (UTC-5) = 23:00 UTC.
        var ev = await _client.CreateEventOkAsync(new("Pokémon League", "pokemon", null, "2027-07-10T18:00", null));

        var text = await _client.GetStringAsync($"/api/events/{ev.Id}/calendar.ics");

        Assert.Contains("DTSTART:20270710T230000Z", text);
        Assert.Contains("DTEND:20270711T020000Z", text); // Pokémon: 180 minutes
    }

    [Fact]
    public async Task Unknown_event_returns_404()
    {
        var res = await _client.GetAsync($"/api/events/{Guid.NewGuid()}/calendar.ics");

        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }
}

public class IcsConfiguredBaseUrlTests
{
    private sealed class Factory : TestAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Store:PublicBaseUrl", "http://192.168.1.20:5173/");
        }
    }

    [Fact]
    public async Task Registration_link_uses_configured_public_base_url()
    {
        using var factory = new Factory();
        var client = factory.CreateClient();
        var ev = await client.CreateEventOkAsync(new("FNM", "mtg", null, "2027-01-15T18:00", null));

        var vevent = Calendar.Load(await client.GetStringAsync($"/api/events/{ev.Id}/calendar.ics"))!.Events.Single();

        Assert.Equal(new Uri($"http://192.168.1.20:5173/e/{ev.Id}"), vevent.Url);
    }
}
