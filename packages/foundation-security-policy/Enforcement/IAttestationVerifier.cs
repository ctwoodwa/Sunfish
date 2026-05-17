using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Enforcement;

/// <summary>
/// Per-platform attestation-evidence verifier per ADR 0068 §4.3. The
/// interface is declared in Phase 1 to establish the contract; per-
/// platform implementations (Apple Secure Enclave, Android Keystore,
/// Windows TPM 2.0, FIDO2 hardware token) ship in Phase 2.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Verifiers MUST fail closed: an <see cref="AttestationEvidence"/>
/// claiming a tier above <see cref="AttestationTier.SoftwareSandbox"/>
/// with empty / null platform proof MUST return
/// <c>IsVerified: false</c>. The policy layer does NOT trust caller-
/// supplied <see cref="AttestationEvidence.Tier"/> values for hardware
/// tiers (§1.2.1).
/// </para>
/// </remarks>
public interface IAttestationVerifier
{
    /// <summary>The attestation tier this verifier knows how to prove.</summary>
    AttestationTier SupportedTier { get; }

    /// <summary>
    /// Verify the supplied platform-attestation proof against the
    /// platform's attestation root. MUST be side-effect free.
    /// </summary>
    ValueTask<AttestationVerificationResult> VerifyAsync(
        System.ReadOnlyMemory<byte> platformProof,
        System.DateTimeOffset evidenceAt,
        System.Threading.CancellationToken cancellationToken = default);
}
