namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Lifecycle phases for a <see cref="Lease"/>.
/// </summary>
/// <remarks>
/// Allowed transitions (per W#27 hand-off Phase 1):
/// <code>
/// Draft → AwaitingSignature | Cancelled
/// AwaitingSignature → Executed | Cancelled | Draft (revisions)
/// Executed → Active
/// Active → Renewed | Terminated
/// Renewed → Active
/// </code>
/// Terminal: <see cref="Terminated"/>, <see cref="Cancelled"/>.
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
    Terminated,

    /// <summary>Lease was cancelled before execution (terminal).</summary>
    Cancelled
}
