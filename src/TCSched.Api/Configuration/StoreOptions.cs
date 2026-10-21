using System.ComponentModel.DataAnnotations;

namespace TCSched.Api.Configuration;

/// <summary>The single store this app schedules for. Used for the ICS location and time zone.</summary>
public sealed class StoreOptions
{
    public const string SectionName = "Store";

    [Required] public string Name { get; set; } = "";
    [Required] public string Address { get; set; } = "";

    /// <summary>IANA time zone id, e.g. "America/Los_Angeles". Organizer input is interpreted in this zone.</summary>
    [Required] public string TimeZone { get; set; } = "";

    /// <summary>
    /// Base URL players use (for the registration link inside the .ics). Optional: when empty,
    /// the request's own scheme and host are used.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Template id pre-selected on the create-event form (e.g. "mtg"). Optional: if it's missing
    /// or unknown, the UI uses the first template. Store config, not game logic.
    /// </summary>
    public string? DefaultGameTemplateId { get; set; }
}
