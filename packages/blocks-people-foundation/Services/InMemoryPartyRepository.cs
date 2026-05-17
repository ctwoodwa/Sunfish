using System.Collections.Concurrent;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Blocks.People.Foundation.Models.Events;
using Sunfish.Blocks.People.Foundation.Validation;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.People.Foundation.Services;

/// <summary>
/// In-memory backing store implementing both <see cref="IPartyReadModel"/>
/// and <see cref="IPartyWriteService"/>. Suitable for tests, unit fixtures,
/// and the v1 Anchor desktop seed. Persistence-backed implementations
/// (SQLite, Postgres) ship as separate follow-on packages and register
/// themselves as the canonical bindings, shadowing this one.
///
/// <para>
/// <b>Cross-cluster event emission:</b> every write delivers an envelope
/// to the injected <see cref="IDomainEventPublisher"/>. When the canonical
/// foundation-events publisher isn't wired, the host's DI registration
/// of <see cref="NoopDomainEventPublisher"/> consumes them silently.
/// </para>
///
/// <para>
/// <b>Tenant isolation:</b> reads filter by tenant. The repository does
/// NOT enforce that a given <see cref="PartyId"/> is unique across tenants
/// (callers control id allocation); a cross-tenant id collision would
/// surface as a cross-tenant read returning the wrong tenant's row, so
/// stick to <see cref="PartyId.NewId"/> for fresh creates.
/// </para>
/// </summary>
public sealed class InMemoryPartyRepository : IPartyReadModel, IPartyWriteService
{
    private readonly ConcurrentDictionary<PartyId, Party> _parties = new();
    private readonly ConcurrentDictionary<PartyRoleId, PartyRole> _roles = new();
    private readonly ConcurrentDictionary<EmailAddressId, EmailAddress> _emails = new();
    private readonly ConcurrentDictionary<PhoneNumberId, PhoneNumber> _phones = new();
    private readonly ConcurrentDictionary<PartyAddressId, PartyAddress> _addresses = new();
    private readonly IDomainEventPublisher _events;

    /// <summary>Build a repository wired to the given event publisher (or noop if null).</summary>
    public InMemoryPartyRepository(IDomainEventPublisher? events = null)
    {
        _events = events ?? new NoopDomainEventPublisher();
    }

    // ──────────────────────────────────────────────────────────────────
    //  IPartyReadModel
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default)
    {
        _parties.TryGetValue(id, out var p);
        return Task.FromResult(p is null || p.DeletedAt is not null ? null : p);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<PartyId, Party>> GetManyAsync(
        IReadOnlyCollection<PartyId> ids,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<PartyId, Party>(ids.Count);
        foreach (var id in ids)
        {
            if (_parties.TryGetValue(id, out var p) && p.DeletedAt is null)
                result[id] = p;
        }
        return Task.FromResult<IReadOnlyDictionary<PartyId, Party>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Party>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var result = _parties.Values
            .Where(p => p.TenantId == tenantId && p.DeletedAt is null)
            .ToList();
        return Task.FromResult<IReadOnlyList<Party>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Party>> FindByExactDisplayNameAsync(
        TenantId tenantId,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var result = _parties.Values
            .Where(p => p.TenantId == tenantId
                && p.DeletedAt is null
                && string.Equals(p.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult<IReadOnlyList<Party>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Party>> FindByExactEmailAsync(
        TenantId tenantId,
        string emailAddress,
        CancellationToken cancellationToken = default)
    {
        var partyIds = _emails.Values
            .Where(e => e.TenantId == tenantId
                && e.ReplacedAt is null
                && e.DeletedAt is null
                && string.Equals(e.Address, emailAddress, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.PartyId)
            .Distinct()
            .ToList();

        var parties = partyIds
            .Select(pid => _parties.TryGetValue(pid, out var p) ? p : null)
            .Where(p => p is not null && p.DeletedAt is null)
            .Cast<Party>()
            .ToList();
        return Task.FromResult<IReadOnlyList<Party>>(parties);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Party>> FindByExactPhoneE164Async(
        TenantId tenantId,
        string e164,
        CancellationToken cancellationToken = default)
    {
        var partyIds = _phones.Values
            .Where(ph => ph.TenantId == tenantId
                && ph.ReplacedAt is null
                && ph.DeletedAt is null
                && string.Equals(ph.E164, e164, StringComparison.Ordinal))
            .Select(ph => ph.PartyId)
            .Distinct()
            .ToList();

        var parties = partyIds
            .Select(pid => _parties.TryGetValue(pid, out var p) ? p : null)
            .Where(p => p is not null && p.DeletedAt is null)
            .Cast<Party>()
            .ToList();
        return Task.FromResult<IReadOnlyList<Party>>(parties);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EmailAddress>> GetActiveEmailsAsync(PartyId id, CancellationToken cancellationToken = default)
    {
        var result = _emails.Values
            .Where(e => e.PartyId == id && e.ReplacedAt is null && e.DeletedAt is null)
            .ToList();
        return Task.FromResult<IReadOnlyList<EmailAddress>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PhoneNumber>> GetActivePhonesAsync(PartyId id, CancellationToken cancellationToken = default)
    {
        var result = _phones.Values
            .Where(ph => ph.PartyId == id && ph.ReplacedAt is null && ph.DeletedAt is null)
            .ToList();
        return Task.FromResult<IReadOnlyList<PhoneNumber>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PartyAddress>> GetActiveAddressesAsync(PartyId id, CancellationToken cancellationToken = default)
    {
        var result = _addresses.Values
            .Where(a => a.PartyId == id && a.ReplacedAt is null && a.DeletedAt is null)
            .ToList();
        return Task.FromResult<IReadOnlyList<PartyAddress>>(result);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PartyRole>> GetActiveRolesAsync(PartyId id, CancellationToken cancellationToken = default)
    {
        var result = _roles.Values
            .Where(r => r.PartyId == id && r.EndedAt is null && r.DeletedAt is null)
            .ToList();
        return Task.FromResult<IReadOnlyList<PartyRole>>(result);
    }

    /// <inheritdoc />
    public Task<bool> HasActiveRoleAsync(PartyId id, string roleName, CancellationToken cancellationToken = default)
    {
        var hit = _roles.Values.Any(r =>
            r.PartyId == id
            && r.EndedAt is null
            && r.DeletedAt is null
            && string.Equals(r.RoleName, roleName, StringComparison.Ordinal));
        return Task.FromResult(hit);
    }

    // ──────────────────────────────────────────────────────────────────
    //  IPartyWriteService
    // ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Party> CreateAsync(
        TenantId tenantId,
        PartyKind kind,
        string displayName,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        var party = Party.Create(tenantId, kind, displayName, actor);
        Throw(PartyValidator.Validate(party));
        _parties[party.Id] = party;
        await Publish(PeopleFoundationEventNames.PartyCreated,
            new PartyCreatedPayload(party.Id, party.Kind, party.DisplayName),
            $"party-created:{party.Id.Value}",
            tenantId,
            cancellationToken).ConfigureAwait(false);
        return party;
    }

    /// <inheritdoc />
    public async Task<Party> UpdateAsync(Party updated, PartyId actor, CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(updated.Id, out var existing))
            throw new InvalidOperationException($"Party '{updated.Id.Value}' does not exist.");
        if (existing.DeletedAt is not null)
            throw new InvalidOperationException($"Party '{updated.Id.Value}' is tombstoned; cannot update.");

        var stamped = updated with
        {
            UpdatedAt = Instant.Now,
            UpdatedBy = actor,
            Version = existing.Version + 1,
        };
        Throw(PartyValidator.Validate(stamped));
        _parties[stamped.Id] = stamped;
        return stamped;
    }

    /// <inheritdoc />
    public Task<Party> DeleteAsync(PartyId id, string? reason, PartyId actor, CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(id, out var existing))
            throw new InvalidOperationException($"Party '{id.Value}' does not exist.");
        if (existing.DeletedAt is not null)
            return Task.FromResult(existing); // idempotent on re-delete

        var now = Instant.Now;
        var deleted = existing with
        {
            DeletedAt = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAt = now,
            UpdatedBy = actor,
            Version = existing.Version + 1,
        };
        _parties[id] = deleted;
        return PublishAndReturn(
            PeopleFoundationEventNames.PartyDeleted,
            new PartyDeletedPayload(id, reason),
            $"party-deleted:{id.Value}",
            existing.TenantId,
            deleted,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PartyRole> AttachRoleAsync(
        PartyId partyId,
        string roleName,
        string roleRecordId,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(partyId, out var party) || party.DeletedAt is not null)
            throw new InvalidOperationException($"Party '{partyId.Value}' is not live; cannot attach role.");

        // Idempotency: same (PartyId, RoleName, RoleRecordId) with an active
        // row → return the existing row, no event.
        var existingActive = _roles.Values.FirstOrDefault(r =>
            r.PartyId == partyId
            && r.EndedAt is null
            && r.DeletedAt is null
            && string.Equals(r.RoleName, roleName, StringComparison.Ordinal)
            && string.Equals(r.RoleRecordId, roleRecordId, StringComparison.Ordinal));
        if (existingActive is not null)
            return existingActive;

        var role = PartyRole.Create(party.TenantId, partyId, roleName, roleRecordId, actor);
        Throw(PartyRoleValidator.Validate(role));
        _roles[role.Id] = role;
        await Publish(PeopleFoundationEventNames.RoleAttached,
            new RoleAttachedPayload(role.Id, partyId, roleName, roleRecordId),
            $"role-attached:{role.Id.Value}",
            party.TenantId,
            cancellationToken).ConfigureAwait(false);
        return role;
    }

    /// <inheritdoc />
    public async Task<PartyRole> DetachRoleAsync(
        PartyRoleId roleId,
        string? endedReason,
        PartyId actor,
        CancellationToken cancellationToken = default)
    {
        if (!_roles.TryGetValue(roleId, out var role))
            throw new InvalidOperationException($"PartyRole '{roleId.Value}' does not exist.");
        if (role.EndedAt is not null)
            throw new InvalidOperationException($"PartyRole '{roleId.Value}' is already detached.");

        var ended = role.End(Instant.Now, endedReason, actor);
        Throw(PartyRoleValidator.Validate(ended));
        _roles[roleId] = ended;
        await Publish(PeopleFoundationEventNames.RoleDetached,
            new RoleDetachedPayload(roleId, role.PartyId, role.RoleName, endedReason),
            $"role-detached:{roleId.Value}",
            role.TenantId,
            cancellationToken).ConfigureAwait(false);
        return ended;
    }

    /// <inheritdoc />
    public async Task<EmailAddress> AddEmailAsync(
        PartyId partyId,
        string address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(partyId, out var party) || party.DeletedAt is not null)
            throw new InvalidOperationException($"Party '{partyId.Value}' is not live; cannot add email.");
        var email = EmailAddress.Create(party.TenantId, partyId, address, isPrimary, actor, label);
        Throw(EmailAddressValidator.Validate(email));
        _emails[email.Id] = email;
        await Publish(PeopleFoundationEventNames.EmailAddressAdded,
            new EmailAddressAddedPayload(email.Id, partyId, address, isPrimary),
            $"email-added:{email.Id.Value}",
            party.TenantId,
            cancellationToken).ConfigureAwait(false);
        return email;
    }

    /// <inheritdoc />
    public async Task<EmailAddress> SupersedeEmailAsync(
        EmailAddressId priorRowId,
        string address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (!_emails.TryGetValue(priorRowId, out var prior))
            throw new InvalidOperationException($"EmailAddress '{priorRowId.Value}' does not exist.");
        if (prior.ReplacedAt is not null)
            throw new InvalidOperationException($"EmailAddress '{priorRowId.Value}' is already superseded.");
        if (prior.DeletedAt is not null)
            throw new InvalidOperationException($"EmailAddress '{priorRowId.Value}' is tombstoned.");

        var now = Instant.Now;
        var newRow = EmailAddress.Create(prior.TenantId, prior.PartyId, address, isPrimary, actor, label);
        Throw(EmailAddressValidator.Validate(newRow));

        var supersededPrior = prior with
        {
            ReplacedAt = now,
            UpdatedAt = now,
            UpdatedBy = actor,
            Version = prior.Version + 1,
        };
        _emails[priorRowId] = supersededPrior;
        _emails[newRow.Id] = newRow;
        await Publish(PeopleFoundationEventNames.EmailAddressAdded,
            new EmailAddressAddedPayload(newRow.Id, newRow.PartyId, address, isPrimary),
            $"email-added:{newRow.Id.Value}",
            prior.TenantId,
            cancellationToken).ConfigureAwait(false);
        return newRow;
    }

    /// <inheritdoc />
    public async Task<PhoneNumber> AddPhoneAsync(
        PartyId partyId,
        string e164,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        bool isMobile = false,
        string? extension = null,
        CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(partyId, out var party) || party.DeletedAt is not null)
            throw new InvalidOperationException($"Party '{partyId.Value}' is not live; cannot add phone.");
        var phone = PhoneNumber.Create(party.TenantId, partyId, e164, isPrimary, actor, label, extension, isMobile);
        Throw(PhoneNumberValidator.Validate(phone));
        _phones[phone.Id] = phone;
        await Publish(PeopleFoundationEventNames.PhoneNumberAdded,
            new PhoneNumberAddedPayload(phone.Id, partyId, e164, isPrimary),
            $"phone-added:{phone.Id.Value}",
            party.TenantId,
            cancellationToken).ConfigureAwait(false);
        return phone;
    }

    /// <inheritdoc />
    public async Task<PartyAddress> AddAddressAsync(
        PartyId partyId,
        Address address,
        bool isPrimary,
        PartyId actor,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (!_parties.TryGetValue(partyId, out var party) || party.DeletedAt is not null)
            throw new InvalidOperationException($"Party '{partyId.Value}' is not live; cannot add address.");
        var pa = PartyAddress.Create(party.TenantId, partyId, address, isPrimary, actor, label);
        Throw(PartyAddressValidator.Validate(pa));
        _addresses[pa.Id] = pa;
        await Publish(PeopleFoundationEventNames.AddressAdded,
            new AddressAddedPayload(pa.Id, partyId, address.Country, isPrimary),
            $"address-added:{pa.Id.Value}",
            party.TenantId,
            cancellationToken).ConfigureAwait(false);
        return pa;
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    private static void Throw(ValidationResult result)
    {
        if (!result.IsValid) throw new PartyValidationException(result);
    }

    private Task Publish<TPayload>(
        string eventType,
        TPayload payload,
        string idempotencyKey,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var envelope = new DomainEventEnvelope<TPayload>
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = 1,
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey = idempotencyKey,
            Payload = payload!,
        };
        return _events.PublishAsync(envelope, cancellationToken);
    }

    private async Task<TReturn> PublishAndReturn<TPayload, TReturn>(
        string eventType,
        TPayload payload,
        string idempotencyKey,
        TenantId tenantId,
        TReturn returnValue,
        CancellationToken cancellationToken)
    {
        await Publish(eventType, payload, idempotencyKey, tenantId, cancellationToken).ConfigureAwait(false);
        return returnValue;
    }
}
