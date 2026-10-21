namespace TCSched.Api.Domain;

/// <summary>
/// Data that describes a game. Event logic reads these properties and never branches on
/// <see cref="Id"/>, so adding a game is a data change, not a code change.
/// </summary>
public sealed class GameTemplate
{
    /// <summary>Stable slug, e.g. "mtg".</summary>
    public required string Id { get; set; }
    public required string Name { get; set; }
    public List<string> Formats { get; set; } = [];
    public required string DefaultFormat { get; set; }
    public int DefaultDurationMinutes { get; set; }
    public int DefaultCapacity { get; set; }
    public int MaxCapacity { get; set; }
    public int MinPlayers { get; set; }
}
