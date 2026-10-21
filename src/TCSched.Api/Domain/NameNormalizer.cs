using System.Text;
using System.Text.RegularExpressions;

namespace TCSched.Api.Domain;

/// <summary>
/// Builds the duplicate-detection key for a player name: Unicode-normalized, trimmed,
/// inner whitespace collapsed, upper-cased. " alex  smith" and "ALEX SMITH" collide.
/// </summary>
public static partial class NameNormalizer
{
    public static string Clean(string name) =>
        Whitespace().Replace(name.Normalize(NormalizationForm.FormC).Trim(), " ");

    public static string Normalize(string name) => Clean(name).ToUpperInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
