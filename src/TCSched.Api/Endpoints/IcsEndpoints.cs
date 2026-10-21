using System.Text;
using System.Text.RegularExpressions;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCSched.Api.Configuration;
using TCSched.Api.Data;
using TCSched.Api.Domain;

namespace TCSched.Api.Endpoints;

public static partial class IcsEndpoints
{
    public static RouteGroupBuilder MapIcsEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/events/{id:guid}/calendar.ics", DownloadAsync);
        return api;
    }

    private static async Task<IResult> DownloadAsync(
        Guid id, HttpRequest request, AppDbContext db, IOptions<StoreOptions> store,
        StoreClock clock, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().Include(e => e.GameTemplate)
            .SingleOrDefaultAsync(e => e.Id == id, ct);
        if (ev is null)
            return ApiProblems.NotFound("Event");

        var baseUrl = store.Value.PublicBaseUrl?.TrimEnd('/') ?? $"{request.Scheme}://{request.Host}";
        var ics = Build(ev, ev.GameTemplate!.Name, store.Value, $"{baseUrl}/e/{ev.Id}", clock.UtcNow);

        var local = TimeZoneInfo.ConvertTimeFromUtc(ev.StartsAtUtc, clock.TimeZone);
        var fileName = $"{Slug(ev.Name)}-{local:yyyy-MM-dd}.ics";
        return Results.File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", fileName);
    }

    /// <summary>
    /// One VEVENT with UTC start/end (the "Z" form). Google Calendar and Outlook both import
    /// UTC times reliably, while a TZID would need a VTIMEZONE block they don't always honor.
    /// </summary>
    public static string Build(Event ev, string gameName, StoreOptions store, string registrationUrl, DateTime nowUtc)
    {
        var vevent = new CalendarEvent
        {
            Uid = $"{ev.Id}@tcsched",
            DtStamp = new CalDateTime(nowUtc),
            DtStart = new CalDateTime(ev.StartsAtUtc),
            DtEnd = new CalDateTime(ev.EndsAtUtc),
            Summary = ev.Name,
            Location = $"{store.Name}, {store.Address}",
            Description = $"{gameName} · {ev.Format}\nRegister: {registrationUrl}",
            Url = new Uri(registrationUrl),
        };

        var calendar = new Calendar { ProductId = "-//TCSched//Event Calendar//EN", Method = "PUBLISH" };
        calendar.Events.Add(vevent);
        return new CalendarSerializer().SerializeToString(calendar)!;
    }

    private static string Slug(string name)
    {
        var slug = NonAlphanumeric().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return slug.Length == 0 ? "event" : slug[..Math.Min(slug.Length, 60)];
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
