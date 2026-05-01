using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// TEMPORARY local stub for ADR 0063's <c>MinimumSpec</c> type. Lives
/// in foundation-catalog only until ADR 0063 Phase 1 substrate (W#41
/// or sibling) ships the canonical type in
/// <c>Sunfish.Foundation.MissionSpace</c>. At that point, replace this
/// stub with a using-alias or namespace import; the
/// <see cref="BusinessCaseBundleManifest.Requirements"/> field
/// signature remains unchanged for callers (the canonical shape's
/// <see cref="Policy"/> field is positionally compatible with this
/// stub).
/// </summary>
/// <remarks>
/// Per the W#38 stub-unblock addendum (2026-05-01), the stub is
/// intentionally minimal: it carries the JSON shape ADR 0063 specifies
/// but no behavior. The canonical <c>MinimumSpec</c> will add: per-
/// dimension spec records (10 dimensions); <see cref="SpecPolicy"/>
/// extension; <c>PerPlatformSpec</c> overrides;
/// <c>IMinimumSpecResolver</c> consumer.
/// </remarks>
// TODO(adr-0063 phase-1): replace with using-alias to
// Sunfish.Foundation.MissionSpace.MinimumSpec when canonical substrate ships.
public sealed record MinimumSpec
{
    /// <summary>The bundle author's intent for install-time gating per ADR 0063.</summary>
    [JsonPropertyName("policy")]
    [JsonConverter(typeof(JsonStringEnumConverter<SpecPolicy>))]
    public SpecPolicy Policy { get; init; } = SpecPolicy.Recommended;
}

/// <summary>
/// Per ADR 0063 — bundle-author intent for the
/// <see cref="MinimumSpec"/> at install-time UX. Stub for the
/// W#38 timeline; canonical 3-value taxonomy preserved.
/// </summary>
public enum SpecPolicy
{
    /// <summary>Install is blocked when the spec doesn't match.</summary>
    Required,

    /// <summary>Install proceeds with a UX warning when the spec doesn't match.</summary>
    Recommended,

    /// <summary>Spec is shown to the operator but does not gate install.</summary>
    Informational,
}
