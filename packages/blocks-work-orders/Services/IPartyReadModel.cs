namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// LOCAL STUB for the canonical <c>IPartyReadModel</c> that ships with
/// <c>blocks-people-foundation</c>. Same shape as
/// <c>blocks-financial-ar</c>'s local stub (sibling cluster pattern).
/// When <c>blocks-people-foundation</c> merges, this declaration gets
/// deleted + consumers re-namespace via a one-line <c>using</c> swap
/// in a follow-up sweep PR — no API surface change.
/// </summary>
// TODO: relocate to Sunfish.Blocks.People.Foundation when that package ships.
public interface IPartyReadModel
{
    /// <summary>
    /// Look up the display name for a party id, or <c>null</c> when
    /// unknown.
    /// </summary>
    Task<string?> GetDisplayNameAsync(Guid partyId, CancellationToken cancellationToken = default);
}
