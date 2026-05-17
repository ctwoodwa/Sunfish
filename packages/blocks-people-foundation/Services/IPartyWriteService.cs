using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.People.Foundation.Services;

/// <summary>
/// Write-side surface for the canonical Party substrate. Every method
/// validates via the PR 1/2 validators on entry and throws
/// <see cref="PartyValidationException"/> on failure; callers that
/// want non-throwing semantics should run the validator manually
/// before calling.
///
/// <para>
/// <b>Actor parameter, not implicit context.</b> Each method takes
/// <c>actor: PartyId</c> explicitly. We considered injecting
/// <c>IActorPrincipalResolver</c> (which exists in
/// <c>foundation-ship-common</c>) but its shape returns a
/// <see cref="Sunfish.Foundation.Capabilities.Principal"/>, not a
/// <see cref="PartyId"/> — there is no canonical Principal → Party
/// mapping yet, and inventing one inside this service would create
/// an abstraction the call sites don't actually want. The caller
/// (controller, importer, migration script) decides where the actor
/// comes from. The Halt 1 follow-up is whoever lands a canonical
/// <c>IPartyContext</c> across the platform.
/// </para>
/// </summary>
public interface IPartyWriteService
{
    /// <summary>Create + persist a fresh Party. Returns the materialized record.</summary>
    Task<Party> CreateAsync(
        TenantId tenantId,
        PartyKind kind,
        string displayName,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist a mutated <see cref="Party"/> record. The caller produces
    /// the new state via <c>existing with { ... }</c>; this service bumps
    /// <c>Version</c> and stamps <c>UpdatedAt</c> / <c>UpdatedBy</c>.
    /// </summary>
    Task<Party> UpdateAsync(Party updated, PartyId actor, CancellationToken cancellationToken = default);

    /// <summary>Tombstone a Party (sets <c>DeletedAt</c> + <c>DeletedReason</c>; never hard-deleted).</summary>
    Task<Party> DeleteAsync(PartyId id, string? reason, PartyId actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attach a role-edge to a party. Idempotency: if an active
    /// (<c>EndedAt == null</c>) <see cref="PartyRole"/> already exists
    /// for <c>(PartyId, RoleName, RoleRecordId)</c>, the existing
    /// row is returned and NO event is emitted. Detach + re-attach
    /// across different RoleRecordIds creates separate rows by design.
    /// </summary>
    Task<PartyRole> AttachRoleAsync(
        PartyId partyId,
        string roleName,
        string roleRecordId,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>End an attached role-edge. Throws if the role is already ended.</summary>
    Task<PartyRole> DetachRoleAsync(
        PartyRoleId roleId,
        string? endedReason,
        PartyId actor,
        CancellationToken cancellationToken = default);

    /// <summary>Add an email-address row. Insert-only — updates use the supersede path.</summary>
    Task<EmailAddress> AddEmailAsync(
        PartyId partyId,
        string address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Supersede an email-address row: insert a new row with the
    /// caller-supplied state + set <c>ReplacedAt</c> on the prior row.
    /// Atomic at the repository level — readers never see both rows
    /// active simultaneously.
    /// </summary>
    Task<EmailAddress> SupersedeEmailAsync(
        EmailAddressId priorRowId,
        string address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default);

    /// <summary>Add a phone-number row (E.164).</summary>
    Task<PhoneNumber> AddPhoneAsync(
        PartyId partyId,
        string e164,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        bool isMobile = false,
        string? extension = null,
        CancellationToken cancellationToken = default);

    /// <summary>Add a postal-address row.</summary>
    Task<PartyAddress> AddAddressAsync(
        PartyId partyId,
        Address address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown by <see cref="IPartyWriteService"/> methods when a write fails
/// validation. Carries the underlying
/// <see cref="Validation.ValidationResult"/> for inspection.
/// </summary>
public sealed class PartyValidationException : Exception
{
    /// <summary>The validator's structured result.</summary>
    public Validation.ValidationResult Result { get; }

    /// <summary>Construct from a validator result; message reads the first error.</summary>
    public PartyValidationException(Validation.ValidationResult result)
        : base(result.Errors.Count == 0 ? "Validation failed." : result.Errors[0])
    {
        Result = result;
    }
}
