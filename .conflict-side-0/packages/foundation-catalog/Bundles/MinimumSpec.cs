using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// DEPRECATED — superseded by <c>Sunfish.Foundation.MissionSpace.MinimumSpec</c>.
/// Scheduled for removal 2026-08-01.
/// </summary>
/// <remarks>
/// <para>
/// W#41 (PR #473) shipped the canonical <c>MinimumSpec</c> at
/// <c>Sunfish.Foundation.MissionSpace</c> with the full 10-dimension
/// per-dimension spec records, <c>PerPlatformSpec</c> COMPOSE rule, and
/// <c>IMinimumSpecResolver</c> consumer. This stub remains for the
/// transition window (90 days from 2026-05-01) so existing callers in
/// <c>foundation-catalog</c> tests don't break.
/// </para>
/// <para>
/// New code MUST use <c>Sunfish.Foundation.MissionSpace.MinimumSpec</c>
/// directly. Callers of <see cref="BusinessCaseBundleManifest.Requirements"/>
/// will migrate when the field's type is renamed in the follow-up
/// removal PR (scheduled for 2026-08-01); the JSON shape is wire-format
/// compatible (<see cref="Policy"/> field maps to canonical
/// <c>SpecPolicy</c>).
/// </para>
/// <para>
/// SCHEDULED_REMOVAL: 2026-08-01 — drop this stub + retype
/// <see cref="BusinessCaseBundleManifest.Requirements"/> to
/// <c>Sunfish.Foundation.MissionSpace.MinimumSpec?</c>. Hard
/// <c>[Obsolete]</c> escalation to compiler error not applied here
/// because repo-wide TreatWarningsAsErrors=true would break the W#38
/// test surface today; the deprecation lands as documentation +
/// scheduled removal date per the W#41 hand-off allowance.
/// </para>
/// </remarks>
public sealed record MinimumSpec
{
    /// <summary>The bundle author's intent for install-time gating per ADR 0063.</summary>
    [JsonPropertyName("policy")]
    [JsonConverter(typeof(JsonStringEnumConverter<SpecPolicy>))]
    public SpecPolicy Policy { get; init; } = SpecPolicy.Recommended;
}

/// <summary>
/// DEPRECATED — superseded by <c>Sunfish.Foundation.MissionSpace.SpecPolicy</c>.
/// Scheduled for removal 2026-08-01 alongside the deprecated
/// <see cref="MinimumSpec"/> stub. Canonical 3-value taxonomy
/// (<c>Required</c> / <c>Recommended</c> / <c>Informational</c>) is
/// wire-format identical to the canonical enum.
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
