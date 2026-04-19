namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Lifecycle phases for a <see cref="Lease"/>.
/// </summary>
/// <remarks>
/// Transitions are intentionally deferred to a follow-up pass.
/// Current follow-up items (not implemented here):
/// <list type="bullet">
///   <item><description>Draft → AwaitingSignature (DocuSign envelope dispatch)</description></item>
///   <item><description>AwaitingSignature → Executed (all parties signed)</description></item>
///   <item><description>Executed → Active (commencement date reached)</description></item>
///   <item><description>Active → Renewed (renewal executed)</description></item>
///   <item><description>Active / Renewed → Terminated (early termination)</description></item>
/// </list>
/// </remarks>
public enum LeasePhase
{
    /// <summary>Lease is being authored; no signatures requested yet.</summary>
    Draft,

    /// <summary>Envelope sent; waiting for all party signatures.</summary>
    AwaitingSignature,

    /// <summary>All parties have signed; awaiting commencement date.</summary>
    Executed,

    /// <summary>Lease term is currently running.</summary>
    Active,

    /// <summary>Lease has been renewed and the renewed term is running.</summary>
    Renewed,

    /// <summary>Lease has been terminated before or at expiry.</summary>
    Terminated
}
