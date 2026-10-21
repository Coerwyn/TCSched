using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TCSched.Api.Data;

namespace TCSched.Api.Tests;

/// <summary>
/// Runs the real API against its own temp-file SQLite database. We use a real SQLite file
/// instead of EF's in-memory provider because that provider has no transactions or
/// constraints, and those are what these tests need to check.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// The app's "now" in every test: noon Pacific on 2026-06-01. Fixed so tests that schedule
    /// events on literal dates (2027-01-15, ...) never expire, and so "started" and "upcoming"
    /// checks compare against the same clock the API uses.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 6, 1, 19, 0, 0, TimeSpan.Zero);

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"tcsched-test-{Guid.NewGuid():N}.db");

    /// <summary>Optional override for the template seed file (absolute path).</summary>
    public string? TemplatesPath { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_dbPath}");
        if (TemplatesPath is not null)
            builder.UseSetting("Seed:TemplatesPath", TemplatesPath);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        });
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Runs <paramref name="action"/> with a fresh DbContext from the app's container.</summary>
    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
            File.Delete(_dbPath + suffix);
    }
}
