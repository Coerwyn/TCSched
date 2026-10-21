using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Domain;

namespace TCSched.Api.Data;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Path to the template seed file. Relative paths resolve from the app's base directory.</summary>
    public string TemplatesPath { get; set; } = Path.Combine("Templates", "templates.json");
}

public static class DbInitializer
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Applies migrations, then seeds templates. Runs once at startup.</summary>
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbInitializer));

        await db.Database.MigrateAsync(ct);

        var seed = config.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();
        var path = Path.IsPathRooted(seed.TemplatesPath)
            ? seed.TemplatesPath
            : Path.Combine(AppContext.BaseDirectory, seed.TemplatesPath);

        var added = await SeedTemplatesAsync(db, LoadTemplates(path), ct);
        logger.LogInformation("Template seed: {Added} added from {Path}", added, path);
    }

    /// <summary>Reads and validates the seed file. Any invalid template stops startup.</summary>
    public static List<GameTemplate> LoadTemplates(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Template seed file not found: {path}");

        var templates = JsonSerializer.Deserialize<List<GameTemplate>>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException($"Template seed file is empty: {path}");

        var problems = templates
            .Select(t => (t.Id, Errors: GameTemplateValidator.Validate(t)))
            .Where(x => x.Errors.Count > 0)
            .Select(x => $"'{x.Id}': {string.Join(" ", x.Errors.SelectMany(e => e.Value))}")
            .ToList();

        var duplicateIds = templates.GroupBy(t => t.Id).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateIds.Count > 0)
            problems.Add($"Duplicate template ids: {string.Join(", ", duplicateIds)}");

        if (problems.Count > 0)
            throw new InvalidOperationException(
                $"Invalid template seed file {path}:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");

        return templates;
    }

    /// <summary>
    /// Insert-only: templates already in the database are never overwritten, so changes
    /// made there (e.g. by a future admin UI) survive restarts. Returns the number added.
    /// </summary>
    public static async Task<int> SeedTemplatesAsync(AppDbContext db, IEnumerable<GameTemplate> templates, CancellationToken ct = default)
    {
        var existing = await db.GameTemplates.Select(t => t.Id).ToListAsync(ct);
        var missing = templates.Where(t => !existing.Contains(t.Id)).ToList();
        db.GameTemplates.AddRange(missing);
        await db.SaveChangesAsync(ct);
        return missing.Count;
    }
}
