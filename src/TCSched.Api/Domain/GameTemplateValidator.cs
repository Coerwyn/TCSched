using System.Text.RegularExpressions;

namespace TCSched.Api.Domain;

/// <summary>
/// The rules every template must follow. Used by the seeder today and meant to be reused
/// unchanged by the Phase 2 admin endpoints.
/// </summary>
public static partial class GameTemplateValidator
{
    public const int MinDurationMinutes = 15;
    public const int MaxDurationMinutes = 720;
    public const int MaxNameLength = 60;

    /// <summary>Returns errors keyed by field name (ValidationProblem shape). Empty means valid.</summary>
    public static Dictionary<string, string[]> Validate(GameTemplate t)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
            list.Add(message);
        }

        if (string.IsNullOrWhiteSpace(t.Id) || !Slug().IsMatch(t.Id))
            Add(nameof(t.Id), "Id must be 2-32 characters of lowercase letters, digits or '-'.");

        if (string.IsNullOrWhiteSpace(t.Name) || t.Name.Trim().Length > MaxNameLength)
            Add(nameof(t.Name), $"Name is required and must be at most {MaxNameLength} characters.");

        if (t.Formats.Count == 0 || t.Formats.Any(string.IsNullOrWhiteSpace))
            Add(nameof(t.Formats), "At least one format is required, and formats cannot be blank.");
        else if (t.Formats.Distinct(StringComparer.OrdinalIgnoreCase).Count() != t.Formats.Count)
            Add(nameof(t.Formats), "Formats must be unique.");

        if (!t.Formats.Contains(t.DefaultFormat))
            Add(nameof(t.DefaultFormat), "Default format must be one of the formats.");

        if (t.DefaultDurationMinutes is < MinDurationMinutes or > MaxDurationMinutes)
            Add(nameof(t.DefaultDurationMinutes), $"Duration must be {MinDurationMinutes}-{MaxDurationMinutes} minutes.");

        if (t.MinPlayers < 1)
            Add(nameof(t.MinPlayers), "Minimum players must be at least 1.");
        if (t.DefaultCapacity < t.MinPlayers)
            Add(nameof(t.DefaultCapacity), "Default capacity must be at least the minimum players.");
        if (t.MaxCapacity < t.DefaultCapacity)
            Add(nameof(t.MaxCapacity), "Max capacity must be at least the default capacity.");
        if (t.MaxCapacity > EventRules.MaxCapacity)
            Add(nameof(t.MaxCapacity), $"Max capacity cannot exceed {EventRules.MaxCapacity}.");

        return errors.ToDictionary(e => e.Key, e => e.Value.ToArray());
    }

    [GeneratedRegex("^[a-z0-9-]{2,32}$")]
    private static partial Regex Slug();
}
