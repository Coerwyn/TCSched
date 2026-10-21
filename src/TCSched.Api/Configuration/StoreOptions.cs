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

    /// <summary>True when <see cref="PublicBaseUrl"/> is blank or an absolute http/https URL.</summary>
    public bool HasValidPublicBaseUrl() =>
        string.IsNullOrWhiteSpace(PublicBaseUrl)
        || (Uri.TryCreate(PublicBaseUrl.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment));

    /// <summary>
    /// Template id pre-selected on the create-event form (e.g. "mtg"). Optional: if it's missing
    /// or unknown, the UI uses the first template. Store config, not game logic.
    /// </summary>
    public string? DefaultGameTemplateId { get; set; }
}
