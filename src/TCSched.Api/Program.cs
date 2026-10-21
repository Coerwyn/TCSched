using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using TCSched.Api.Configuration;
using TCSched.Api.Data;
using TCSched.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddOptions<StoreOptions>()
    .Bind(builder.Configuration.GetSection(StoreOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _), "Store:TimeZone is not a known time zone id.")
    .Validate(o => o.HasValidPublicBaseUrl(), "Store:PublicBaseUrl must be blank or an absolute http(s) URL, e.g. http://192.168.1.20:8080.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<StoreClock>();

// Read the connection string when the context is resolved, not at startup, so tests can override it.
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<IConfiguration>().GetConnectionString("Default")));

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services);

// Trust X-Forwarded-Host/Proto from a local proxy (the Vite dev server) so request-based
// links use the address the player actually opened. Only loopback proxies are trusted by default.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto,
});

app.UseExceptionHandler();
app.UseStatusCodePages();

// Serves the built Vue app (copied into wwwroot by the Docker build).
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));
api.MapStoreEndpoints();
api.MapTemplateEndpoints();
api.MapEventEndpoints();
api.MapRegistrationEndpoints();
api.MapIcsEndpoints();

// Unknown /api paths get a JSON 404 instead of the SPA's index.html.
app.MapFallback("/api/{**rest}", () => ApiProblems.NotFound("API endpoint"));

// Unknown non-API paths go to the SPA so client-side routes like /e/{id} work on refresh.
app.MapFallbackToFile("index.html");

app.Run();

// Lets WebApplicationFactory<Program> find the entry point in tests.
public partial class Program;
