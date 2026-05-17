namespace Sunfish.Blocks.Reports;

/// <summary>
/// Opt-in interface that cartridge results can implement to surface
/// provisionality + warnings up through the
/// <see cref="IReportRunner"/>'s <see cref="ReportRunResult{T}"/>
/// envelope. Cartridges that consume Open or SoftClosed accounting
/// periods (Trial Balance, P&amp;L) implement this; cartridges that
/// don't (RentRoll, AR/AP Aging) skip it.
/// </summary>
public interface IReportProvisionalityCarrier
{
    /// <summary>True when the result derives from data crossing an Open or SoftClosed period.</summary>
    bool IsProvisional { get; }

    /// <summary>Plain-English warnings to surface to the report consumer (e.g., "trial balance crosses Open period 2026-05").</summary>
    System.Collections.Generic.IReadOnlyList<string> Warnings { get; }
}
