using Microsoft.EntityFrameworkCore;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Orchestration;

namespace Sunfish.Bridge.Services;

/// <summary>
/// Façade over <see cref="SunfishBridgeDbContext"/> for the Bridge control-plane's
/// tenant-registry concerns (ADR 0031 Wave 5.1). Callers never query the DbContext
/// directly for tenant metadata — this service mediates every read and write so the
/// control plane has a single surface for signup, billing transitions, admin lookups,
/// and the founder-key handoff.
/// </summary>
/// <remarks>
/// <para>
/// This service is paper-aligned: it holds no team data. All records it manages are
/// operator-owned (<c>{tenant_id, plan, billing, support_contacts, team_public_key}</c>
/// per the ADR). The <c>team_public_key</c> column holds only a public key — the
/// operator never sees the matching private key, preserving paper §17.2's ciphertext-
/// at-rest invariant.
/// </para>
/// <para>
/// Registered by <c>Program.ConfigureSaasPosture</c> as scoped (matching DbContext
/// lifetime). Not registered in Relay posture; Relay has no control-plane surface.
/// </para>
/// <para>
/// Wave 5.2.B extends the service with <see cref="SuspendAsync"/>,
/// <see cref="ResumeAsync"/>, and <see cref="CancelAsync"/> lifecycle methods, and
/// publishes <see cref="TenantLifecycleEvent"/> via <see cref="ITenantRegistryEventBus"/>
/// after every successful mutation. The Wave 5.2.C supervisor subscribes to the bus
/// — this keeps the registry independent of process orchestration while allowing
/// the supervisor to react to every relevant state transition.
/// </para>
/// </remarks>
public interface ITenantRegistry
{
    Task<TenantRegistration?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<TenantRegistration?> GetByIdAsync(Guid tenantId, CancellationToken ct);
    Task<TenantRegistration> CreateAsync(string slug, string displayName, string plan, CancellationToken ct);
    Task SetTeamPublicKeyAsync(Guid tenantId, byte[] publicKey, CancellationToken ct);
    Task UpdateTrustLevelAsync(Guid tenantId, TrustLevel level, CancellationToken ct);
    Task<IReadOnlyList<TenantRegistration>> ListActiveAsync(CancellationToken ct);

    /// <summary>
    /// Transitions the tenant from <see cref="TenantStatus.Active"/> to
    /// <see cref="TenantStatus.Suspended"/>, recording <paramref name="reason"/>
    /// in <see cref="TenantRegistration.SuspendedReason"/>. Idempotent: calling
    /// on an already-<see cref="TenantStatus.Suspended"/> tenant is a no-op
    /// (no DB write, no event). Throws <see cref="InvalidOperationException"/>
    /// if the tenant is <see cref="TenantStatus.Pending"/> or
    /// <see cref="TenantStatus.Cancelled"/>.
    /// </summary>
    ValueTask SuspendAsync(Guid id, string reason, CancellationToken ct);

    /// <summary>
    /// Transitions the tenant from <see cref="TenantStatus.Suspended"/> back to
    /// <see cref="TenantStatus.Active"/>, clearing <see cref="TenantRegistration.SuspendedReason"/>.
    /// Idempotent on an already-<see cref="TenantStatus.Active"/> tenant. Throws
    /// <see cref="InvalidOperationException"/> on <see cref="TenantStatus.Pending"/>
    /// (resume of a never-activated tenant is meaningless) or
    /// <see cref="TenantStatus.Cancelled"/>.
    /// </summary>
    ValueTask ResumeAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Transitions the tenant from <see cref="TenantStatus.Active"/> or
    /// <see cref="TenantStatus.Suspended"/> to <see cref="TenantStatus.Cancelled"/>,
    /// recording the current UTC instant in <see cref="TenantRegistration.CancelledAt"/>.
    /// <paramref name="mode"/> is recorded on the published
    /// <see cref="TenantLifecycleEvent"/> for the Wave 5.2.C supervisor to act on;
    /// <see cref="TenantRegistry"/> itself performs no file I/O. Throws
    /// <see cref="InvalidOperationException"/> on <see cref="TenantStatus.Pending"/>
    /// or already-<see cref="TenantStatus.Cancelled"/>.
    /// </summary>
    ValueTask CancelAsync(Guid id, DeleteMode mode, CancellationToken ct);
}

/// <inheritdoc cref="ITenantRegistry"/>
public sealed class TenantRegistry : ITenantRegistry
{
    private readonly SunfishBridgeDbContext _db;
    private readonly ITenantRegistryEventBus _eventBus;

    public TenantRegistry(SunfishBridgeDbContext db, ITenantRegistryEventBus eventBus)
    {
        _db = db;
        _eventBus = eventBus;
    }

    /// <inheritdoc />
    public Task<TenantRegistration?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        return _db.TenantRegistrations.FirstOrDefaultAsync(t => t.Slug == slug, ct);
    }

    /// <inheritdoc />
    public Task<TenantRegistration?> GetByIdAsync(Guid tenantId, CancellationToken ct)
        => _db.TenantRegistrations.FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// Returns a <see cref="TenantRegistration"/> with <see cref="TenantStatus.Pending"/>
    /// and a <see langword="null"/> <see cref="TenantRegistration.TeamPublicKey"/>. The
    /// founder flow (Wave 5.4) completes the record via <see cref="SetTeamPublicKeyAsync"/>.
    /// Throws <see cref="InvalidOperationException"/> if <paramref name="slug"/> is already
    /// taken — enforced by the unique index on <c>tenant_registrations.Slug</c>, verified
    /// in-memory here so tests can assert the friendlier message.
    /// </para>
    /// <para>
    /// Publishes a <see cref="TenantLifecycleEvent"/> with
    /// <c>Previous == Current == <see cref="TenantStatus.Pending"/></c> — the enum has
    /// no <c>None</c> value, so <see cref="TenantLifecycleEvent"/> uses equality of
    /// <c>Previous</c> and <c>Current</c> as the fresh-create discriminator. Wave 5.2.C's
    /// supervisor subscribes to this event to pre-allocate tenant-scoped resources (paths,
    /// future keystore entries) at signup time rather than at first Active transition.
    /// </para>
    /// </remarks>
    public async Task<TenantRegistration> CreateAsync(
        string slug, string displayName, string plan, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(plan);

        // Pre-insert duplicate check. Providers with a unique index (Npgsql) also
        // enforce this at SaveChangesAsync time; the in-memory provider does not,
        // so this belt-and-braces read gives the service a consistent exception
        // shape across providers.
        var alreadyExists = await _db.TenantRegistrations
            .AsNoTracking()
            .AnyAsync(t => t.Slug == slug, ct);
        if (alreadyExists)
        {
            throw new InvalidOperationException(
                $"A tenant with slug '{slug}' already exists.");
        }

        var now = DateTime.UtcNow;
        var registration = new TenantRegistration
        {
            TenantId = Guid.NewGuid(),
            Slug = slug,
            DisplayName = displayName,
            Plan = plan,
            TrustLevel = TrustLevel.RelayOnly,
            Status = TenantStatus.Pending,
            TeamPublicKey = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.TenantRegistrations.Add(registration);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race: a concurrent writer won the insert between our pre-check and
            // SaveChanges. On relational providers the unique index throws. Detach
            // our pending entity and confirm the race via a second read. await in
            // a catch filter is disallowed (CS7094), hence the body-level check.
            _db.Entry(registration).State = EntityState.Detached;
            var duplicate = await _db.TenantRegistrations
                .AsNoTracking()
                .AnyAsync(t => t.Slug == slug && t.TenantId != registration.TenantId, ct);
            if (duplicate)
            {
                throw new InvalidOperationException(
                    $"A tenant with slug '{slug}' already exists.");
            }
            throw;
        }

        // Wave 5.2.B — signal fresh-create by Previous == Current == Pending.
        _eventBus.Publish(new TenantLifecycleEvent(
            TenantId: registration.TenantId,
            Previous: TenantStatus.Pending,
            Current: TenantStatus.Pending,
            OccurredAt: now,
            Reason: null));

        return registration;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Transitions <see cref="TenantStatus.Pending"/> → <see cref="TenantStatus.Active"/>.
    /// Does nothing to tenants already in non-Pending states (idempotent key rotation
    /// is a later concern; for Wave 5.1 the operation is write-once-at-founder-flow-end).
    /// Wave 5.2.B: publishes a Pending → Active <see cref="TenantLifecycleEvent"/> on
    /// the first-time transition so the supervisor can start the tenant's data plane.
    /// Re-invocation after the Active transition re-writes the key but does NOT re-publish.
    /// </remarks>
    public async Task SetTeamPublicKeyAsync(Guid tenantId, byte[] publicKey, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        if (publicKey.Length == 0)
        {
            throw new ArgumentException("publicKey must be non-empty.", nameof(publicKey));
        }

        var registration = await _db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        var wasPending = registration.Status == TenantStatus.Pending;

        registration.TeamPublicKey = publicKey;
        if (wasPending)
        {
            registration.Status = TenantStatus.Active;
        }
        var now = DateTime.UtcNow;
        registration.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        if (wasPending)
        {
            _eventBus.Publish(new TenantLifecycleEvent(
                TenantId: registration.TenantId,
                Previous: TenantStatus.Pending,
                Current: TenantStatus.Active,
                OccurredAt: now,
                Reason: null));
        }
    }

    /// <inheritdoc />
    public async Task UpdateTrustLevelAsync(Guid tenantId, TrustLevel level, CancellationToken ct)
    {
        var registration = await _db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.TenantId == tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant {tenantId} not found.");

        registration.TrustLevel = level;
        registration.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TenantRegistration>> ListActiveAsync(CancellationToken ct)
        => await _db.TenantRegistrations
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Slug)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async ValueTask SuspendAsync(Guid id, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var registration = await _db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.TenantId == id, ct)
            ?? throw new InvalidOperationException($"Tenant {id} not found.");

        switch (registration.Status)
        {
            case TenantStatus.Suspended:
                // Idempotent no-op. Do not rewrite SuspendedReason — the first-suspend
                // reason is the billing/operator audit artifact; swapping it silently
                // would destroy provenance. No event is published so supervisor
                // handlers don't re-fire on duplicate calls.
                return;

            case TenantStatus.Pending:
                throw new InvalidOperationException(
                    $"Tenant {id} is Pending; cannot Suspend before founder flow completes.");

            case TenantStatus.Cancelled:
                throw new InvalidOperationException(
                    $"Tenant {id} is Cancelled; Suspend is not a valid transition.");

            case TenantStatus.Active:
                // Fall-through to the mutation below.
                break;
        }

        var previous = registration.Status;
        registration.Status = TenantStatus.Suspended;
        registration.SuspendedReason = reason;
        var now = DateTime.UtcNow;
        registration.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        _eventBus.Publish(new TenantLifecycleEvent(
            TenantId: registration.TenantId,
            Previous: previous,
            Current: TenantStatus.Suspended,
            OccurredAt: now,
            Reason: reason));
    }

    /// <inheritdoc />
    public async ValueTask ResumeAsync(Guid id, CancellationToken ct)
    {
        var registration = await _db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.TenantId == id, ct)
            ?? throw new InvalidOperationException($"Tenant {id} not found.");

        switch (registration.Status)
        {
            case TenantStatus.Active:
                // Idempotent — already Active, no write, no event.
                return;

            case TenantStatus.Pending:
                throw new InvalidOperationException(
                    $"Tenant {id} is Pending; cannot Resume a tenant that has never been Active.");

            case TenantStatus.Cancelled:
                throw new InvalidOperationException(
                    $"Tenant {id} is Cancelled; Resume is not a valid transition.");

            case TenantStatus.Suspended:
                // Fall-through to the mutation below.
                break;
        }

        var previous = registration.Status;
        registration.Status = TenantStatus.Active;
        registration.SuspendedReason = null;
        var now = DateTime.UtcNow;
        registration.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        _eventBus.Publish(new TenantLifecycleEvent(
            TenantId: registration.TenantId,
            Previous: previous,
            Current: TenantStatus.Active,
            OccurredAt: now,
            Reason: null));
    }

    /// <inheritdoc />
    public async ValueTask CancelAsync(Guid id, DeleteMode mode, CancellationToken ct)
    {
        var registration = await _db.TenantRegistrations
            .FirstOrDefaultAsync(t => t.TenantId == id, ct)
            ?? throw new InvalidOperationException($"Tenant {id} not found.");

        switch (registration.Status)
        {
            case TenantStatus.Cancelled:
                throw new InvalidOperationException(
                    $"Tenant {id} is already Cancelled.");

            case TenantStatus.Pending:
                throw new InvalidOperationException(
                    $"Tenant {id} is Pending; Cancel is not a valid transition.");

            case TenantStatus.Active:
            case TenantStatus.Suspended:
                // Fall-through to the mutation below.
                break;
        }

        var previous = registration.Status;
        var now = DateTime.UtcNow;
        registration.Status = TenantStatus.Cancelled;
        registration.CancelledAt = now;
        registration.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        // DeleteMode is carried on the event payload so the Wave 5.2.C supervisor
        // can perform the graveyard-move vs. secure-wipe branch; TenantRegistry
        // itself does no file I/O. Mode is stringified into Reason because
        // TenantLifecycleEvent keeps a deliberately-narrow payload shape.
        _eventBus.Publish(new TenantLifecycleEvent(
            TenantId: registration.TenantId,
            Previous: previous,
            Current: TenantStatus.Cancelled,
            OccurredAt: now,
            Reason: mode.ToString()));
    }
}
