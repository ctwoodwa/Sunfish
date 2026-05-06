using System;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Single-setting snapshot in an <see cref="AtlasView"/>. Per ADR 0065 §5.
/// </summary>
/// <remarks>
/// The snapshot is the LWW-projected current value at the cited path,
/// produced by <see cref="IAtlasProjector.ProjectAsync"/> from the per-tenant
/// Standing Order log. The <see cref="LastIssuedBy"/> field references the
/// <see cref="StandingOrder"/> whose issuance produced the current value
/// (after applying ADR 0065 §2's last-writer-wins-by-IssuedAt-then-IssuedBy
/// resolution at the (<see cref="StandingOrder.Scope"/>, <see cref="Path"/>) grain).
/// </remarks>
/// <param name="Path">Dotted path within the parent scope (matches the producing <see cref="StandingOrderTriple.Path"/>).</param>
/// <param name="CurrentValue">Current value at the path; null when the most recent triple's <see cref="StandingOrderTriple.NewValue"/> was null (the path is "unset").</param>
/// <param name="LastIssuedBy">The <see cref="StandingOrder"/> that produced this snapshot. Per ADR 0065 §5 the field name preserves the spec's naming.</param>
/// <param name="LastIssuedAt">Wall-clock time at which the producing order was issued.</param>
/// <param name="Schema">Schema descriptor — drives the Atlas form-view renderer.</param>
public sealed record AtlasSettingSnapshot(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("currentValue")] JsonNode? CurrentValue,
    [property: JsonPropertyName("lastIssuedBy")] StandingOrderId LastIssuedBy,
    [property: JsonPropertyName("lastIssuedAt")] DateTimeOffset LastIssuedAt,
    [property: JsonPropertyName("schema")] AtlasSchemaDescriptor Schema);
