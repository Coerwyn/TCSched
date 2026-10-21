using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TCSched.Api.Domain;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class EventEndpointTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CreateEventRequest Fnm(
        string? template = "mtg", string? format = null, string? startsAtLocal = "2027-01-15T18:00",
        int? capacity = null, string? name = "Friday Night Magic") =>
        new(name, template, format, startsAtLocal, capacity);

    [Fact]
    public async Task Create_uses_template_defaults_when_format_and_capacity_omitted()
    {
        var res = await _client.CreateEventAsync(Fnm());

        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var ev = (await res.Content.ReadFromJsonAsync<EventDto>())!;
        Assert.Equal($"/api/events/{ev.Id}", res.Headers.Location?.ToString());
        Assert.Equal("Magic: The Gathering", ev.GameName);
        Assert.Equal("Commander", ev.Format);
        Assert.Equal(16, ev.Capacity);
        Assert.Equal(4, ev.MinPlayers);
        Assert.Equal(240, ev.DurationMinutes);
        Assert.Equal(16, ev.SeatsLeft);
        Assert.False(ev.IsFull);
    }

    [Theory]
    // Store is America/Los_Angeles: PST (UTC-8) in January, PDT (UTC-7) in July.
    [InlineData("2027-01-15T18:00", "2027-01-16T02:00:00Z", "2027-01-16T06:00:00Z")]
    [InlineData("2027-07-15T18:00", "2027-07-16T01:00:00Z", "2027-07-16T05:00:00Z")]
    public async Task Start_time_is_store_local_and_converted_to_utc(string local, string expectedStart, string expectedEnd)
    {
        var ev = await _client.CreateEventOkAsync(Fnm(startsAtLocal: local));

        Assert.Equal(DateTime.Parse(expectedStart).ToUniversalTime(), ev.StartsAtUtc);
        Assert.Equal(DateTime.Parse(expectedEnd).ToUniversalTime(), ev.EndsAtUtc); // MTG: 240 minutes
    }

    [Fact]
    public async Task Template_limits_capacity_per_game()
    {
        // MTG allows 30, Lorcana's template caps at 24 — same code path, different data.
        Assert.Equal(30, (await _client.CreateEventOkAsync(Fnm(capacity: 30))).Capacity);

        var res = await _client.CreateEventAsync(Fnm(template: "lorcana", capacity: 25));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var (code, fields) = await res.ReadProblemAsync();
        Assert.Equal("VALIDATION", code);
        Assert.Equal(["capacity"], fields);
    }

    public static TheoryData<CreateEventRequest, string> InvalidRequests => new()
    {
        { Fnm(name: " "), "name" },
        { Fnm(name: new string('x', EventRules.MaxEventNameLength + 1)), "name" },
        { Fnm(capacity: 31), "capacity" },
        { Fnm(capacity: 3), "capacity" },                       // below MTG min players (4)
        { Fnm(format: "Vintage"), "format" },
        { Fnm(template: "pokemon", format: "Commander"), "format" }, // MTG format on a Pokémon event
        { Fnm(startsAtLocal: "2020-01-01T18:00"), "startsAtLocal" },  // in the past
        { Fnm(startsAtLocal: "next friday"), "startsAtLocal" },
        { Fnm(startsAtLocal: "2027-01-15T18:00:00-08:00"), "startsAtLocal" }, // offsets not accepted
        { Fnm(startsAtLocal: "2027-03-14T02:30"), "startsAtLocal" },  // skipped by DST in Los Angeles
        { Fnm(template: null), "gameTemplateId" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Invalid_create_returns_validation_problem_for_the_field(CreateEventRequest req, string field)
    {
        var res = await _client.CreateEventAsync(req);

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var (code, fields) = await res.ReadProblemAsync();
        Assert.Equal("VALIDATION", code);
        Assert.Contains(field, fields);
    }

    [Fact]
    public async Task Tests_run_on_the_fixed_clock_not_the_real_one()
    {
        // TestAppFactory.Now is 2026-06-01 12:00 PDT. That date is already past in real life, so
        // these results only hold if the API uses the injected clock.
        Assert.Equal(HttpStatusCode.Created, (await _client.CreateEventAsync(Fnm(startsAtLocal: "2026-06-01T13:00"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.CreateEventAsync(Fnm(startsAtLocal: "2026-06-01T11:00"))).StatusCode);
    }

    [Fact]
    public async Task Past_start_error_says_what_time_it_is_at_the_store()
    {
        var res = await _client.CreateEventAsync(Fnm(startsAtLocal: "2020-01-01T18:00"));

        var body = await res.Content.ReadAsStringAsync();
        // e.g. "It's now Wed Oct 21, 4:12 PM PDT at the store (America/Los_Angeles)."
        Assert.Matches(@"It's now \w{3} \w{3} \d{1,2}, \d{1,2}:\d{2} (AM|PM) P[DS]T at the store \(America/Los_Angeles\)\.", body);
    }

    [Fact]
    public async Task Unknown_template_is_rejected()
    {
        var res = await _client.CreateEventAsync(Fnm(template: "chess"));

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("UNKNOWN_TEMPLATE", (await res.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task Get_returns_event_or_404()
    {
        var created = await _client.CreateEventOkAsync(Fnm(template: "pokemon"));

        var ev = await _client.GetFromJsonAsync<EventDto>($"/api/events/{created.Id}");
        Assert.Equal(created, ev);

        var missing = await _client.GetAsync($"/api/events/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("NOT_FOUND", (await missing.ReadProblemAsync()).Code);
    }

    [Fact]
    public async Task List_filters_by_start_range_and_orders_by_start()
    {
        // Use a month no other test touches so results are predictable on the shared DB.
        var late = await _client.CreateEventOkAsync(Fnm(startsAtLocal: "2028-02-20T18:00", name: "Late"));
        var early = await _client.CreateEventOkAsync(Fnm(startsAtLocal: "2028-02-05T18:00", name: "Early"));
        await _client.CreateEventOkAsync(Fnm(startsAtLocal: "2028-03-05T18:00", name: "Next month"));

        var feb = await _client.GetFromJsonAsync<List<EventDto>>(
            "/api/events?from=2028-02-01T00:00:00-08:00&to=2028-03-01T00:00:00-08:00");

        Assert.Equal([early.Id, late.Id], feb!.Select(e => e.Id));
    }

    [Fact]
    public async Task List_rejects_inverted_range()
    {
        var res = await _client.GetAsync("/api/events?from=2028-03-01T00:00:00Z&to=2028-02-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}

public class FourthGameTests
{
    [Fact]
    public async Task An_event_for_a_game_added_only_as_data_works_end_to_end()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tcsched-templates-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new[]
        {
            new GameTemplate
            {
                Id = "fab", Name = "Flesh and Blood", Formats = ["Blitz", "Classic Constructed"],
                DefaultFormat = "Blitz", DefaultDurationMinutes = 150,
                DefaultCapacity = 8, MaxCapacity = 12, MinPlayers = 2,
            },
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var factory = new TestAppFactory { TemplatesPath = path };
        var client = factory.CreateClient();

        var ev = await client.CreateEventOkAsync(new("Armory", "fab", null, "2027-01-15T18:00", null));

        Assert.Equal(("Blitz", 8, 2, 150), (ev.Format, ev.Capacity, ev.MinPlayers, ev.DurationMinutes));
        var tooBig = await client.CreateEventAsync(new("Armory", "fab", null, "2027-01-15T18:00", 13));
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
    }
}
