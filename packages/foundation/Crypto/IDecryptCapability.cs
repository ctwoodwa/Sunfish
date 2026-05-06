using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// A capability granting decrypt access to one or more
/// <c>Sunfish.Foundation.Recovery.EncryptedField</c> values for a
/// specific tenant. Phase 1 reference impl is
/// <c>Sunfish.Foundation.Recovery.Crypto.FixedDecryptCapability</c>;
/// macaroon-bound capabilities (ADR 0032) are deferred.
/// </summary>
/// <remarks>
/// W#48 Phase 1.5 PR 2: relocated from
/// <c>Sunfish.Foundation.Recovery.Crypto</c> to
/// <c>Sunfish.Foundation.Crypto</c> (sibling to <see cref="KeyFingerprint"/>
/// + <see cref="PrincipalId"/>) to break the
/// <c>ui-core → foundation-recovery → kernel-security → ui-core</c>
/// cycle. After this move,
/// <c>Sunfish.UICore.Wayfinder.Integrations.IDecryptCapabilityProvider</c>
/// (Phase 1 full follow-up) can return <see cref="IDecryptCapability"/>
/// without dragging in <c>foundation-recovery</c>.
/// Concrete implementations (<c>FixedDecryptCapability</c> +
/// <c>TenantKeyProviderFieldDecryptor</c>) stay in
/// <c>foundation-recovery</c> — only the interface contract moved.
/// </remarks>
public interface IDecryptCapability
{
    /// <summary>
    /// Stable identifier for this capability. Logged in denial audit
    /// records and surfaced in
    /// <c>Sunfish.Foundation.Recovery.Crypto.FieldDecryptionDeniedException</c>
    /// so a denial can be traced to the specific issuing capability.
    /// </summary>
    string CapabilityId { get; }

    /// <summary>
    /// Returns <c>null</c> when the capability is valid for
    /// <paramref name="targetTenant"/> at <paramref name="now"/>;
    /// otherwise returns a short rejection reason (e.g.
    /// <c>"expired"</c>, <c>"wrong-tenant"</c>) which is recorded in
    /// the denial audit payload.
    /// </summary>
    string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now);
}
