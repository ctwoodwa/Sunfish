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
/// <b>Purpose allowlist (fail-closed):</b> Phase 1b accepts ONLY
/// purposes in the <see cref="AcceptedPurposes"/> set —
/// currently <c>"integration-validation"</c>. Requests with any
/// other purpose return <c>null</c> per the
/// <see cref="IDecryptCapabilityProvider.AcquireAsync"/> fail-closed
/// contract. A future amendment may extend the allowlist; the
/// default is deliberately narrow so a misuse of an off-allowlist
/// purpose surfaces as a deterministic deny rather than a
/// silently-issued mismatched-purpose capability.
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

    /// <summary>
    /// Closed allowlist of purposes this Phase 1b implementation
    /// accepts. Requests for any other purpose return <c>null</c>
    /// per the fail-closed contract. Extend via ADR amendment when
    /// new capability-acquisition flows ship.
    /// </summary>
    public static readonly System.Collections.Generic.IReadOnlySet<string> AcceptedPurposes =
        new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal)
        {
            "integration-validation",
        };

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
        if (!AcceptedPurposes.Contains(purpose))
        {
            // M2 fail-closed: Phase 1b only honors purposes on the
            // allowlist. Off-allowlist purposes return null so a
            // mistakenly-requested purpose can never be issued an
            // unrelated key-domain's capability.
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
