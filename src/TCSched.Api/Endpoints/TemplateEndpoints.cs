using Microsoft.EntityFrameworkCore;
using TCSched.Api.Data;
using TCSched.Api.Domain;

namespace TCSched.Api.Endpoints;

public sealed record GameTemplateDto(
    string Id,
    string Name,
    IReadOnlyList<string> Formats,
    string DefaultFormat,
    int DefaultDurationMinutes,
    int DefaultCapacity,
    int MaxCapacity,
    int MinPlayers)
{
    public static GameTemplateDto From(GameTemplate t) => new(
        t.Id, t.Name, t.Formats, t.DefaultFormat, t.DefaultDurationMinutes,
        t.DefaultCapacity, t.MaxCapacity, t.MinPlayers);
}

public static class TemplateEndpoints
{
    public static RouteGroupBuilder MapTemplateEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/templates", async (AppDbContext db, CancellationToken ct) =>
        {
            var templates = await db.GameTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);
            return templates.Select(GameTemplateDto.From);
        });

        return api;
    }
}
