using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Recovery.TenantKey;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Reference <see cref="IDecryptCapabilityProvider"/> backed by the
/// existing <see cref="ITenantKeyProvider"/>. Issues short-lived
/// <see cref="FixedDecryptCapability"/> values bound to the
/// requested tenant for the requested purpose. Per W#48 Phase 1b
/// hand-off + ADR 0067 §5.3.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Capability scope:</b> this provider issues capabilities only
/// for tenants the underlying
/// <see cref="ITenantKeyProvider"/> has key material for —
/// <see cref="ITenantKeyProvider.DeriveKeyAsync"/> is consulted to
/// confirm the tenant is known + the purpose is supported. If
/// derivation throws or returns an empty key, the call returns
/// <c>null</c> per the fail-closed contract on
/// <see cref="IDecryptCapabilityProvider.AcquireAsync"/>.
/// </para>
/// <para>
/// <b>Purpose allowlist:</b> Phase 1b accepts every well-formed
/// non-empty purpose string. A future amendment may narrow this to
/// the
/// <c>Sunfish.UICore.Wayfinder.Integrations.IntegrationCapabilityPurposes</c>
/// taxonomy + similar registered taxonomies.
/// </para>
/// <para>
/// <b>TTL clamp:</b> the requested TTL is honored verbatim up to a
/// 30-minute ceiling. Longer TTLs are silently clamped — capabilities
/// living past 30 minutes defeat the just-in-time decrypt-capability
/// design intent.
/// </para>
/// </remarks>
public sealed class TenantKeyDecryptCapabilityProvider : IDecryptCapabilityProvider
{
    private static readonly TimeSpan MaxTtl = TimeSpan.FromMinutes(30);

    private readonly ITenantKeyProvider _tenantKeys;
    private readonly Sunfish.Foundation.Recovery.IRecoveryClock _clock;

    /// <summary>
    /// Construct the provider. <paramref name="clock"/> defaults to
    /// the system clock when null.
    /// </summary>
    public TenantKeyDecryptCapabilityProvider(
        ITenantKeyProvider tenantKeys,
        Sunfish.Foundation.Recovery.IRecoveryClock? clock = null)
    {
        _tenantKeys = tenantKeys ?? throw new ArgumentNullException(nameof(tenantKeys));
        _clock = clock ?? new Sunfish.Foundation.Recovery.SystemRecoveryClock();
    }

    /// <inheritdoc />
    public async Task<IDecryptCapability?> AcquireAsync(
        TenantId tenantId,
        string purpose,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return null;
        }
        if (ttl <= TimeSpan.Zero)
        {
            return null;
        }
        ct.ThrowIfCancellationRequested();

        ReadOnlyMemory<byte> derived;
        try
        {
            derived = await _tenantKeys.DeriveKeyAsync(tenantId, purpose, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        if (derived.IsEmpty)
        {
            return null;
        }

        var clamped = ttl > MaxTtl ? MaxTtl : ttl;
        var validUntil = _clock.UtcNow().Add(clamped);
        var capabilityId = $"tenant-key:{tenantId.Value}:{purpose}:{validUntil.ToUnixTimeSeconds()}";

        return new FixedDecryptCapability(
            capabilityId,
            ActorId.System,
            tenantId,
            validUntil);
    }
}
