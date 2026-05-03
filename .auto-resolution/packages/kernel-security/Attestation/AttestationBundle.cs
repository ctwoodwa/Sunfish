using System.Formats.Cbor;

namespace Sunfish.Kernel.Security.Attestation;

/// <summary>
/// Wire-format envelope carried in the sync daemon's <c>CAPABILITY_NEG</c>
/// <c>attestation_bundle</c> field (sync-daemon-protocol §3.2). A single bundle
/// may carry multiple attestations — e.g. a node that is both <c>team_member</c>
/// and <c>financial_role</c> attaches both to one bundle.
/// </summary>
public sealed record AttestationBundle(IReadOnlyList<RoleAttestation> Attestations)
{
    /// <summary>
    /// Serializes the bundle as a canonical CBOR array of attestation records.
    /// </summary>
    public byte[] ToCbor()
    {
        ArgumentNullException.ThrowIfNull(Attestations);

        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartArray(Attestations.Count);
        foreach (var a in Attestations)
        {
            // Inline the attestation fields rather than nesting ToCbor() bytes — the latter
            // would be double-encoded (CBOR inside a bstr). We want a flat CBOR tree for
            // downstream canonical-hash use cases.
            writer.WriteStartArray(7);
            writer.WriteByteString(a.TeamId);
            writer.WriteByteString(a.SubjectPublicKey);
            writer.WriteTextString(a.Role);
            writer.WriteInt64(a.IssuedAt.ToUnixTimeSeconds());
            writer.WriteInt64(a.ExpiresAt.ToUnixTimeSeconds());
            writer.WriteByteString(a.IssuerPublicKey);
            writer.WriteByteString(a.Signature);
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
        return writer.Encode();
    }

    /// <summary>Reads a bundle from its canonical CBOR encoding.</summary>
    /// <exception cref="CborContentException">The payload is malformed.</exception>
    public static AttestationBundle FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = new CborReader(cbor.ToArray(), CborConformanceMode.Canonical);
        var outerCount = reader.ReadStartArray();
        var list = new List<RoleAttestation>(outerCount ?? 0);

        while (reader.PeekState() != CborReaderState.EndArray)
        {
            var innerCount = reader.ReadStartArray();
            if (innerCount != 7)
            {
                throw new CborContentException(
                    $"RoleAttestation CBOR must have exactly 7 fields (got {innerCount}).");
            }

            var teamId = reader.ReadByteString();
            var subject = reader.ReadByteString();
            var role = reader.ReadTextString();
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64());
            var issuer = reader.ReadByteString();
            var signature = reader.ReadByteString();
            reader.ReadEndArray();

            list.Add(new RoleAttestation(teamId, subject, role, issuedAt, expiresAt, issuer, signature));
        }

        reader.ReadEndArray();
        return new AttestationBundle(list);
    }
}
