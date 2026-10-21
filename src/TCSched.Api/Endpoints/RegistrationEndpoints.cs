using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Configuration;
using TCSched.Api.Data;
using TCSched.Api.Domain;

namespace TCSched.Api.Endpoints;

public sealed record RegisterRequest(string? Name);

public sealed record RegistrationResultDto(
    Guid RegistrationId,
    Guid EventId,
    string DisplayName,
    int RegisteredCount,
    int Capacity,
    int SeatsLeft);

public sealed record RegistrationDto(Guid Id, string DisplayName, DateTime RegisteredAtUtc);

public static class RegistrationEndpoints
{
    public static RouteGroupBuilder MapRegistrationEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/events/{id:guid}/registrations", RegisterAsync);
        api.MapGet("/events/{id:guid}/registrations", ListAsync);
        return api;
    }

    /// <summary>
    /// Registers a player. Capacity is enforced by the database, not by reading a count first:
    /// <list type="number">
    /// <item>One conditional UPDATE claims a seat only if one is free and the event hasn't started.
    ///   The check and the increment happen in one statement, so two requests can't both take the last seat.</item>
    /// <item>The registration is inserted in the same transaction. If the unique (event, name) index
    ///   rejects it, the transaction rolls back and the seat is released.</item>
    /// </list>
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        Guid id, RegisterRequest req, AppDbContext db, StoreClock clock, CancellationToken ct)
    {
        var displayName = NameNormalizer.Clean(req.Name ?? "");
        if (displayName.Length == 0 || displayName.Length > EventRules.MaxPlayerNameLength)
            return ApiProblems.Validation(new Dictionary<string, string[]>
            {
                ["name"] = [$"Name is required and must be at most {EventRules.MaxPlayerNameLength} characters."],
            });
        var normalizedName = NameNormalizer.Normalize(displayName);
        var now = clock.UtcNow;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Step 1: atomic seat claim.
        var claimed = await db.Events
            .Where(e => e.Id == id && e.RegisteredCount < e.Capacity && e.StartsAtUtc > now)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.RegisteredCount, e => e.RegisteredCount + 1), ct);

        if (claimed == 0)
        {
            await tx.RollbackAsync(ct);
            return await ExplainRejectionAsync(db, id, normalizedName, now, ct);
        }

        // Step 2: record who took the seat.
        var registration = new Registration
        {
            Id = Guid.NewGuid(),
            EventId = id,
            DisplayName = displayName,
            NormalizedName = normalizedName,
            RegisteredAtUtc = now,
        };
        db.Registrations.Add(registration);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            await tx.RollbackAsync(ct); // gives the seat back
            return Duplicate(displayName);
        }

        var counts = await db.Events.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { e.RegisteredCount, e.Capacity }).SingleAsync(ct);
        await tx.CommitAsync(ct);

        return Results.Created($"/api/events/{id}/registrations/{registration.Id}", new RegistrationResultDto(
            registration.Id, id, displayName, counts.RegisteredCount, counts.Capacity,
            counts.Capacity - counts.RegisteredCount));
    }

    /// <summary>
    /// Runs only after the seat claim was refused, to pick the clearest message. It doesn't
    /// decide whether to accept the registration; the UPDATE already decided that.
    /// </summary>
    private static async Task<IResult> ExplainRejectionAsync(
        AppDbContext db, Guid id, string normalizedName, DateTime now, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().Where(e => e.Id == id)
            .Select(e => new { e.StartsAtUtc, e.Capacity }).SingleOrDefaultAsync(ct);
        if (ev is null)
            return ApiProblems.NotFound("Event");

        // "You're already registered" is more useful than "it's full" for someone re-scanning.
        var existing = await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == id && r.NormalizedName == normalizedName)
            .Select(r => r.DisplayName).FirstOrDefaultAsync(ct);
        if (existing is not null)
            return Duplicate(existing);

        if (ev.StartsAtUtc <= now)
            return ApiProblems.Conflict("EVENT_STARTED", "Registration is closed.",
                "This event has already started.");

        return ApiProblems.Conflict("EVENT_FULL", "This event is full.",
            $"All {ev.Capacity} seats are taken.");
    }

    private static IResult Duplicate(string displayName) =>
        ApiProblems.Conflict("DUPLICATE_REGISTRATION", "Already registered.",
            $"'{displayName}' is already registered for this event. If you're a different player, add an initial (e.g. \"Alex P.\").");

    // SQLITE_CONSTRAINT_UNIQUE (extended result code 2067).
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 };

    private static async Task<IResult> ListAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Events.AnyAsync(e => e.Id == id, ct))
            return ApiProblems.NotFound("Event");

        var registrations = await db.Registrations.AsNoTracking()
            .Where(r => r.EventId == id)
            .OrderBy(r => r.RegisteredAtUtc)
            .Select(r => new RegistrationDto(r.Id, r.DisplayName, r.RegisteredAtUtc))
            .ToListAsync(ct);
        return Results.Ok(registrations);
    }
}
