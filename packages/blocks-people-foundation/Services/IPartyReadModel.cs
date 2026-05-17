using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Services;

/// <summary>
/// Read-side surface over the canonical Party substrate. Downstream
/// blocks (AR, Leases, Work Orders, Comms) take an
/// <see cref="IPartyReadModel"/> dependency to resolve display data
/// and presence-of-role checks; they never reach into the underlying
/// store directly.
///
/// <para>
/// History queries (with <c>ReplacedAt</c> rows or <c>EndedAt</c>
/// roles) are intentionally excluded from this v1 surface — most
/// consumers want "current state of Jane". Time-travel queries land
/// in a follow-on history-projection workstream once persistence
/// (SQLite-backed repo) ships.
/// </para>
/// </summary>
public interface IPartyReadModel
{
    /// <summary>Fetch a single Party by id; returns null when missing or tombstoned.</summary>
    Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Fetch many Parties by id; missing/tombstoned ids are omitted from the result.</summary>
    Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(
        IReadOnlyCollection<PartyId> ids,
        CancellationToken cancellationToken = default);

    /// <summary>List all live (non-tombstoned) Parties in a tenant.</summary>
    Task<IReadOnlyList<Party>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default);

    /// <summary>Find Parties by exact display name (case-insensitive). Returns empty list if none match.</summary>
    Task<IReadOnlyList<Party>> FindByExactDisplayNameAsync(
        TenantId tenantId,
        string displayName,
        CancellationToken cancellationToken = default);

    /// <summary>Find Parties by exact email address (case-insensitive, queries currently-active EmailAddress rows).</summary>
    Task<IReadOnlyList<Party>> FindByExactEmailAsync(
        TenantId tenantId,
        string emailAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Find Parties by exact E.164 phone (queries currently-active PhoneNumber rows).</summary>
    Task<IReadOnlyList<Party>> FindByExactPhoneE164Async(
        TenantId tenantId,
        string e164,
        CancellationToken cancellationToken = default);

    /// <summary>Currently-active EmailAddress rows (ReplacedAt == null AND DeletedAt == null) for a party.</summary>
    Task<IReadOnlyList<EmailAddress>> GetActiveEmailsAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Currently-active PhoneNumber rows for a party.</summary>
    Task<IReadOnlyList<PhoneNumber>> GetActivePhonesAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Currently-active PartyAddress rows for a party.</summary>
    Task<IReadOnlyList<PartyAddress>> GetActiveAddressesAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Currently-active PartyRole edges (EndedAt == null AND DeletedAt == null) for a party.</summary>
    Task<IReadOnlyList<PartyRole>> GetActiveRolesAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>True iff <paramref name="id"/> currently holds <paramref name="roleName"/> on any record (active row exists).</summary>
    Task<bool> HasActiveRoleAsync(PartyId id, string roleName, CancellationToken cancellationToken = default);
}
