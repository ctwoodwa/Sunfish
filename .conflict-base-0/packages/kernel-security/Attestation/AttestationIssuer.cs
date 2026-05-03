using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Attestation;

/// <summary>
/// Default <see cref="IAttestationIssuer"/>. Signs the canonical CBOR encoding of
/// the attested tuple with the caller-supplied Ed25519 private key.
/// </summary>
public sealed class AttestationIssuer : IAttestationIssuer
{
    private readonly IEd25519Signer _signer;
    private readonly TimeProvider _clock;

    /// <summary>Constructs an issuer bound to an Ed25519 signer and a clock.</summary>
    public AttestationIssuer(IEd25519Signer signer, TimeProvider? clock = null)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _clock = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public RoleAttestation Issue(
        byte[] teamId,
        byte[] subjectPublicKey,
        string role,
        TimeSpan validity,
        ReadOnlyMemory<byte> issuerPrivateKey)
    {
        ArgumentNullException.ThrowIfNull(teamId);
        ArgumentNullException.ThrowIfNull(subjectPublicKey);
        ArgumentException.ThrowIfNullOrEmpty(role);
        if (teamId.Length != RoleAttestation.TeamIdLength)
        {
            throw new ArgumentException(
                $"TeamId must be {RoleAttestation.TeamIdLength} bytes (was {teamId.Length}).",
                nameof(teamId));
        }
        if (subjectPublicKey.Length != RoleAttestation.PublicKeyLength)
        {
            throw new ArgumentException(
                $"Subject public key must be {RoleAttestation.PublicKeyLength} bytes (was {subjectPublicKey.Length}).",
                nameof(subjectPublicKey));
        }
        if (validity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validity), "Attestation validity must be positive.");
        }

        var issuedAt = _clock.GetUtcNow();
        var expiresAt = issuedAt + validity;

        // Derive the issuer's public key from the private key seed. We do this by
        // asking the signer — an alternative would be to carry the public key on
        // IAttestationIssuer.Issue, but the paper flow (admin holds the keypair)
        // makes the derivation-from-seed model simpler at the call site.
        var issuerPublic = DeriveEd25519PublicKey(issuerPrivateKey.Span);

        // Build the signable payload (6 fields: everything except the signature).
        var stub = new RoleAttestation(
            TeamId: teamId,
            SubjectPublicKey: subjectPublicKey,
            Role: role,
            IssuedAt: issuedAt,
            ExpiresAt: expiresAt,
            IssuerPublicKey: issuerPublic,
            Signature: Array.Empty<byte>());

        var signable = stub.ToSignable();
        var signature = _signer.Sign(signable, issuerPrivateKey.Span);

        return stub with { Signature = signature };
    }

    private static byte[] DeriveEd25519PublicKey(ReadOnlySpan<byte> privateKeySeed)
    {
        var creationParams = new NSec.Cryptography.KeyCreationParameters
        {
            ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport,
        };
        using var key = NSec.Cryptography.Key.Import(
            NSec.Cryptography.SignatureAlgorithm.Ed25519,
            privateKeySeed,
            NSec.Cryptography.KeyBlobFormat.RawPrivateKey,
            in creationParams);
        return key.PublicKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);
    }
}
