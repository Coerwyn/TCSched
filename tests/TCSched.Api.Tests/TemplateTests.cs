using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Data;
using TCSched.Api.Domain;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class TemplateEndpointTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task Lists_the_three_seeded_games()
    {
        var templates = await factory.CreateClient().GetFromJsonAsync<List<GameTemplateDto>>("/api/templates");

        Assert.NotNull(templates);
        Assert.Equal(["lorcana", "mtg", "pokemon"], templates.Select(t => t.Id).Order());

        var mtg = templates.Single(t => t.Id == "mtg");
        Assert.Equal("Magic: The Gathering", mtg.Name);
        Assert.Contains("Commander", mtg.Formats);
        Assert.Equal(30, mtg.MaxCapacity);
    }
}

public class TemplateSeedingTests
{
    private static GameTemplate Valid(string id = "fab") => new()
    {
        Id = id,
        Name = "Flesh and Blood",
        Formats = ["Classic Constructed", "Blitz"],
        DefaultFormat = "Blitz",
        DefaultDurationMinutes = 180,
        DefaultCapacity = 8,
        MaxCapacity = 16,
        MinPlayers = 2,
    };

    private static string WriteSeedFile(params GameTemplate[] templates)
    {
        var path = Path.Combine(Path.GetTempPath(), $"tcsched-templates-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(templates, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return path;
    }

    [Fact]
    public async Task A_fourth_game_from_the_seed_file_is_served_without_code_changes()
    {
        using var factory = new TestAppFactory { TemplatesPath = WriteSeedFile(Valid()) };

        var templates = await factory.CreateClient().GetFromJsonAsync<List<GameTemplateDto>>("/api/templates");

        var fab = Assert.Single(templates!);
        Assert.Equal("fab", fab.Id);
        Assert.Equal(["Classic Constructed", "Blitz"], fab.Formats);
    }

    [Fact]
    public void Invalid_seed_file_stops_startup()
    {
        var bad = Valid();
        bad.MaxCapacity = 31;
        using var factory = new TestAppFactory { TemplatesPath = WriteSeedFile(bad) };

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Max capacity cannot exceed 30", ex.ToString());
    }

    [Fact]
    public async Task Seeding_is_insert_only_and_never_overwrites_existing_templates()
    {
        using var factory = new TestAppFactory { TemplatesPath = WriteSeedFile(Valid()) };
        factory.CreateClient(); // starts the app, which runs the first seed

        await factory.WithDbAsync(async db =>
        {
            var fab = await db.GameTemplates.SingleAsync(t => t.Id == "fab");
            fab.DefaultCapacity = 12; // simulate an edit made outside the seed file
            await db.SaveChangesAsync();

            var added = await DbInitializer.SeedTemplatesAsync(db, [Valid(), Valid("other")]);

            Assert.Equal(1, added);
        });

        await factory.WithDbAsync(async db =>
        {
            Assert.Equal(12, (await db.GameTemplates.SingleAsync(t => t.Id == "fab")).DefaultCapacity);
            Assert.Equal(2, await db.GameTemplates.CountAsync());
        });
    }
}

public class GameTemplateValidatorTests
{
    private static GameTemplate Valid() => new()
    {
        Id = "mtg",
        Name = "Magic: The Gathering",
        Formats = ["Commander", "Draft"],
        DefaultFormat = "Commander",
        DefaultDurationMinutes = 240,
        DefaultCapacity = 16,
        MaxCapacity = 30,
        MinPlayers = 4,
    };

    [Fact]
    public void Valid_template_has_no_errors() => Assert.Empty(GameTemplateValidator.Validate(Valid()));

    [Fact]
    public void The_shipped_seed_file_is_valid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Templates", "templates.json");
        Assert.Equal(3, DbInitializer.LoadTemplates(path).Count);
    }

    public static TheoryData<string, Action<GameTemplate>> InvalidCases => new()
    {
        { nameof(GameTemplate.Id), t => t.Id = "Bad Slug!" },
        { nameof(GameTemplate.Name), t => t.Name = " " },
        { nameof(GameTemplate.Formats), t => t.Formats = [] },
        { nameof(GameTemplate.Formats), t => t.Formats = ["Draft", "draft"] },
        { nameof(GameTemplate.DefaultFormat), t => t.DefaultFormat = "Vintage" },
        { nameof(GameTemplate.DefaultDurationMinutes), t => t.DefaultDurationMinutes = 5 },
        { nameof(GameTemplate.MinPlayers), t => t.MinPlayers = 0 },
        { nameof(GameTemplate.DefaultCapacity), t => t.MinPlayers = 20 },
        { nameof(GameTemplate.MaxCapacity), t => t.MaxCapacity = 10 },
        { nameof(GameTemplate.MaxCapacity), t => { t.DefaultCapacity = 31; t.MaxCapacity = 31; } },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_template_reports_the_field(string field, Action<GameTemplate> breakIt)
    {
        var t = Valid();
        breakIt(t);

        Assert.Contains(field, GameTemplateValidator.Validate(t).Keys);
    }
}
