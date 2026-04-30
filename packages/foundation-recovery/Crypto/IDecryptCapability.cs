using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// A capability granting decrypt access to one or more
/// <see cref="EncryptedField"/> values for a specific tenant.
/// Phase 1 reference impl is <see cref="FixedDecryptCapability"/>;
/// macaroon-bound capabilities (ADR 0032) are deferred.
/// </summary>
public interface IDecryptCapability
{
    /// <summary>
    /// Stable identifier for this capability. Logged in denial audit
    /// records and surfaced in <see cref="FieldDecryptionDeniedException"/>
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
