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
    /// Hard timeout for any single cartridge execution. Beyond this,
    /// the runner cancels and throws
    /// <see cref="Exceptions.ReportCartridgeExecutionException"/>.
    /// Per Stage 02 §11 Q10 — Phase 1 default is 60s.
    /// </summary>
    public System.TimeSpan HardTimeout { get; set; } = System.TimeSpan.FromSeconds(60);
}
