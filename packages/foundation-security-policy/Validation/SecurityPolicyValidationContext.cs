using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Validation;

/// <summary>
/// Per-validation context per ADR 0068 §2. Carries the proposing
/// tenant + actor + the actor's <see cref="ShipRole"/> so validators
/// can apply role-aware rules without re-fetching from the
/// permission resolver.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record SecurityPolicyValidationContext(
    TenantId TenantId,
    ActorId Proposer,
    ShipRole ProposerRole);
