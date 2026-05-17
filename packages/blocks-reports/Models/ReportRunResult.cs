namespace Sunfish.Blocks.Reports;

/// <summary>
/// Generic result envelope returned by
/// <see cref="IReportRunner.RunAsync{TParams,TResult}"/>.
/// </summary>
/// <param name="Kind">The cartridge kind that produced this result.</param>
/// <param name="Result">The cartridge-specific result payload.</param>
/// <param name="RunAtUtc">Wall-clock at which the report run started.</param>
/// <param name="SnapshotMarker">Opaque marker captured before cartridge invocation; identifies the upstream-state slice the result is derived from.</param>
/// <param name="RunDuration">Elapsed wall-clock from run start to cartridge completion.</param>
/// <param name="IsProvisional">True when the report's underlying data crosses an Open or SoftClosed accounting period and is subject to revision.</param>
/// <param name="Warnings">Cartridge-attached warnings (e.g., "trial balance crosses Open period 2026-05").</param>
public sealed record ReportRunResult<TResult>(
    ReportKind Kind,
    TResult Result,
    System.DateTimeOffset RunAtUtc,
    string SnapshotMarker,
    System.TimeSpan RunDuration,
    bool IsProvisional,
    System.Collections.Generic.IReadOnlyList<string> Warnings)
    where TResult : class;
