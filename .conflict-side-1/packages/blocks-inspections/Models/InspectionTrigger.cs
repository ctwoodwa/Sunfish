namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Categorizes the purpose / context of an inspection.
/// </summary>
/// <remarks>
/// Added 2026-04-29 per workstream #25 EXTEND. Existing inspection records
/// have <see cref="Inspection.Trigger"/> = <see langword="null"/> meaning
/// "trigger not specified / pre-revision"; new code should always set a
/// trigger so the move-in / move-out delta projection
/// (<c>IInspectionsService.GetMoveInOutDeltaAsync</c>) can pair the right inspections.
/// </remarks>
public enum InspectionTrigger
{
    /// <summary>Routine annual inspection (default for property-management cadence).</summary>
    Annual,

    /// <summary>Move-in baseline inspection at lease start; documents unit + equipment condition before tenancy.</summary>
    MoveIn,

    /// <summary>Move-out delta inspection at lease end; documents unit + equipment condition for security-deposit reconciliation.</summary>
    MoveOut,

    /// <summary>Verification inspection after maintenance/repair work; confirms work-order completion quality.</summary>
    PostRepair,

    /// <summary>Ad-hoc inspection initiated by owner or contractor; not on a regular cadence.</summary>
    OnDemand,
}
