namespace TCSched.Api.Domain;

public static class EventRules
{
    /// <summary>Hard upper limit on any event's capacity. Templates may set a lower max.</summary>
    public const int MaxCapacity = 30;

    public const int MaxEventNameLength = 100;
    public const int MaxPlayerNameLength = 60;
}
