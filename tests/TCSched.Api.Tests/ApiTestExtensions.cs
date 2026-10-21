using System.Net.Http.Json;
using System.Text.Json;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public static class ApiTestExtensions
{
    public static Task<HttpResponseMessage> CreateEventAsync(this HttpClient client, CreateEventRequest req) =>
        client.PostAsJsonAsync("/api/events", req);

    public static async Task<EventDto> CreateEventOkAsync(this HttpClient client, CreateEventRequest req)
    {
        var res = await client.CreateEventAsync(req);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Create failed {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
        return (await res.Content.ReadFromJsonAsync<EventDto>())!;
    }

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, Guid eventId, string? name) =>
        client.PostAsJsonAsync($"/api/events/{eventId}/registrations", new RegisterRequest(name));

    /// <summary>Reads a ProblemDetails body: its <c>code</c> and the field names in <c>errors</c>.</summary>
    public static async Task<(string? Code, string[] Fields)> ReadProblemAsync(this HttpResponseMessage res)
    {
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        var fields = root.TryGetProperty("errors", out var e)
            ? e.EnumerateObject().Select(p => p.Name).ToArray()
            : [];
        return (code, fields);
    }
}
