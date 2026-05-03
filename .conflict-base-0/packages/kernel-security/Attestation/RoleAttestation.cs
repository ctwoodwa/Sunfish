using System.Formats.Cbor;

namespace Sunfish.Kernel.Security.Attestation;

/// <summary>
/// Ed25519-signed claim that a node (identified by <paramref name="SubjectPublicKey"/>)
/// holds <paramref name="Role"/> in the team identified by <paramref name="TeamId"/>.
/// </summary>
/// <remarks>
/// <para>
/// Paper §11.3: attestations prove role membership for sync-capability negotiation.
/// They do <b>not</b> generate encryption keys — that is a separate flow handled by
/// <c>IRoleKeyManager</c>.
/// </para>
/// <para>
/// Signature coverage: the issuer signs a canonical CBOR encoding of the tuple
/// <c>(TeamId, SubjectPublicKey, Role, IssuedAt, ExpiresAt, IssuerPublicKey)</c>.
/// "Canonical" here means CTAP2 / RFC 8949 §4.2 deterministic encoding:
/// definite-length items, shortest integer encoding, UTF-8 strings, and fixed field
/// order (array, not map — a map key ordering argument would be a footgun on an
/// evolving schema).
/// </para>
/// </remarks>
/// <param name="TeamId">16-byte team identifier.</param>
/// <param name="SubjectPublicKey">32-byte Ed25519 public key of the attested node.</param>
/// <param name="Role">Role token — e.g. <c>team_member</c>, <c>financial_role</c>, <c>admin</c>.</param>
/// <param name="IssuedAt">Issuance time (UTC). Serialized as Unix seconds.</param>
/// <param name="ExpiresAt">Expiry time (UTC). Serialized as Unix seconds. Must be &gt; <paramref name="IssuedAt"/>.</param>
/// <param name="IssuerPublicKey">32-byte Ed25519 public key of the issuing admin.</param>
/// <param name="Signature">64-byte Ed25519 detached signature over the canonical CBOR encoding of the preceding fields.</param>
public sealed record RoleAttestation(
    byte[] TeamId,
    byte[] SubjectPublicKey,
    string Role,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    byte[] IssuerPublicKey,
    byte[] Signature)
{
    /// <summary>Team-ID length in bytes.</summary>
    public const int TeamIdLength = 16;

    /// <summary>Ed25519 public-key length in bytes.</summary>
    public const int PublicKeyLength = 32;

    /// <summary>Ed25519 signature length in bytes.</summary>
    public const int SignatureLength = 64;

    /// <summary>
    /// Serializes the signed fields to a canonical CBOR byte sequence. The result is
    /// deterministic: the same input tuple always produces the same bytes.
    /// </summary>
    public byte[] ToSignable()
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartArray(6);
        writer.WriteByteString(TeamId);
        writer.WriteByteString(SubjectPublicKey);
        writer.WriteTextString(Role);
        writer.WriteInt64(IssuedAt.ToUnixTimeSeconds());
        writer.WriteInt64(ExpiresAt.ToUnixTimeSeconds());
        writer.WriteByteString(IssuerPublicKey);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>
    /// Serializes the full attestation (signed fields + signature) as canonical CBOR.
    /// </summary>
    public byte[] ToCbor()
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartArray(7);
        writer.WriteByteString(TeamId);
        writer.WriteByteString(SubjectPublicKey);
        writer.WriteTextString(Role);
        writer.WriteInt64(IssuedAt.ToUnixTimeSeconds());
        writer.WriteInt64(ExpiresAt.ToUnixTimeSeconds());
        writer.WriteByteString(IssuerPublicKey);
        writer.WriteByteString(Signature);
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>Reads an attestation from its canonical CBOR encoding.</summary>
    /// <exception cref="CborContentException">The payload is malformed.</exception>
    public static RoleAttestation FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = new CborReader(cbor.ToArray(), CborConformanceMode.Canonical);
        var count = reader.ReadStartArray();
        if (count != 7)
        {
            throw new CborContentException(
                $"RoleAttestation CBOR must have exactly 7 fields (got {count}).");
        }

        var teamId = reader.ReadByteString();
        var subject = reader.ReadByteString();
        var role = reader.ReadTextString();
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());
        var issuer = reader.ReadByteString();
        var signature = reader.ReadByteString();
        reader.ReadEndArray();

        return new RoleAttestation(teamId, subject, role, issuedAt, expiresAt, issuer, signature);
    }
}
