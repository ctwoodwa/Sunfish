using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync.Protocol;

/// <summary>
/// Wire-format DTO for a <see cref="SignedOperation{ChangeRecord}"/>. The in-memory representation
/// uses <see cref="PrincipalId"/> / <see cref="Signature"/> structs whose byte-array backing fields
/// are private — <see cref="System.Text.Json.JsonSerializer"/> cannot serialize them without custom
/// converters. This DTO encodes the envelope fields as base64url strings and lets the default JSON
/// serializer round-trip cleanly. Conversion at the wire boundary does not affect signature
/// verification: signatures are computed over the CLR <see cref="ChangeRecord"/> via
/// <see cref="CanonicalJson"/>, not over this DTO shape.
/// </summary>
/// <param name="Payload">The domain change record being signed.</param>
/// <param name="IssuerId">Base64url-encoded issuer public key.</param>
/// <param name="IssuedAt">Wall-clock time the envelope was issued.</param>
/// <param name="Nonce">The per-issuance nonce used when signing.</param>
/// <param name="Signature">Base64url-encoded Ed25519 signature.</param>
public sealed record SignedChangeRecordDto(
    ChangeRecord Payload,
    string IssuerId,
    DateTimeOffset IssuedAt,
    Guid Nonce,
    string Signature)
{
    /// <summary>Projects an in-memory <see cref="SignedOperation{ChangeRecord}"/> onto the wire DTO.</summary>
    public static SignedChangeRecordDto FromSigned(SignedOperation<ChangeRecord> op)
    {
        ArgumentNullException.ThrowIfNull(op);
        return new SignedChangeRecordDto(
            op.Payload,
            op.IssuerId.ToBase64Url(),
            op.IssuedAt,
            op.Nonce,
            op.Signature.ToBase64Url());
    }

    /// <summary>Reconstitutes the in-memory <see cref="SignedOperation{ChangeRecord}"/> from the DTO.</summary>
    public SignedOperation<ChangeRecord> ToSigned()
        => new(
            Payload,
            PrincipalId.FromBase64Url(IssuerId),
            IssuedAt,
            Nonce,
            Sunfish.Foundation.Crypto.Signature.FromBase64Url(Signature));
}
