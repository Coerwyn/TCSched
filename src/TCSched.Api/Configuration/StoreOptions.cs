using System.ComponentModel.DataAnnotations;

namespace TCSched.Api.Configuration;

/// <summary>The single store this app schedules for. Used for the ICS location and time zone.</summary>
public sealed class StoreOptions
{
    public const string SectionName = "Store";

    [Required] public string Name { get; set; } = "";
    [Required] public string Address { get; set; } = "";

    /// <summary>IANA time zone id, e.g. "America/Chicago". Organizer input is interpreted in this zone.</summary>
    [Required] public string TimeZone { get; set; } = "";
}
