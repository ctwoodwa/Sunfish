using System.Collections.Concurrent;
using Sunfish.Blocks.TenantAdmin.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TenantAdmin.Services;

/// <summary>
/// In-memory implementation of <see cref="ITenantAdminService"/> backed by
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for demos,
/// integration tests, and kitchen-sink scenarios. Not intended for production.
/// </summary>
public sealed class InMemoryTenantAdminService : ITenantAdminService
{
    private readonly ConcurrentDictionary<TenantId, TenantProfile> _profiles = new();
    private readonly ConcurrentDictionary<TenantUserId, TenantUser> _users = new();
    private readonly ConcurrentDictionary<BundleActivationId, BundleActivation> _activations = new();

    /// <inheritdoc />
    public ValueTask<TenantProfile?> GetTenantProfileAsync(TenantId tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _profiles.TryGetValue(tenantId, out var profile);
        return ValueTask.FromResult(profile);
    }

    /// <inheritdoc />
    public ValueTask<TenantProfile> UpdateTenantProfileAsync(UpdateTenantProfileRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var updated = _profiles.AddOrUpdate(
            request.TenantId,
            _ =>
            {
                if (string.IsNullOrWhiteSpace(request.DisplayName))
                {
                    throw new InvalidOperationException(
                        "DisplayName is required when creating a tenant profile.");
                }

                return new TenantProfile
                {
                    TenantId = request.TenantId,
                    DisplayName = request.DisplayName,
                    ContactEmail = request.ContactEmail,
                    ContactPhone = request.ContactPhone,
                    BundleKey = request.BundleKey,
                    CreatedAt = DateTime.UtcNow,
                };
            },
            (_, existing) => existing with
            {
                DisplayName = request.DisplayName ?? existing.DisplayName,
                ContactEmail = request.ContactEmail ?? existing.ContactEmail,
                ContactPhone = request.ContactPhone ?? existing.ContactPhone,
                BundleKey = request.BundleKey ?? existing.BundleKey,
            });

        return ValueTask.FromResult(updated);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<TenantUser>> ListTenantUsersAsync(TenantId tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<TenantUser> list = _users.Values
            .Where(u => u.TenantId == tenantId)
            .OrderBy(u => u.InvitedAt)
            .ToArray();
        return ValueTask.FromResult(list);
    }

    /// <inheritdoc />
    public ValueTask<TenantUser> InviteTenantUserAsync(InviteTenantUserRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request));
        }

        var user = new TenantUser
        {
            Id = TenantUserId.NewId(),
            TenantId = request.TenantId,
            Email = request.Email,
            DisplayName = request.DisplayName,
            Role = request.Role,
            InvitedAt = DateTime.UtcNow,
            AcceptedAt = null,
        };

        _users[user.Id] = user;
        return ValueTask.FromResult(user);
    }

    /// <inheritdoc />
    public ValueTask<TenantUser> AssignRoleAsync(TenantId tenantId, TenantUserId userId, TenantRole role, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_users.TryGetValue(userId, out var existing) || existing.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                $"No tenant-user '{userId}' found for tenant '{tenantId}'.");
        }

        var updated = existing with { Role = role };
        _users[userId] = updated;
        return ValueTask.FromResult(updated);
    }

    /// <inheritdoc />
    public ValueTask<bool> RemoveTenantUserAsync(TenantId tenantId, TenantUserId userId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_users.TryGetValue(userId, out var existing) && existing.TenantId == tenantId)
        {
            return ValueTask.FromResult(_users.TryRemove(userId, out _));
        }

        return ValueTask.FromResult(false);
    }

    /// <inheritdoc />
    public ValueTask<BundleActivation> ActivateBundleAsync(ActivateBundleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.BundleKey))
        {
            throw new ArgumentException("BundleKey is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Edition))
        {
            throw new ArgumentException("Edition is required.", nameof(request));
        }

        var activation = new BundleActivation
        {
            Id = BundleActivationId.NewId(),
            TenantId = request.TenantId,
            BundleKey = request.BundleKey,
            Edition = request.Edition,
            ActivatedAt = DateTime.UtcNow,
            DeactivatedAt = null,
        };

        _activations[activation.Id] = activation;
        return ValueTask.FromResult(activation);
    }

    /// <inheritdoc />
    public ValueTask<bool> DeactivateBundleAsync(TenantId tenantId, string bundleKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var active = _activations.Values
            .Where(a => a.TenantId == tenantId
                         && string.Equals(a.BundleKey, bundleKey, StringComparison.Ordinal)
                         && a.DeactivatedAt is null)
            .ToArray();

        if (active.Length == 0)
        {
            return ValueTask.FromResult(false);
        }

        foreach (var row in active)
        {
            _activations[row.Id] = row with { DeactivatedAt = DateTime.UtcNow };
        }

        return ValueTask.FromResult(true);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<BundleActivation>> ListActiveBundlesAsync(TenantId tenantId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<BundleActivation> list = _activations.Values
            .Where(a => a.TenantId == tenantId && a.DeactivatedAt is null)
            .OrderBy(a => a.ActivatedAt)
            .ToArray();

        return ValueTask.FromResult(list);
    }
}
