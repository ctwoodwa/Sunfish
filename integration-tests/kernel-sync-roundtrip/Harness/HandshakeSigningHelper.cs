using System.Formats.Cbor;

namespace Sunfish.Integration.KernelSyncRoundtrip.Harness;

/// <summary>
/// Test-side replica of <c>HandshakeProtocol.BuildSigningPayload</c>. The
/// production helper is <c>internal</c> to <see cref="HandshakeProtocol"/>;
/// this integration-test project sits outside the kernel-sync
/// InternalsVisibleTo allowlist, so we rebuild the canonical CBOR payload
/// byte-identically per sync-daemon-protocol §8.
/// </summary>
internal static class HandshakeSigningHelper
{
    /// <summary>
    /// Canonical CBOR 4-element array <c>[node_id, schema_version, public_key, sent_at]</c>
    /// — exactly matches <c>HandshakeProtocol.BuildSigningPayload</c> so a
    /// signature produced here verifies on the peer's real
    /// <c>VerifyHelloSignature</c>.
    /// </summary>
    public static byte[] BuildSigningPayload(
        byte[] nodeId,
        string schemaVersion,
        byte[] publicKey,
        ulong sentAt)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartArray(4);
        writer.WriteByteString(nodeId);
        writer.WriteTextString(schemaVersion);
        writer.WriteByteString(publicKey);
        writer.WriteUInt64(sentAt);
        writer.WriteEndArray();
        return writer.Encode();
    }
}
