using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.BusinessCases.Models;

/// <summary>
/// Point-in-time view of a tenant's resolved entitlements: which bundle + edition
/// is active, which modules are active, and which feature values flow from the
/// bundle manifest's <c>FeatureDefaults</c>.
/// </summary>
/// <param name="TenantId">The tenant this snapshot describes.</param>
/// <param name="ActiveBundleKey">The bundle currently active for the tenant, or null when no bundle is active.</param>
/// <param name="ActiveEdition">The edition selected from the active bundle, or null.</param>
/// <param name="ActiveModules">Module keys currently active (union of RequiredModules and the edition's mapped modules).</param>
/// <param name="ResolvedFeatureValues">Feature key → value map, sourced from the bundle's <c>FeatureDefaults</c>.</param>
/// <param name="ResolvedAt">UTC timestamp this snapshot was produced.</param>
public sealed record TenantEntitlementSnapshot(
    TenantId TenantId,
    string? ActiveBundleKey,
    string? ActiveEdition,
    IReadOnlyList<string> ActiveModules,
    IReadOnlyDictionary<string, string> ResolvedFeatureValues,
    DateTimeOffset ResolvedAt);
