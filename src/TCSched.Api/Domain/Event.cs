namespace TCSched.Api.Domain;

/// <summary>
/// A scheduled event. Format, duration, capacity and min players are copied from the template
/// when the event is created, so later template changes never alter an existing event.
/// </summary>
public sealed class Event
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    public required string GameTemplateId { get; set; }
    public GameTemplate? GameTemplate { get; set; }

    public required string Format { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public int DurationMinutes { get; set; }
    public int Capacity { get; set; }

    /// <summary>Players needed for the event to run (snapshot of the template's MinPlayers).</summary>
    public int MinPlayers { get; set; }

    /// <summary>
    /// Seats taken. Only changed by the conditional update in the registration flow, and kept
    /// within 0..Capacity by a DB check constraint.
    /// </summary>
    public int RegisteredCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public List<Registration> Registrations { get; set; } = [];

    public DateTime EndsAtUtc => StartsAtUtc.AddMinutes(DurationMinutes);
}
