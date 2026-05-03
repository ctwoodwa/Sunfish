namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// The role a <see cref="Party"/> plays in a lease transaction.
/// </summary>
public enum PartyKind
{
    /// <summary>A person or entity renting the unit.</summary>
    Tenant,

    /// <summary>The property owner or their agent.</summary>
    Landlord,

    /// <summary>A property manager acting on behalf of a landlord.</summary>
    Manager,

    /// <summary>A guarantor responsible for rent obligations.</summary>
    Guarantor
}
