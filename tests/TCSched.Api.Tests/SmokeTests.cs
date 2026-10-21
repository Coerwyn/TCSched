using System.Net;
using System.Net.Http.Json;
using TCSched.Api.Endpoints;

namespace TCSched.Api.Tests;

public class SmokeTests(TestAppFactory factory) : IClassFixture<TestAppFactory>
{
    [Fact]
    public async Task Health_endpoint_returns_ok()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Store_endpoint_returns_configured_store()
    {
        var store = await factory.CreateClient().GetFromJsonAsync<StoreDto>("/api/store");

        Assert.NotNull(store);
        Assert.Equal("America/Chicago", store.TimeZone);
        Assert.False(string.IsNullOrWhiteSpace(store.Address));
    }
}
