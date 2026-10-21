using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Data;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class SampleDataTests
{
    private sealed class SeededFactory : TestAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Seed:SampleEvents", "true");
        }
    }

    [Fact]
    public async Task Sample_events_are_consistent_upcoming_and_seeded_only_once()
    {
        using var factory = new SeededFactory();
        var client = factory.CreateClient();

        var events = await client.GetFromJsonAsync<List<EventDto>>("/api/events");
        Assert.Equal(5, events!.Count);
        var now = TestAppFactory.Now.UtcDateTime;
        Assert.All(events, e => Assert.True(e.StartsAtUtc > now && e.StartsAtUtc < now.AddDays(8)));
        Assert.Contains(events, e => e.IsFull);
        Assert.Contains(events, e => e.SeatsLeft == 1);

        await factory.WithDbAsync(async db =>
        {
            foreach (var e in await db.Events.Include(x => x.Registrations).ToListAsync())
                Assert.Equal(e.RegisteredCount, e.Registrations.Count);
        });

        // Running startup initialization again must not add more events.
        await DbInitializer.InitializeAsync(factory.Services);
        Assert.Equal(5, (await client.GetFromJsonAsync<List<EventDto>>("/api/events"))!.Count);

        // The "one seat left" demo fills with exactly one more registration.
        var almostFull = events.Single(e => e.SeatsLeft == 1);
        Assert.Equal(HttpStatusCode.Created, (await client.RegisterAsync(almostFull.Id, "Reviewer")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.RegisterAsync(almostFull.Id, "Second Reviewer")).StatusCode);
    }

    [Fact]
    public async Task Sample_events_are_off_by_default()
    {
        using var factory = new TestAppFactory();

        var events = await factory.CreateClient().GetFromJsonAsync<List<EventDto>>("/api/events");

        Assert.Empty(events!);
    }
}
