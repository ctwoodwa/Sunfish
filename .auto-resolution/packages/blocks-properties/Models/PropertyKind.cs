namespace Sunfish.Blocks.Properties.Models;

/// <summary>
/// Coarse classification for a <see cref="Property"/>. Drives downstream
/// behaviour such as unit modelling (multi-unit only), depreciation defaults,
/// and listing-surface presentation. First-slice scope; richer subtype
/// information is deferred to follow-up hand-offs.
/// </summary>
public enum PropertyKind
{
    /// <summary>Single-family detached home or condo unit.</summary>
    SingleFamily,

    /// <summary>Multi-unit residential building (duplex, fourplex, apartment).</summary>
    MultiUnit,

    /// <summary>Mixed-use parcel combining residential and commercial space.</summary>
    Mixed,

    /// <summary>Vacant land with no permanent improvements.</summary>
    Land,
}
