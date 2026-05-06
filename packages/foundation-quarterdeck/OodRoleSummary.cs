using System;
using Sunfish.Foundation.Wayfinder;

namespace Sunfish.Foundation.Quarterdeck;

/// <summary>
/// Per-role OOD watch summary surfaced on the Quarterdeck per
/// ADR 0080 §2.3 rule 2. The data provider reads the active watch via
/// <c>IOodWatchService.GetActiveWatchAsync</c> for both roles and
/// projects each result into a single
/// <see cref="OodRoleSummary"/> instance per role.
/// </summary>
/// <param name="Role">The OOD role this summary is for.</param>
/// <param name="CurrentActorDisplayName">
/// Display name of the actor currently standing this watch. Null when
/// no active watch exists or the watch has expired.
/// </param>
/// <param name="WatchStartedAt">
/// Wall-clock timestamp when the current watch began. Null when no
/// active watch exists.
/// </param>
/// <param name="IsExpired">
/// Whether the active watch has elapsed past its expected duration.
/// False when no watch is active (absence is distinct from expiry).
/// </param>
public sealed record OodRoleSummary(
    OodRole Role,
    string? CurrentActorDisplayName,
    DateTimeOffset? WatchStartedAt,
    bool IsExpired);
