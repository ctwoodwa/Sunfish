namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A person or entity that is a party to a lease (tenant, landlord, manager, or guarantor).
/// </summary>
public sealed record Party
{
    /// <summary>Unique identifier for this party.</summary>
    public required PartyId Id { get; init; }

    /// <summary>Human-readable display name (e.g., full name or company name).</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role this party plays in lease transactions.</summary>
    public required PartyKind Kind { get; init; }
}
