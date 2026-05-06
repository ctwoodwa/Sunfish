using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// Acquires a short-lived <see cref="IDecryptCapability"/> for a
/// specific tenant + purpose per ADR 0067 §3.14 + §5.3.1. Used by
/// <c>Sunfish.UICore.Wayfinder.Integrations.IIntegrationAtlasProvider.ValidateProviderAsync</c>
/// and similar capability-sourcing flows that must decrypt a
/// stored credential just-in-time without holding the
/// underlying tenant DEK in memory beyond the capability's
/// time-to-live.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cycle-safe placement:</b> the interface lives in
/// <c>Sunfish.Foundation.Crypto</c> alongside
/// <see cref="IDecryptCapability"/> and
/// <see cref="KeyFingerprint"/> so <c>ui-core</c> can return
/// values of this type without referencing
/// <c>foundation-recovery</c>. The concrete implementation
/// (<c>TenantKeyDecryptCapabilityProvider</c>) lives in
/// <c>foundation-recovery</c> and is registered via
/// <c>AddSunfishRecoveryCoordinator()</c>; the
/// <c>AddSunfishIntegrationAtlas()</c> guard rejects host wiring
/// that omits the recovery registration.
/// </para>
/// <para>
/// <b>Fail-closed contract:</b> implementations MUST return
/// <c>null</c> rather than throwing when the requested capability
/// cannot be issued (no DEK available, purpose denied by host
/// policy, tenant unknown). Callers receiving <c>null</c>
/// surface a deterministic
/// <c>ProviderValidationStatus.Unknown</c> outcome with
/// <c>ErrorCode = "no-decrypt-capability"</c> per ADR 0067 §5.3.1
/// — they do NOT fall back to caller-supplied DEKs, retry without
/// authority, or read raw credential bytes from any other source.
/// </para>
/// <para>
/// <b>TTL semantics:</b> the returned capability's
/// <see cref="IDecryptCapability.ValidateForDecrypt"/> rejects
/// reads past the TTL boundary; callers MUST NOT cache the
/// capability beyond the TTL window or share it across tenants.
/// </para>
/// </remarks>
public interface IDecryptCapabilityProvider
{
    /// <summary>
    /// Acquire a capability authorizing decrypt access to
    /// <paramref name="tenantId"/>'s field-encryption material for
    /// the named <paramref name="purpose"/>, valid for at most
    /// <paramref name="ttl"/>. Returns <c>null</c> if the
    /// capability cannot be issued (host policy denial, missing
    /// DEK, tenant unknown).
    /// </summary>
    /// <param name="tenantId">Target tenant.</param>
    /// <param name="purpose">
    /// Stable purpose key drawn from
    /// <c>Sunfish.UICore.Wayfinder.Integrations.IntegrationCapabilityPurposes</c>
    /// (e.g., <c>"integration-validation"</c>) or another
    /// well-known purpose taxonomy. Implementations MAY refuse
    /// purposes outside their allowlist.
    /// </param>
    /// <param name="ttl">
    /// Maximum lifetime for the issued capability. Implementations
    /// MAY return a capability with a shorter actual TTL (the
    /// requested value is an upper bound, not a guarantee).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<IDecryptCapability?> AcquireAsync(
        TenantId tenantId,
        string purpose,
        TimeSpan ttl,
        CancellationToken ct = default);
}
