namespace Sunfish.Blocks.Leases.Models;

/// <summary>
/// A resolved view of one tenant party on a lease, enriched with a
/// display name from the canonical people cluster per ADR §27 / party-
/// model-convention §4 cross-cluster boundary rule.
/// </summary>
/// <param name="PartyId">
/// Canonical party identifier from
/// <c>Sunfish.Blocks.People.Foundation.Models.PartyId</c>.
/// </param>
/// <param name="DisplayName">
/// Display name resolved via
/// <c>Sunfish.Blocks.People.Foundation.Services.IPartyReadModel.GetByIdAsync</c>,
/// or <see langword="null"/> if the party was not found in the people
/// cluster (orphan-tolerant per CRDT §12).
/// </param>
/// <param name="Role">
/// The per-lease role this party holds. Derived from
/// <see cref="Lease.Tenants"/> ordering when no explicit
/// <see cref="LeasePartyRole"/> binding exists: index 0 →
/// <see cref="LeaseHolderRole.PrimaryLeaseholder"/>, others →
/// <see cref="LeaseHolderRole.CoLeaseholder"/>.
/// </param>
public sealed record LeaseholderDisplay(
    Sunfish.Blocks.People.Foundation.Models.PartyId PartyId,
    string? DisplayName,
    LeaseHolderRole Role);
