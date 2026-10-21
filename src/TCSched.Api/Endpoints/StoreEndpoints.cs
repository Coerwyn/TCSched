using Microsoft.Extensions.Options;
using TCSched.Api.Configuration;

namespace TCSched.Api.Endpoints;

public sealed record StoreDto(string Name, string Address, string TimeZone);

public static class StoreEndpoints
{
    public static RouteGroupBuilder MapStoreEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/store", (IOptions<StoreOptions> store) =>
            new StoreDto(store.Value.Name, store.Value.Address, store.Value.TimeZone));
        return api;
    }
}
