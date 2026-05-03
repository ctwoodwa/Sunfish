using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.Common;

/// <summary>
/// Internal helpers that sign and verify <see cref="SyncEnvelope"/> instances by routing through
/// the Foundation <see cref="IOperationSigner"/> / <see cref="IOperationVerifier"/> contract.
/// </summary>
/// <remarks>
/// <para>
/// The signer/verifier contract takes a generic payload and canonical-JSON serializes
/// it alongside a header (issuerId, issuedAt, nonce). To sign envelope header+payload bytes through
/// that interface, we wrap the raw signable bytes in a <see cref="SignablePayload"/> wrapper whose
/// canonical-JSON representation is deterministic. Both Sign and Verify reconstruct the same wrapper
/// from the same inputs, so the computed signable bytes round-trip bit-for-bit.
/// </para>
/// <para>
/// The envelope's signable bytes concatenate a canonical-JSON header (id, from, to, kind, sentAt-ms,
/// nonce) with the raw payload bytes. This keeps the payload's bytes unmodified on the wire while
/// still binding the signature to all header fields. Any tamper to header or payload fails verify.
/// </para>
/// </remarks>
internal static class EnvelopeSigning
{
    /// <summary>
    /// Wrapper record used only to route envelope-signable bytes through
    /// <see cref="IOperationSigner.SignAsync{T}"/>. Its canonical-JSON form is deterministic
    /// across both signing and verification paths.
    /// </summary>
    /// <param name="ContentBase64">Base64-encoded signable bytes.</param>
    internal sealed record SignablePayload(string ContentBase64);

    /// <summary>
    /// Computes the envelope-level signable bytes: canonical-JSON header concatenated with the
    /// raw payload buffer.
    /// </summary>
    public static byte[] ComputeSignableBytes(
        SyncMessageId id,
        PeerId from,
        PeerId to,
        SyncMessageKind kind,
        DateTimeOffset sentAt,
        Nonce nonce,
        ReadOnlyMemory<byte> payload)
    {
        var header = new
        {
            id = id.Value.ToString("N"),
            from = from.Value,
            to = to.Value,
            kind = kind.ToString(),
            sentAt = sentAt.ToUnixTimeMilliseconds(),
            nonce = nonce.Value.ToString("N"),
        };
        var headerBytes = CanonicalJson.Serialize(header);
        var combined = new byte[headerBytes.Length + payload.Length];
        headerBytes.CopyTo(combined, 0);
        payload.Span.CopyTo(combined.AsSpan(headerBytes.Length));
        return combined;
    }

    /// <summary>
    /// Signs a new envelope using <paramref name="signer"/>'s Ed25519 key. The signer's
    /// <see cref="IOperationSigner.IssuerId"/> is used as the <c>FromPeer</c>.
    /// </summary>
    public static SyncEnvelope Sign(
        IOperationSigner signer,
        PeerId toPeer,
        SyncMessageKind kind,
        ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(signer);

        var id = SyncMessageId.NewId();
        var from = PeerId.From(signer.IssuerId);
        var sentAt = DateTimeOffset.UtcNow;
        var nonce = Nonce.NewNonce();

        var signable = ComputeSignableBytes(id, from, toPeer, kind, sentAt, nonce, payload);
        var wrapper = new SignablePayload(Convert.ToBase64String(signable));

        // Envelope signing is off the hot path; a sync call into the ValueTask signer is acceptable
        // to keep the envelope factory API synchronous.
        var signed = signer.SignAsync(wrapper, sentAt, nonce.Value).AsTask().GetAwaiter().GetResult();

        return new SyncEnvelope(id, from, toPeer, kind, sentAt, nonce, payload, signed.Signature);
    }

    /// <summary>
    /// Verifies that <paramref name="env"/>'s signature was produced by <paramref name="expectedSigner"/>.
    /// </summary>
    public static bool Verify(IOperationVerifier verifier, SyncEnvelope env, PrincipalId expectedSigner)
    {
        ArgumentNullException.ThrowIfNull(verifier);
        ArgumentNullException.ThrowIfNull(env);

        var signable = ComputeSignableBytes(env.Id, env.FromPeer, env.ToPeer, env.Kind, env.SentAt, env.Nonce, env.Payload);
        var wrapper = new SignablePayload(Convert.ToBase64String(signable));
        var reconstructed = new SignedOperation<SignablePayload>(
            Payload: wrapper,
            IssuerId: expectedSigner,
            IssuedAt: env.SentAt,
            Nonce: env.Nonce.Value,
            Signature: env.Signature);
        return verifier.Verify(reconstructed);
    }
}
