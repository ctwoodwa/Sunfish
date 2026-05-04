using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Materialized projection of a tenant's Standing Order log into a
/// path-keyed settings catalog. Per ADR 0065 §5.
/// </summary>
/// <remarks>
/// Returned by <see cref="IAtlasProjector.ProjectAsync"/>. The projection
/// applies ADR 0065 §2's last-writer-wins-by-IssuedAt-then-IssuedBy at the
/// (<see cref="StandingOrder.Scope"/>, <see cref="StandingOrderTriple.Path"/>)
/// grain; only orders in <see cref="StandingOrderState.Validated"/> /
/// <see cref="StandingOrderState.Applied"/> contribute (rescinded / rejected /
/// conflicted orders are skipped).
/// </remarks>
/// <param name="TenantId">Tenant the view was projected for.</param>
/// <param name="ProjectedAt">Wall-clock time at which the projection was computed.</param>
/// <param name="SettingsByPath">Composite-key-keyed dictionary of current settings. Keys are <c>"&lt;scope&gt;:&lt;path&gt;"</c> (lowercase scope name + literal colon + path) so the same path under two scopes appears as two distinct entries. <see cref="AtlasSettingSnapshot.Path"/> on each value carries the raw path without the scope prefix.</param>
public sealed record AtlasView(
    [property: JsonPropertyName("tenantId")] TenantId TenantId,
    [property: JsonPropertyName("projectedAt")] DateTimeOffset ProjectedAt,
    [property: JsonPropertyName("settingsByPath")] IReadOnlyDictionary<string, AtlasSettingSnapshot> SettingsByPath);
