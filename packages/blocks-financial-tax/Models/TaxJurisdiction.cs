using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// A taxing authority's jurisdiction node — federal, state, county,
/// city, district, special — per Stage 02 §3.14. Forms a tree via
/// <see cref="ParentJurisdictionId"/>; the root of each chain is
/// typically a Country or Federal node.
///
/// <para>
/// CRDT conventions applied:
/// </para>
/// <list type="bullet">
///   <item>§1 — string ULID/Guid id via <see cref="TaxJurisdictionId"/>.</item>
///   <item>§2 — <see cref="DeletedAtUtc"/> tombstone (soft-delete).</item>
///   <item>§5 — <see cref="JurisdictionLevel"/> is a stable string code.</item>
/// </list>
/// </summary>
/// <param name="Id">Stable identity.</param>
/// <param name="Level">Federal / state / county / city / district / special / country.</param>
/// <param name="IsoCountry">ISO 3166-1 alpha-2 country code ("US", "CA", "DE").</param>
/// <param name="Region">Optional ISO 3166-2 region code ("US-VA", "DE-BY"). Null for country-level rows.</param>
/// <param name="Locality">Optional locality string ("Frederick County", "Winchester"). Null for state/federal rows.</param>
/// <param name="Name">Display label shown to users in the tax-code editor.</param>
/// <param name="ParentJurisdictionId">FK to the enclosing jurisdiction. Null for a root (typically Country or Federal).</param>
/// <param name="Notes">Free-form operator notes.</param>
/// <param name="IsActive">When false, the row is retained but excluded from default resolver results.</param>
/// <param name="CreatedAtUtc">When the row was first written.</param>
/// <param name="UpdatedAtUtc">When the row was last upserted.</param>
/// <param name="DeletedAtUtc">Tombstone — when set, the row is soft-deleted. Resolver excludes by default.</param>
public sealed record TaxJurisdiction(
    TaxJurisdictionId Id,
    JurisdictionLevel Level,
    string IsoCountry,
    string? Region,
    string? Locality,
    string Name,
    TaxJurisdictionId? ParentJurisdictionId,
    string? Notes,
    bool IsActive = true,
    Instant? CreatedAtUtc = null,
    Instant? UpdatedAtUtc = null,
    Instant? DeletedAtUtc = null)
{
    /// <summary>
    /// Create a freshly-stamped <see cref="TaxJurisdiction"/> with a new
    /// id and matching created/updated timestamps. Callers that already
    /// have a stable id (e.g. importing from ERPNext) should construct
    /// the record directly.
    /// </summary>
    public static TaxJurisdiction Create(
        JurisdictionLevel level,
        string isoCountry,
        string name,
        TaxJurisdictionId? parentJurisdictionId = null,
        string? region = null,
        string? locality = null,
        string? notes = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        return new TaxJurisdiction(
            Id: TaxJurisdictionId.NewId(),
            Level: level,
            IsoCountry: isoCountry,
            Region: region,
            Locality: locality,
            Name: name,
            ParentJurisdictionId: parentJurisdictionId,
            Notes: notes,
            IsActive: true,
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }
}
