namespace Sunfish.Foundation.Taxonomy.Models;

/// <summary>
/// Governance posture of a taxonomy definition (ADR 0056). Determines who
/// may mutate the definition's node set and version stream.
/// </summary>
public enum TaxonomyGovernanceRegime
{
    /// <summary>Tenant-local; clone, extend, and alter freely. No central approval needed.</summary>
    Civilian,

    /// <summary>Org-scoped; the owner approves derivations from the parent. Used for enterprise glossaries.</summary>
    Enterprise,

    /// <summary>Sunfish-shipped or compliance-source; only Sunfish (the Authoritative actor) may publish new versions or mutate nodes. Pinned-version references only.</summary>
    Authoritative
}
