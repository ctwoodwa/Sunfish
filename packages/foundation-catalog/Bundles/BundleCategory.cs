using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>Coarse classification for a business-case bundle.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BundleCategory
{
    /// <summary>Operations-heavy bundles (property mgmt, facility ops, project mgmt).</summary>
    Operations = 0,

    /// <summary>Diligence-heavy bundles (acquisition, underwriting, compliance reviews).</summary>
    Diligence = 1,

    /// <summary>Finance-heavy bundles (accounting packages, treasury).</summary>
    Finance = 2,

    /// <summary>Platform bundles that do not fit a single business case (admin packs, observability).</summary>
    Platform = 3,
}
