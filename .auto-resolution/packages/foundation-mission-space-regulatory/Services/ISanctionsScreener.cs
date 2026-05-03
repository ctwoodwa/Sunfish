using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Per ADR 0064-A1.3 + A1.6 — sanctions screener. Operator-decision-aware
/// emit-only by default; <see cref="ScreeningPolicy.AdvisoryOnly"/> opts out
/// of any blocking semantics and emits a one-shot
/// <c>SanctionsAdvisoryOnlyConfigured</c> audit at host configuration time.
/// </summary>
public interface ISanctionsScreener
{
    /// <summary>The configured policy. Phase 1 substrate ships with <see cref="ScreeningPolicy.Default"/>.</summary>
    ScreeningPolicy Policy { get; }

    /// <summary>
    /// Screens a subject against the configured sanctions list source.
    /// Returns the result regardless of <see cref="Policy"/>; the decision
    /// to act on hits is the host's. Per A1.3: <see cref="ScreeningPolicy.AdvisoryOnly"/>
    /// hosts use the result for visibility only — no enforcement is implied.
    /// </summary>
    ValueTask<SanctionsScreeningResult> ScreenAsync(string subjectId, CancellationToken ct = default);
}

/// <summary>Source of sanctions list entries. Phase 3 wires concrete sources (OFAC SDN, EU consolidated list, etc.).</summary>
public interface ISanctionsListSource
{
    /// <summary>
    /// Returns matches for <paramref name="subjectId"/>. Phase 1 substrate
    /// ships an empty source; production hosts wire their own per A1.16.
    /// </summary>
    System.Collections.Generic.IReadOnlyList<SanctionsListEntry> MatchesFor(string subjectId);
}
