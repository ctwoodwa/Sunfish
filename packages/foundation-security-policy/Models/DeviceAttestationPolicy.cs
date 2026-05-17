using System.Collections.ObjectModel;

namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Per-action-class accepted attestation tiers + watch-transfer
/// requirement per ADR 0068 §1.2.
/// <see cref="AcceptedTiersForReadActions"/> MUST contain every tier
/// in <see cref="AcceptedTiersForPrivilegedActions"/> (read posture
/// MUST NOT be stricter than privileged posture). The consistency
/// validator (subsequent PR) enforces via
/// <see cref="IsReadAtLeastAsPermissiveAsPrivileged"/>.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record DeviceAttestationPolicy(
    IReadOnlyList<AttestationTier> AcceptedTiersForPrivilegedActions,
    IReadOnlyList<AttestationTier> AcceptedTiersForReadActions,
    bool RequireAttestationForWatchTransfer)
{
    public static readonly DeviceAttestationPolicy Default = new(
        AcceptedTiersForPrivilegedActions: Array.AsReadOnly(new[]
        {
            AttestationTier.AppleSecureElement,
            AttestationTier.AndroidHardwareKeyStore,
            AttestationTier.Tpm2,
            AttestationTier.Fido2HardwareToken,
        }),
        AcceptedTiersForReadActions: Array.AsReadOnly(new[]
        {
            AttestationTier.SoftwareSandbox,
            AttestationTier.AppleSecureElement,
            AttestationTier.AndroidHardwareKeyStore,
            AttestationTier.Tpm2,
            AttestationTier.Fido2HardwareToken,
        }),
        RequireAttestationForWatchTransfer: true);

    /// <summary>
    /// True when <see cref="AcceptedTiersForReadActions"/> is a
    /// superset (or equal) of <see cref="AcceptedTiersForPrivilegedActions"/>
    /// — i.e., read posture is equally or less restrictive than
    /// privileged posture. Per §1.2.2.
    /// </summary>
    public bool IsReadAtLeastAsPermissiveAsPrivileged
        => AcceptedTiersForPrivilegedActions.All(t => AcceptedTiersForReadActions.Contains(t));
}
