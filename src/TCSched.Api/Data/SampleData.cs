using Microsoft.EntityFrameworkCore;
using TCSched.Api.Configuration;
using TCSched.Api.Domain;

namespace TCSched.Api.Data;

/// <summary>
/// Demo events for the coming week, so a reviewer sees a populated calendar and can watch an
/// event fill up with a single scan. Opt-in (<c>Seed:SampleEvents</c>), and it only runs when
/// there are no events at all, so it never adds to real data.
/// </summary>
public static class SampleData
{
    private sealed record Sample(
        string Name, string TemplateId, string Format, DayOfWeek Day, TimeOnly Time, int Capacity, int Registered);

    private static readonly Sample[] Samples =
    [
        new("Friday Night Magic", "mtg", "Commander", DayOfWeek.Friday, new(18, 0), 16, 6),
        new("Pokémon League Challenge", "pokemon", "Standard", DayOfWeek.Saturday, new(12, 0), 20, 9),
        new("Lorcana Sealed (one seat left!)", "lorcana", "Sealed", DayOfWeek.Sunday, new(13, 0), 4, 3),
        new("Modern Monday", "mtg", "Modern", DayOfWeek.Monday, new(18, 30), 8, 1),
        new("Draft Night", "mtg", "Draft", DayOfWeek.Wednesday, new(18, 0), 8, 8),
    ];

    private static readonly string[] Players =
    [
        "Alex Rivera", "Bo Chen", "Cy Diaz", "Dana Evans", "Eli Foster", "Fay Garcia",
        "Gus Hall", "Hana Ito", "Ivy Jones",
    ];

    public static async Task<int> SeedAsync(AppDbContext db, StoreClock clock, CancellationToken ct = default)
    {
        if (await db.Events.AnyAsync(ct))
            return 0;

        var templates = await db.GameTemplates.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var today = clock.LocalToday;
        var added = 0;

        foreach (var s in Samples)
        {
            // Skip samples whose game isn't configured (e.g. a custom templates.json).
            if (!templates.TryGetValue(s.TemplateId, out var t) || !t.Formats.Contains(s.Format))
                continue;

            var date = NextOccurrence(today, s.Day);
            var ev = new Event
            {
                Id = Guid.NewGuid(),
                Name = s.Name,
                GameTemplateId = t.Id,
                Format = s.Format,
                StartsAtUtc = clock.LocalToUtc(date.ToDateTime(s.Time)),
                DurationMinutes = t.DefaultDurationMinutes,
                Capacity = Math.Clamp(s.Capacity, t.MinPlayers, Math.Min(t.MaxCapacity, EventRules.MaxCapacity)),
                MinPlayers = t.MinPlayers,
                CreatedAtUtc = clock.UtcNow,
            };
            ev.RegisteredCount = Math.Min(s.Registered, ev.Capacity);
            ev.Registrations = Players.Take(ev.RegisteredCount).Select((name, i) => new Registration
            {
                Id = Guid.NewGuid(),
                EventId = ev.Id,
                DisplayName = name,
                NormalizedName = NameNormalizer.Normalize(name),
                RegisteredAtUtc = clock.UtcNow.AddMinutes(-(ev.RegisteredCount - i) * 7),
            }).ToList();

            db.Events.Add(ev);
            added++;
        }

        await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>The next date after <paramref name="today"/> that falls on <paramref name="day"/> (1–7 days ahead).</summary>
    private static DateOnly NextOccurrence(DateOnly today, DayOfWeek day)
    {
        var ahead = ((int)day - (int)today.DayOfWeek + 7) % 7;
        return today.AddDays(ahead == 0 ? 7 : ahead);
    }
}
