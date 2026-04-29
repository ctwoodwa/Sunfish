namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Reference primitive — what consuming records (signature events, equipment
/// rows, vendor specialties) store to point at a node. Versions are pinned
/// at classification time so historical records remain interpretable even
/// after later definition versions ship.
/// </summary>
public sealed record TaxonomyClassification
{
    /// <summary>Identity of the referenced definition.</summary>
    public required TaxonomyDefinitionId Definition { get; init; }

    /// <summary>Stable code of the referenced node.</summary>
    public required string Code { get; init; }

    /// <summary>Pinned version. Mandatory for compliance and audit references.</summary>
    public required TaxonomyVersion Version { get; init; }

    /// <summary>Optional cached display string, captured at classification time for offline rendering. Resolvers may refresh this from the live definition when needed.</summary>
    public string? DisplayCache { get; init; }
}
