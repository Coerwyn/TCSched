using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Configuration;
using TCSched.Api.Data;
using TCSched.Api.Domain;

namespace TCSched.Api.Endpoints;

/// <summary>
/// <paramref name="StartsAtLocal"/> is store-local wall time, e.g. "2026-11-07T18:00".
/// Format and Capacity default to the template's values when omitted.
/// </summary>
public sealed record CreateEventRequest(
    string? Name,
    string? GameTemplateId,
    string? Format,
    string? StartsAtLocal,
    int? Capacity);

public sealed record EventDto(
    Guid Id,
    string Name,
    string GameTemplateId,
    string GameName,
    string Format,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    int DurationMinutes,
    int Capacity,
    int RegisteredCount,
    int SeatsLeft,
    int MinPlayers,
    bool IsFull)
{
    public static EventDto From(Event e, string gameName) => new(
        e.Id, e.Name, e.GameTemplateId, gameName, e.Format,
        e.StartsAtUtc, e.EndsAtUtc, e.DurationMinutes,
        e.Capacity, e.RegisteredCount, e.Capacity - e.RegisteredCount, e.MinPlayers,
        e.RegisteredCount >= e.Capacity);
}

public static class EventEndpoints
{
    public static RouteGroupBuilder MapEventEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/events", CreateAsync);
        api.MapGet("/events", ListAsync);
        api.MapGet("/events/{id:guid}", GetAsync);
        return api;
    }

    private static async Task<IResult> CreateAsync(
        CreateEventRequest req, AppDbContext db, StoreClock clock, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var name = req.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > EventRules.MaxEventNameLength)
            errors["name"] = [$"Name is required and must be at most {EventRules.MaxEventNameLength} characters."];

        if (!clock.TryLocalToUtc(req.StartsAtLocal, out var startsAtUtc, out var timeError))
            errors["startsAtLocal"] = [timeError!];
        else if (startsAtUtc <= clock.UtcNow)
            errors["startsAtLocal"] = [string.Create(CultureInfo.InvariantCulture,
                $"Start time must be in the future. It's now {clock.LocalNow:ddd MMM d, h:mm tt} at the store ({clock.TimeZone.Id})."),
            ];

        if (string.IsNullOrWhiteSpace(req.GameTemplateId))
        {
            errors["gameTemplateId"] = ["Game is required."];
            return ApiProblems.Validation(errors);
        }

        var template = await db.GameTemplates.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == req.GameTemplateId, ct);
        if (template is null)
            return ApiProblems.BadRequest("UNKNOWN_TEMPLATE", $"Unknown game '{req.GameTemplateId}'.");

        // Everything below reads template properties only; no game-specific branches.
        var format = string.IsNullOrWhiteSpace(req.Format) ? template.DefaultFormat : req.Format.Trim();
        if (!template.Formats.Contains(format))
            errors["format"] = [$"Format must be one of: {string.Join(", ", template.Formats)}."];

        var capacity = req.Capacity ?? template.DefaultCapacity;
        var maxCapacity = Math.Min(template.MaxCapacity, EventRules.MaxCapacity);
        if (capacity < template.MinPlayers || capacity > maxCapacity)
            errors["capacity"] = [$"Capacity for {template.Name} must be between {template.MinPlayers} and {maxCapacity}."];

        if (errors.Count > 0)
            return ApiProblems.Validation(errors);

        var ev = new Event
        {
            Id = Guid.NewGuid(),
            Name = name!,
            GameTemplateId = template.Id,
            Format = format,
            StartsAtUtc = startsAtUtc,
            DurationMinutes = template.DefaultDurationMinutes,
            Capacity = capacity,
            MinPlayers = template.MinPlayers,
            CreatedAtUtc = clock.UtcNow,
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/events/{ev.Id}", EventDto.From(ev, template.Name));
    }

    /// <summary>Events starting in [from, to), ordered by start. Both bounds optional.</summary>
    private static async Task<IResult> ListAsync(
        DateTimeOffset? from, DateTimeOffset? to, AppDbContext db, CancellationToken ct)
    {
        if (from is not null && to is not null && from >= to)
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["from"] = ["'from' must be before 'to'."] });

        var query = db.Events.AsNoTracking().Include(e => e.GameTemplate).AsQueryable();
        if (from is not null) { var f = from.Value.UtcDateTime; query = query.Where(e => e.StartsAtUtc >= f); }
        if (to is not null) { var t = to.Value.UtcDateTime; query = query.Where(e => e.StartsAtUtc < t); }

        var events = await query.OrderBy(e => e.StartsAtUtc).ToListAsync(ct);
        return Results.Ok(events.Select(e => EventDto.From(e, e.GameTemplate!.Name)));
    }

    private static async Task<IResult> GetAsync(Guid id, AppDbContext db, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.GameTemplate)
            .SingleOrDefaultAsync(e => e.Id == id, ct);
        return ev is null
            ? ApiProblems.NotFound("Event")
            : Results.Ok(EventDto.From(ev, ev.GameTemplate!.Name));
    }
}
