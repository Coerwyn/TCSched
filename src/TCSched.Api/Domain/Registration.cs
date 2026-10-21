namespace TCSched.Api.Domain;

public sealed class Registration
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }

    /// <summary>The name as the player typed it (trimmed).</summary>
    public required string DisplayName { get; set; }

    /// <summary>Duplicate-detection key; unique per event. See <see cref="NameNormalizer"/>.</summary>
    public required string NormalizedName { get; set; }

    public DateTime RegisteredAtUtc { get; set; }
}
