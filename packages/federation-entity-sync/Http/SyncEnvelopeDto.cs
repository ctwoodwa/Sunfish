using System.Globalization;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync.Http;

/// <summary>
/// Wire-format DTO for <see cref="SyncEnvelope"/>. <see cref="SyncEnvelope"/> carries a
/// <see cref="ReadOnlyMemory{T}"/> payload plus <see cref="PrincipalId"/> / <see cref="Signature"/>
/// structs whose byte-array backing fields are private — <see cref="System.Text.Json.JsonSerializer"/>
/// cannot round-trip them with its default options. This DTO encodes the binary fields as base64
/// strings and uses plain primitives for the identifiers so default JSON serialization works.
/// </summary>
/// <remarks>
/// <para>
/// The envelope signature is computed over the in-memory CLR fields (via
/// <c>EnvelopeSigning.Sign</c>, which canonicalizes via <c>CanonicalJson.SerializeSignable</c>),
/// not over this DTO shape. Round-tripping via the DTO therefore does not invalidate the signature,
/// as long as the encoded fields preserve every byte of information the signature covers.
/// </para>
/// <para>
/// <see cref="SentAt"/> is encoded in ISO 8601 round-trippable "O" format rather than Unix
/// milliseconds because <c>CanonicalJson.SerializeSignable</c> emits <c>issuedAt</c> in the same
/// round-trippable "O" form (with sub-millisecond tick precision). Using Unix milliseconds here
/// would truncate the <see cref="DateTimeOffset"/> precision and cause signature verification to
/// fail after a DTO round-trip.
/// </para>
/// </remarks>
/// <param name="Id">Guid in 32-hex "N" form.</param>
/// <param name="FromPeer">The sender peer id (base64url public key).</param>
/// <param name="ToPeer">The recipient peer id (base64url public key).</param>
/// <param name="Kind">The <see cref="SyncMessageKind"/> enum name.</param>
/// <param name="SentAt">Envelope send time in ISO 8601 "O" (round-trippable) format, UTC.</param>
/// <param name="Nonce">Envelope nonce in 32-hex "N" form.</param>
/// <param name="PayloadBase64">Base64 (standard, not URL) of the payload bytes.</param>
/// <param name="SignatureBase64Url">Base64url (no padding) of the 64-byte Ed25519 signature.</param>
internal sealed record SyncEnvelopeDto(
    string Id,
    string FromPeer,
    string ToPeer,
    string Kind,
    string SentAt,
    string Nonce,
    string PayloadBase64,
    string SignatureBase64Url)
{
    /// <summary>Projects an in-memory <see cref="SyncEnvelope"/> onto the wire DTO.</summary>
    public static SyncEnvelopeDto From(SyncEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return new SyncEnvelopeDto(
            Id: envelope.Id.Value.ToString("N"),
            FromPeer: envelope.FromPeer.Value,
            ToPeer: envelope.ToPeer.Value,
            Kind: envelope.Kind.ToString(),
            SentAt: envelope.SentAt.ToString("O", CultureInfo.InvariantCulture),
            Nonce: envelope.Nonce.Value.ToString("N"),
            PayloadBase64: Convert.ToBase64String(envelope.Payload.Span),
            SignatureBase64Url: envelope.Signature.ToBase64Url());
    }

    /// <summary>Reconstitutes the in-memory <see cref="SyncEnvelope"/> from the DTO.</summary>
    /// <exception cref="FormatException">Thrown when any encoded field is malformed.</exception>
    public SyncEnvelope ToEnvelope()
    {
        if (!Enum.TryParse<SyncMessageKind>(Kind, ignoreCase: false, out var kind))
            throw new FormatException($"Unknown SyncMessageKind '{Kind}'.");

        var idGuid = Guid.ParseExact(Id, "N");
        var nonceGuid = Guid.ParseExact(Nonce, "N");
        var payload = Convert.FromBase64String(PayloadBase64);
        var signature = Signature.FromBase64Url(SignatureBase64Url);
        var sentAt = DateTimeOffset.ParseExact(
            SentAt, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        return new SyncEnvelope(
            Id: new SyncMessageId(idGuid),
            FromPeer: new PeerId(FromPeer),
            ToPeer: new PeerId(ToPeer),
            Kind: kind,
            SentAt: sentAt,
            Nonce: new Nonce(nonceGuid),
            Payload: payload,
            Signature: signature);
    }
}
