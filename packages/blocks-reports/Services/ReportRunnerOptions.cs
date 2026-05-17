namespace Sunfish.Blocks.Reports;

/// <summary>
/// Runner-level options.
/// </summary>
public sealed class ReportRunnerOptions
{
    /// <summary>
    /// Maximum number of warnings to attach to a
    /// <see cref="ReportRunResult{T}"/> before truncation. Default
    /// 32. Tune up for verbose period-crossing cartridges.
    /// </summary>
    public int MaxWarnings { get; set; } = 32;

    /// <summary>
    /// Reserved — runner-side enforcement lands in PR 7 (umbrella),
    /// where the timeout has a natural home alongside any per-kind
    /// override map. PR 1 substrate accepts + stores the value but
    /// does NOT yet enforce it; the caller's
    /// <see cref="System.Threading.CancellationToken"/> is the only
    /// cancellation surface honored in Phase 1. Cartridge authors
    /// MUST NOT rely on this option to bound execution time in PRs
    /// 2–6. Per Stage 02 §11 Q10 — default 60s.
    /// </summary>
    /// <remarks>
    /// Council .NET-architect A.1 + security-engineering SE-1
    /// (PR #980 review) both flagged the doc-vs-behavior gap; both
    /// agreed enforcement is best handled at PR 7's umbrella seam
    /// rather than pollute the substrate with cancellation-source
    /// translation logic the cluster will rewrite anyway.
    /// </remarks>
    public System.TimeSpan HardTimeout { get; set; } = System.TimeSpan.FromSeconds(60);
}
