using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.Common;

/// <summary>
/// Wire-format envelope for a single federation sync message. The envelope is signed by the
/// sender's Ed25519 key; payloads carried inside typically contain already-signed operations
/// from Phase B. Double-signing is intentional — payload signature proves authorship;
/// envelope signature proves the transport hop.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Payload"/> is a <see cref="ReadOnlyMemory{T}"/> of bytes. Record-synthesized equality
/// will compare the underlying buffer by reference, NOT by content. If tests need to assert
/// structural equality of two envelopes they must compare fields individually (or unpack the
/// payload via <see cref="ReadOnlyMemory{T}.ToArray"/>).
/// </para>
/// <para>
/// Use <see cref="SignAndCreate"/> to build a signed envelope. Use <see cref="Verify"/> to verify
/// the envelope signature against an expected signer's <see cref="PrincipalId"/>.
/// </para>
/// </remarks>
/// <param name="Id">Unique per-envelope identifier.</param>
/// <param name="FromPeer">The sending peer's id.</param>
/// <param name="ToPeer">The intended recipient peer's id.</param>
/// <param name="Kind">The kind of sync message carried in <see cref="Payload"/>.</param>
/// <param name="SentAt">The wall-clock time at which the sender produced the envelope.</param>
/// <param name="Nonce">A unique per-envelope nonce used to prevent replay.</param>
/// <param name="Payload">The opaque payload bytes, interpretation determined by <see cref="Kind"/>.</param>
/// <param name="Signature">Ed25519 signature covering the header + payload bytes.</param>
public sealed record SyncEnvelope(
    SyncMessageId Id,
    PeerId FromPeer,
    PeerId ToPeer,
    SyncMessageKind Kind,
    DateTimeOffset SentAt,
    Nonce Nonce,
    ReadOnlyMemory<byte> Payload,
    Signature Signature)
{
    /// <summary>
    /// Signs a payload on behalf of the signer and produces a fully-populated envelope. Generates
    /// a new <see cref="SyncMessageId"/> and <see cref="Nonce"/>, stamps <see cref="SentAt"/> with
    /// <see cref="DateTimeOffset.UtcNow"/>, and derives <see cref="FromPeer"/> from the signer.
    /// </summary>
    public static SyncEnvelope SignAndCreate(
        IOperationSigner signer,
        PeerId toPeer,
        SyncMessageKind kind,
        ReadOnlyMemory<byte> payload)
        => EnvelopeSigning.Sign(signer, toPeer, kind, payload);

    /// <summary>
    /// Verifies that this envelope's signature was produced by <paramref name="expectedSigner"/>.
    /// </summary>
    /// <remarks>
    /// Returns <c>false</c> for any tamper: altered payload, altered header fields, or wrong signer.
    /// </remarks>
    public bool Verify(IOperationVerifier verifier, PrincipalId expectedSigner)
        => EnvelopeSigning.Verify(verifier, this, expectedSigner);
}

/// <summary>
/// Discriminator for the kind of message carried inside a <see cref="SyncEnvelope"/>. Receivers
/// dispatch to the correct handler based on this value.
/// </summary>
public enum SyncMessageKind
{
    /// <summary>Sender announces its current entity head state for a set of entities.</summary>
    EntityHeadsAnnouncement,

    /// <summary>Sender requests the changes needed to catch up to the receiver's heads.</summary>
    EntityChangesRequest,

    /// <summary>Sender returns the operations required to satisfy a prior request.</summary>
    EntityChangesResponse,

    /// <summary>Sender announces a capability-graph digest for RIBLT reconciliation.</summary>
    CapabilityDigest,

    /// <summary>RIBLT-encoded capability-graph delta.</summary>
    CapabilityRibltEncoded,

    /// <summary>Full capability-graph snapshot (fallback when RIBLT cannot converge).</summary>
    CapabilityFullSet,

    /// <summary>Sender requests the receiver pin a set of blob CIDs.</summary>
    BlobPinRequest,

    /// <summary>Sender announces blob-pin attestations signed by the local peer.</summary>
    BlobAttestationBroadcast,

    /// <summary>Liveness probe — receivers echo back.</summary>
    HealthProbe,
}
