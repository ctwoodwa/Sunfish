using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Attestation;

/// <summary>
/// Default <see cref="IAttestationVerifier"/>. Rejects on signature mismatch,
/// expiry, issuer mismatch, or malformed field lengths.
/// </summary>
public sealed class AttestationVerifier : IAttestationVerifier
{
    private readonly IEd25519Signer _signer;

    /// <summary>Constructs a verifier bound to an Ed25519 primitive.</summary>
    public AttestationVerifier(IEd25519Signer signer)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    /// <inheritdoc />
    public bool Verify(RoleAttestation attestation, byte[] expectedIssuerPublicKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(attestation);
        ArgumentNullException.ThrowIfNull(expectedIssuerPublicKey);

        // Shape checks — treat any malformed field as failed verification rather than throwing.
        if (attestation.TeamId is null || attestation.TeamId.Length != RoleAttestation.TeamIdLength) return false;
        if (attestation.SubjectPublicKey is null || attestation.SubjectPublicKey.Length != RoleAttestation.PublicKeyLength) return false;
        if (attestation.IssuerPublicKey is null || attestation.IssuerPublicKey.Length != RoleAttestation.PublicKeyLength) return false;
        if (attestation.Signature is null || attestation.Signature.Length != RoleAttestation.SignatureLength) return false;
        if (string.IsNullOrEmpty(attestation.Role)) return false;
        if (expectedIssuerPublicKey.Length != RoleAttestation.PublicKeyLength) return false;

        // Issuer binding.
        if (!CryptographicEquals(attestation.IssuerPublicKey, expectedIssuerPublicKey)) return false;

        // Temporal validity — half-open interval [IssuedAt, ExpiresAt).
        if (now < attestation.IssuedAt || now >= attestation.ExpiresAt) return false;

        // Signature check.
        var signable = attestation.ToSignable();
        return _signer.Verify(signable, attestation.Signature, attestation.IssuerPublicKey);
    }

    private static bool CryptographicEquals(byte[] a, byte[] b)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
}
