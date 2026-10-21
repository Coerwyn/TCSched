using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Domain;

namespace TCSched.Api.Tests;

public class NameNormalizerTests
{
    [Theory]
    [InlineData("alex", "ALEX")]
    [InlineData("  Alex   Smith ", "ALEX SMITH")]
    [InlineData("alex\tsmith", "ALEX SMITH")]
    public void Normalizes_case_and_whitespace(string input, string expected) =>
        Assert.Equal(expected, NameNormalizer.Normalize(input));

    [Fact]
    public void Composed_and_decomposed_accents_collide() =>
        Assert.Equal(NameNormalizer.Normalize("José"), NameNormalizer.Normalize("José"));
}

public class DatabaseConstraintTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    private static Event NewEvent(int capacity) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Friday Night Magic",
        GameTemplateId = "mtg",
        Format = "Commander",
        StartsAtUtc = DateTime.UtcNow.AddDays(1),
        DurationMinutes = 240,
        Capacity = capacity,
        CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task Database_refuses_registered_count_above_capacity()
    {
        factory.CreateClient();
        var ev = NewEvent(capacity: 2);

        await factory.WithDbAsync(async db =>
        {
            db.Events.Add(ev);
            await db.SaveChangesAsync();

            // Bypass the app's checks entirely: the CHECK constraint is the final guard.
            var ex = await Assert.ThrowsAsync<SqliteException>(() => db.Events
                .Where(e => e.Id == ev.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.RegisteredCount, 3)));
            Assert.Contains("CK_Events_RegisteredCount", ex.Message);
        });
    }

    [Fact]
    public async Task Database_refuses_capacity_above_30()
    {
        factory.CreateClient();

        await factory.WithDbAsync(async db =>
        {
            db.Events.Add(NewEvent(capacity: 31));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("CK_Events_Capacity", ex.InnerException?.Message);
        });
    }

    [Fact]
    public async Task Database_refuses_duplicate_normalized_name_for_same_event()
    {
        factory.CreateClient();
        var ev = NewEvent(capacity: 4);

        await factory.WithDbAsync(async db =>
        {
            db.Events.Add(ev);
            await db.SaveChangesAsync();

            Registration Reg(string name) => new()
            {
                Id = Guid.NewGuid(), EventId = ev.Id, DisplayName = name,
                NormalizedName = NameNormalizer.Normalize(name), RegisteredAtUtc = DateTime.UtcNow,
            };
            db.Registrations.AddRange(Reg("alex"), Reg(" ALEX "));
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("UNIQUE constraint failed", ex.InnerException?.Message);
        });
    }

    [Fact]
    public async Task Dates_round_trip_as_utc()
    {
        factory.CreateClient();
        var ev = NewEvent(capacity: 4);

        await factory.WithDbAsync(async db => { db.Events.Add(ev); await db.SaveChangesAsync(); });
        await factory.WithDbAsync(async db =>
        {
            var loaded = await db.Events.AsNoTracking().SingleAsync(e => e.Id == ev.Id);
            Assert.Equal(DateTimeKind.Utc, loaded.StartsAtUtc.Kind);
            Assert.Equal(ev.StartsAtUtc, loaded.StartsAtUtc);
        });
    }
}
