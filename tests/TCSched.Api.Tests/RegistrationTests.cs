using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Domain;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class RegistrationTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    /// <summary>MTG's minimum is 4 players, so capacity 4 is the smallest MTG event.</summary>
    private Task<EventDto> NewEventAsync(int capacity = 4) =>
        _client.CreateEventOkAsync(new("FNM", "mtg", null, "2027-01-15T18:00", capacity));

    private async Task<(int Count, int Rows)> SeatsInDbAsync(Guid eventId)
    {
        (int, int) result = default;
        await factory.WithDbAsync(async db =>
        {
            var count = await db.Events.Where(e => e.Id == eventId).Select(e => e.RegisteredCount).SingleAsync();
            var rows = await db.Registrations.CountAsync(r => r.EventId == eventId);
            result = (count, rows);
        });
        return result;
    }

    [Fact]
    public async Task Register_takes_a_seat()
    {
        var ev = await NewEventAsync();

        var res = await _client.RegisterAsync(ev.Id, "  Alex   Smith ");

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var body = (await res.Content.ReadFromJsonAsync<RegistrationResultDto>())!;
        Assert.Equal("Alex Smith", body.DisplayName);
        Assert.Equal((1, 4, 3), (body.RegisteredCount, body.Capacity, body.SeatsLeft));

        var list = await _client.GetFromJsonAsync<List<RegistrationDto>>($"/api/events/{ev.Id}/registrations");
        Assert.Equal(["Alex Smith"], list!.Select(r => r.DisplayName));
        Assert.Equal(1, (await _client.GetFromJsonAsync<EventDto>($"/api/events/{ev.Id}"))!.RegisteredCount);
    }

    [Fact]
    public async Task Event_fills_up_then_rejects_with_event_full()
    {
        var ev = await NewEventAsync(capacity: 4);
        foreach (var name in new[] { "A", "B", "C", "D" })
            Assert.Equal(HttpStatusCode.Created, (await _client.RegisterAsync(ev.Id, name)).StatusCode);

        var res = await _client.RegisterAsync(ev.Id, "E");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("EVENT_FULL", (await res.ReadProblemAsync()).Code);
        Assert.Equal((4, 4), await SeatsInDbAsync(ev.Id));
        Assert.True((await _client.GetFromJsonAsync<EventDto>($"/api/events/{ev.Id}"))!.IsFull);
    }

    [Fact]
    public async Task Duplicate_name_is_rejected_and_does_not_use_a_seat()
    {
        var ev = await NewEventAsync();
        await _client.RegisterAsync(ev.Id, "alex");

        var res = await _client.RegisterAsync(ev.Id, " ALEX ");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("DUPLICATE_REGISTRATION", (await res.ReadProblemAsync()).Code);
        Assert.Equal((1, 1), await SeatsInDbAsync(ev.Id));
    }

    [Fact]
    public async Task Same_name_on_a_different_event_is_fine()
    {
        var a = await NewEventAsync();
        var b = await NewEventAsync();

        Assert.Equal(HttpStatusCode.Created, (await _client.RegisterAsync(a.Id, "Alex")).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.RegisterAsync(b.Id, "Alex")).StatusCode);
    }

    [Fact]
    public async Task Registered_player_on_a_full_event_is_told_they_are_already_registered()
    {
        var ev = await NewEventAsync(capacity: 4);
        foreach (var name in new[] { "A", "B", "C", "D" })
            await _client.RegisterAsync(ev.Id, name);

        var res = await _client.RegisterAsync(ev.Id, "a");

        Assert.Equal("DUPLICATE_REGISTRATION", (await res.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task Started_event_rejects_registration()
    {
        // The API won't create past events, so insert one directly.
        var id = Guid.NewGuid();
        await factory.WithDbAsync(async db =>
        {
            db.Events.Add(new Event
            {
                Id = id, Name = "Earlier today", GameTemplateId = "mtg", Format = "Commander",
                StartsAtUtc = TestAppFactory.Now.UtcDateTime.AddMinutes(-5), DurationMinutes = 240,
                Capacity = 8, MinPlayers = 4, CreatedAtUtc = TestAppFactory.Now.UtcDateTime.AddDays(-1),
            });
            await db.SaveChangesAsync();
        });

        var res = await _client.RegisterAsync(id, "Late Larry");

        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
        Assert.Equal("EVENT_STARTED", (await res.ReadProblemAsync()).Code);
        Assert.Equal((0, 0), await SeatsInDbAsync(id));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x123456789x123456789x123456789x123456789x123456789x123456789x")] // 61 chars
    public async Task Invalid_name_is_rejected(string? name)
    {
        var ev = await NewEventAsync();

        var res = await _client.RegisterAsync(ev.Id, name);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var (code, fields) = await res.ReadProblemAsync();
        Assert.Equal(("VALIDATION", "name"), (code, Assert.Single(fields)));
    }

    [Fact]
    public async Task Unknown_event_returns_404()
    {
        var res = await _client.RegisterAsync(Guid.NewGuid(), "Alex");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);

        var list = await _client.GetAsync($"/api/events/{Guid.NewGuid()}/registrations");
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }
}

/// <summary>
/// Many registrations at once against one event. Each test gets its own app and database so
/// nothing else is running at the same time.
/// </summary>
public class LastSeatRaceTests
{
    private static async Task<(HttpStatusCode Status, string? Code)[]> RegisterConcurrentlyAsync(
        HttpClient client, Guid eventId, IEnumerable<string> names)
    {
        using var start = new ManualResetEventSlim(false);
        var tasks = names.Select(name => Task.Run(async () =>
        {
            start.Wait(); // release every request together
            var res = await client.RegisterAsync(eventId, name);
            return (res.StatusCode, res.IsSuccessStatusCode ? null : (await res.ReadProblemAsync()).Code);
        })).ToList();
        start.Set();
        return await Task.WhenAll(tasks);
    }

    private static async Task<(int Count, int Rows)> SeatsInDbAsync(TestAppFactory factory, Guid eventId)
    {
        (int, int) result = default;
        await factory.WithDbAsync(async db =>
        {
            result = (
                await db.Events.Where(e => e.Id == eventId).Select(e => e.RegisteredCount).SingleAsync(),
                await db.Registrations.CountAsync(r => r.EventId == eventId));
        });
        return result;
    }

    [Theory]
    [InlineData(4, 50)]
    [InlineData(5, 50)]
    [InlineData(30, 100)]
    public async Task Concurrent_registrations_never_exceed_capacity(int capacity, int requests)
    {
        using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var ev = await client.CreateEventOkAsync(new("Prerelease", "mtg", null, "2027-01-15T18:00", capacity));

        var results = await RegisterConcurrentlyAsync(client, ev.Id, Enumerable.Range(1, requests).Select(i => $"Player {i}"));

        Assert.Equal(capacity, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(requests - capacity, results.Count(r => r.Code == "EVENT_FULL"));
        Assert.Equal((capacity, capacity), await SeatsInDbAsync(factory, ev.Id));
    }

    [Fact]
    public async Task Concurrent_duplicates_take_one_seat_and_release_the_rest()
    {
        using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var ev = await client.CreateEventOkAsync(new("Prerelease", "mtg", null, "2027-01-15T18:00", 4));

        // The same player mashing "Register" 20 times while three others also join.
        var names = Enumerable.Repeat("Alex", 20).Concat(["Bo", "Cy", "Di"]);
        var results = await RegisterConcurrentlyAsync(client, ev.Id, names);

        Assert.Equal(4, results.Count(r => r.Status == HttpStatusCode.Created));
        Assert.Equal(19, results.Count(r => r.Code == "DUPLICATE_REGISTRATION"));
        Assert.Equal((4, 4), await SeatsInDbAsync(factory, ev.Id));
    }
}
