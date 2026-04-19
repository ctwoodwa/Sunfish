namespace Sunfish.Blocks.Inspections.Models;

/// <summary>
/// Lifecycle phases for an <see cref="Inspection"/>.
/// </summary>
/// <remarks>
/// Valid transitions:
/// <list type="bullet">
///   <item><description><see cref="Scheduled"/> → <see cref="InProgress"/> via <c>StartAsync</c></description></item>
///   <item><description><see cref="InProgress"/> → <see cref="Completed"/> via <c>CompleteAsync</c></description></item>
///   <item><description><see cref="Scheduled"/> or <see cref="InProgress"/> → <see cref="Cancelled"/> (future pass)</description></item>
/// </list>
/// </remarks>
public enum InspectionPhase
{
    /// <summary>Inspection has been created and is waiting to begin.</summary>
    Scheduled,

    /// <summary>Inspector has started the inspection; responses are being collected.</summary>
    InProgress,

    /// <summary>All responses recorded; inspection is closed.</summary>
    Completed,

    /// <summary>Inspection was cancelled before or during execution.</summary>
    Cancelled,
}
