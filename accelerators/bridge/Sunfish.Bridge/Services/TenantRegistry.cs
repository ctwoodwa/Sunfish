using Microsoft.EntityFrameworkCore;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Entities;

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
/// </remarks>
public interface ITenantRegistry
{
    Task<TenantRegistration?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<TenantRegistration?> GetByIdAsync(Guid tenantId, CancellationToken ct);
    Task<TenantRegistration> CreateAsync(string slug, string displayName, string plan, CancellationToken ct);
    Task SetTeamPublicKeyAsync(Guid tenantId, byte[] publicKey, CancellationToken ct);
    Task UpdateTrustLevelAsync(Guid tenantId, TrustLevel level, CancellationToken ct);
    Task<IReadOnlyList<TenantRegistration>> ListActiveAsync(CancellationToken ct);
}

/// <inheritdoc cref="ITenantRegistry"/>
public sealed class TenantRegistry : ITenantRegistry
{
    private readonly SunfishBridgeDbContext _db;

    public TenantRegistry(SunfishBridgeDbContext db) => _db = db;

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
    /// Returns a <see cref="TenantRegistration"/> with <see cref="TenantStatus.Pending"/>
    /// and a <see langword="null"/> <see cref="TenantRegistration.TeamPublicKey"/>. The
    /// founder flow (Wave 5.4) completes the record via <see cref="SetTeamPublicKeyAsync"/>.
    /// Throws <see cref="InvalidOperationException"/> if <paramref name="slug"/> is already
    /// taken — enforced by the unique index on <c>tenant_registrations.Slug</c>, verified
    /// in-memory here so tests can assert the friendlier message.
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

        return registration;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Transitions <see cref="TenantStatus.Pending"/> → <see cref="TenantStatus.Active"/>.
    /// Does nothing to tenants already in non-Pending states (idempotent key rotation
    /// is a later concern; for Wave 5.1 the operation is write-once-at-founder-flow-end).
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

        registration.TeamPublicKey = publicKey;
        if (registration.Status == TenantStatus.Pending)
        {
            registration.Status = TenantStatus.Active;
        }
        registration.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
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
}
