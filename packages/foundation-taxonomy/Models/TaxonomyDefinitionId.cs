using System.Text.RegularExpressions;

namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Three-part identity for a taxonomy product, formatted as
/// <c>Vendor.Domain.TaxonomyName</c>. Each token must match
/// <c>[A-Za-z][A-Za-z0-9]*</c>; tokens may not contain periods.
/// </summary>
/// <param name="Vendor">Vendor / publisher segment (e.g., <c>Sunfish</c>).</param>
/// <param name="Domain">Domain segment (e.g., <c>Signature</c>, <c>Equipment</c>).</param>
/// <param name="TaxonomyName">Taxonomy-name segment (e.g., <c>Scopes</c>, <c>Classes</c>).</param>
public readonly record struct TaxonomyDefinitionId(string Vendor, string Domain, string TaxonomyName)
{
    private static readonly Regex TokenPattern = new(@"^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    /// <summary>Three-segment dotted identity string.</summary>
    public override string ToString() => $"{Vendor}.{Domain}.{TaxonomyName}";

    /// <summary>The full dotted identity string (alias for <see cref="ToString"/>).</summary>
    public string Value => ToString();

    /// <summary>Parses a <c>Vendor.Domain.TaxonomyName</c> string.</summary>
    /// <exception cref="FormatException">Thrown when the input is not exactly three valid tokens separated by periods.</exception>
    public static TaxonomyDefinitionId Parse(string identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var parts = identity.Split('.');
        if (parts.Length != 3)
        {
            throw new FormatException($"TaxonomyDefinitionId must have exactly three dot-separated tokens; got '{identity}'.");
        }
        foreach (var part in parts)
        {
            if (!TokenPattern.IsMatch(part))
            {
                throw new FormatException($"TaxonomyDefinitionId token '{part}' is invalid; tokens must match [A-Za-z][A-Za-z0-9]*.");
            }
        }
        return new TaxonomyDefinitionId(parts[0], parts[1], parts[2]);
    }

    /// <summary>Validates token shape against <c>[A-Za-z][A-Za-z0-9]*</c>; throws on the first invalid token.</summary>
    /// <exception cref="FormatException">Thrown when any segment is invalid.</exception>
    public void Validate()
    {
        if (!TokenPattern.IsMatch(Vendor)) throw new FormatException($"Vendor '{Vendor}' is invalid.");
        if (!TokenPattern.IsMatch(Domain)) throw new FormatException($"Domain '{Domain}' is invalid.");
        if (!TokenPattern.IsMatch(TaxonomyName)) throw new FormatException($"TaxonomyName '{TaxonomyName}' is invalid.");
    }
}
