using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.FeatureManagement;

/// <summary>
/// Input to feature evaluation. Carries the tenant, edition, active bundles
/// and modules, caller identity, environment, and free-form attributes
/// that providers and entitlement resolvers may consult.
/// </summary>
public sealed record FeatureEvaluationContext
{
    /// <summary>Tenant the evaluation is for, if known.</summary>
    public TenantId? TenantId { get; init; }

    /// <summary>Edition key (e.g. <c>lite</c>, <c>standard</c>, <c>enterprise</c>), if known.</summary>
    public string? Edition { get; init; }

    /// <summary>Bundle keys currently active for the tenant.</summary>
    public IReadOnlyList<string> ActiveBundleKeys { get; init; } = Array.Empty<string>();

    /// <summary>Module keys currently active for the tenant.</summary>
    public IReadOnlyList<string> ActiveModuleKeys { get; init; } = Array.Empty<string>();

    /// <summary>Caller identity, if available.</summary>
    public string? UserId { get; init; }

    /// <summary>Deployment environment. Defaults to <c>production</c>.</summary>
    public string Environment { get; init; } = "production";

    /// <summary>Free-form attributes (region, locale, device class, …).</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; }
        = new Dictionary<string, string>();
}
