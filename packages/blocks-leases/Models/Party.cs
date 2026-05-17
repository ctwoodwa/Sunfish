namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A person or entity that is a party to a lease (tenant, landlord, manager, or guarantor).
/// </summary>
/// <remarks>
/// <b>DEPRECATED — use <see cref="Sunfish.Blocks.People.Foundation.Models.Party"/> instead.</b>
/// Predates the canonical party-model convention (see
/// <c>_shared/engineering/party-model-convention.md</c>). The lease-
/// local shape carries only Id+DisplayName+Kind; the canonical Party
/// has the full ~30-field surface. Resolve display names through
/// <c>Sunfish.Blocks.People.Foundation.Services.IPartyReadModel</c>
/// at read time rather than denormalizing.
/// </remarks>
[Obsolete("Use Sunfish.Blocks.People.Foundation.Models.Party instead. Resolve display names via IPartyReadModel; removal is a future sunfish-api-change pipeline step.")]
public sealed record Party
{
    /// <summary>Unique identifier for this party.</summary>
    public required PartyId Id { get; init; }

    /// <summary>Human-readable display name (e.g., full name or company name).</summary>
    public required string DisplayName { get; init; }

    /// <summary>The role this party plays in lease transactions.</summary>
    public required PartyKind Kind { get; init; }
}
