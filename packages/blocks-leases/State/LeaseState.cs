using Sunfish.Blocks.Leases.Models;

namespace Sunfish.Blocks.Leases.State;

/// <summary>
/// Placeholder state-machine record for the lease lifecycle.
/// Holds the current <see cref="LeasePhase"/> and records the last transition time.
/// Transition logic is deferred to a follow-up pass (DocuSign workflow, commencement rules, etc.).
/// </summary>
/// <param name="Phase">The current lifecycle phase of the associated lease.</param>
/// <param name="EnteredAtUtc">
/// The UTC timestamp at which the lease entered <paramref name="Phase"/>.
/// </param>
public sealed record LeaseState(LeasePhase Phase, DateTime EnteredAtUtc)
{
    /// <summary>
    /// Creates an initial <see cref="LeaseState"/> in <see cref="LeasePhase.Draft"/>
    /// with <see cref="EnteredAtUtc"/> set to <see cref="DateTime.UtcNow"/>.
    /// </summary>
    public static LeaseState Initial() => new(LeasePhase.Draft, DateTime.UtcNow);
}
