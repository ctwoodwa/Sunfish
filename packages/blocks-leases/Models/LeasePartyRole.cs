namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// Join entry binding a <see cref="LeaseHolderRole"/> to a <see cref="PartyId"/>
/// on a specific <see cref="LeaseId"/>. Multiple roles per lease are
/// permitted (one PrimaryLeaseholder + N CoLeaseholders + N Occupants +
/// N Guarantors). Per W#27 Phase 4.
/// </summary>
public sealed record LeasePartyRole
{
    /// <summary>Stable identifier.</summary>
    public required LeasePartyRoleId Id { get; init; }

    /// <summary>The lease this role-binding applies to.</summary>
    public required LeaseId Lease { get; init; }

    /// <summary>The party assigned this role.</summary>
    public required PartyId Party { get; init; }

    /// <summary>The role the party plays on this lease.</summary>
    public required LeaseHolderRole Role { get; init; }
}
