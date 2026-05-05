using System;
using System.Collections.Generic;
using System.Threading;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Cross-tenant sweep boundary for the expiry background service. R4 (XO
/// post-merge council 2026-05-06): extracted from <see cref="IOodWatchRepository"/>
/// so the type system enforces the single-caller invariant — only
/// <see cref="OodWatchExpiryService"/> can resolve this interface, because
/// it is <c>internal</c> and not part of the public surface.
/// </summary>
/// <remarks>
/// Concrete repository implementations typically implement BOTH
/// <see cref="IOodWatchRepository"/> (per-tenant operations) and
/// <see cref="IOodWatchSweepRepository"/> (cross-tenant sweep). The host's
/// DI container registers each binding to the same singleton instance —
/// see the <c>AddSunfishWayfinder</c> XML doc for the recommended pattern.
/// </remarks>
internal interface IOodWatchSweepRepository
{
    /// <summary>
    /// Returns all Active watches whose
    /// <c>StartedAt + MaxWatchDuration &lt;= cutoff</c> across every tenant.
    /// Called exclusively by <see cref="OodWatchExpiryService"/> per ADR 0078 §5.
    /// </summary>
    IAsyncEnumerable<OodWatch> GetExpiredCandidatesAsync(
        DateTimeOffset cutoff, CancellationToken ct = default);
}
