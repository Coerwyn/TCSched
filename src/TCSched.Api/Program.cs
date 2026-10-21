using TCSched.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services
    .AddOptions<StoreOptions>()
    .Bind(builder.Configuration.GetSection(StoreOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Serves the built Vue app (copied into wwwroot by the Docker build).
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");
api.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Unknown non-API paths go to the SPA so client-side routes like /e/{id} work on refresh.
app.MapFallbackToFile("index.html");

app.Run();

// Lets WebApplicationFactory<Program> find the entry point in tests.
public partial class Program;
