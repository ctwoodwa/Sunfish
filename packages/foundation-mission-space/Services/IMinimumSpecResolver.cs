using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per ADR 0063-A1.1 — evaluates a <see cref="MinimumSpec"/> against the
/// host's runtime <see cref="MissionEnvelope"/> and produces a
/// <see cref="SystemRequirementsResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// Per A1.7 — per-platform overrides COMPOSE with the baseline:
/// for each dimension, the platform override replaces the baseline value
/// if present; otherwise the baseline applies. <see cref="EvaluateAsync"/>
/// applies the COMPOSE rule before evaluation.
/// </para>
/// <para>
/// Per A1.8 — verdict semantics: <see cref="OverallVerdict.Block"/> if any
/// <see cref="DimensionPolicyKind.Required"/> dimension fails;
/// <see cref="OverallVerdict.WarnOnly"/> if no Required dimension fails but
/// at least one <see cref="DimensionPolicyKind.Recommended"/> dimension does;
/// <see cref="OverallVerdict.Pass"/> otherwise. Per A1.8 explicit
/// Informational rule, <see cref="DimensionPolicyKind.Informational"/>
/// dimensions are surfaced in the result but never gate the verdict.
/// </para>
/// <para>
/// Per A1.7 — host calls <see cref="InvalidateCache"/> when the upstream
/// dimension probe transitions (Healthy → Stale / Failed). Cost class is
/// <see cref="ProbeCostClass.Medium"/> per A1.6; default cache TTL is 30s.
/// </para>
/// </remarks>
public interface IMinimumSpecResolver
{
    /// <summary>Evaluates <paramref name="spec"/> against <paramref name="envelope"/> for the supplied <paramref name="platform"/> key.</summary>
    /// <param name="spec">The bundle's authored <see cref="MinimumSpec"/>.</param>
    /// <param name="envelope">The host's runtime <see cref="MissionEnvelope"/>.</param>
    /// <param name="platform">Optional platform key (e.g., <c>"ios"</c>); when provided, the matching <see cref="PerPlatformSpec"/> override composes with the baseline.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask<SystemRequirementsResult> EvaluateAsync(
        MinimumSpec spec,
        MissionEnvelope envelope,
        string? platform = null,
        CancellationToken ct = default);

    /// <summary>Per A1.7 — invalidate the per-spec cache (host signals on probe-status transition).</summary>
    void InvalidateCache();
}
