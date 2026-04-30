using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Reference <see cref="IDecryptCapability"/> bound to a single tenant +
/// actor with an explicit expiry. Phase 1 substrate per ADR 0046-A3;
/// the macaroon-bound flavor (ADR 0032) lands in a follow-up.
/// </summary>
public sealed class FixedDecryptCapability : IDecryptCapability
{
    public FixedDecryptCapability(string capabilityId, ActorId actor, TenantId tenant, DateTimeOffset validUntil)
    {
        if (string.IsNullOrWhiteSpace(capabilityId))
        {
            throw new ArgumentException("CapabilityId must be non-empty.", nameof(capabilityId));
        }
        CapabilityId = capabilityId;
        Actor = actor;
        Tenant = tenant;
        ValidUntil = validUntil;
    }

    public string CapabilityId { get; }

    public ActorId Actor { get; }

    public TenantId Tenant { get; }

    public DateTimeOffset ValidUntil { get; }

    public string? ValidateForDecrypt(TenantId targetTenant, DateTimeOffset now)
    {
        if (!Tenant.Equals(targetTenant))
        {
            return "wrong-tenant";
        }
        if (now > ValidUntil)
        {
            return "expired";
        }
        return null;
    }
}
