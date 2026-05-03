using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>Lifecycle status of a bundle manifest.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BundleStatus
{
    /// <summary>Authoring in progress; do not provision to tenants.</summary>
    Draft = 0,

    /// <summary>Opt-in for pilot tenants; may change without major-version bump.</summary>
    Preview = 1,

    /// <summary>Generally available; follows ADR 0007 versioning policy.</summary>
    GA = 2,

    /// <summary>Still loadable for existing tenants; no new provisioning.</summary>
    Deprecated = 3,
}
