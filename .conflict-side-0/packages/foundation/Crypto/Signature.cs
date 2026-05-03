using System.Buffers.Text;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Crypto;

/// <summary>
/// An Ed25519 signature — a 64-byte detached signature over a canonical-JSON signable envelope.
/// </summary>
/// <remarks>
/// Like <see cref="PrincipalId"/>, this type provides value equality over its bytes and will
/// throw <see cref="NullReferenceException"/> from members on a default-constructed instance.
/// Construct via <see cref="FromBytes"/> or <see cref="IOperationSigner"/>.
/// </remarks>
[JsonConverter(typeof(SignatureJsonConverter))]
public readonly record struct Signature
{
    /// <summary>Ed25519 signature length in bytes (64).</summary>
    public const int LengthInBytes = 64;

    private readonly byte[] _bytes;

    private Signature(byte[] bytes)
    {
        _bytes = bytes;
    }

    /// <summary>Constructs a signature from a 64-byte Ed25519 signature (defensively copied).</summary>
    public static Signature FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != LengthInBytes)
            throw new ArgumentException($"Signature requires exactly {LengthInBytes} bytes.", nameof(bytes));
        return new Signature(bytes.ToArray());
    }

    /// <summary>Returns the raw signature bytes as a read-only span.</summary>
    public ReadOnlySpan<byte> AsSpan() => _bytes;

    /// <summary>Returns the base64url (unpadded) string form of the signature.</summary>
    public string ToBase64Url()
    {
        Span<byte> encoded = stackalloc byte[Base64Url.GetEncodedLength(LengthInBytes)];
        Base64Url.EncodeToUtf8(_bytes, encoded, out _, out var written);
        return Encoding.UTF8.GetString(encoded[..written]);
    }

    /// <summary>Parses the base64url string form back into a signature.</summary>
    public static Signature FromBase64Url(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var utf8 = Encoding.UTF8.GetBytes(value);
        Span<byte> decoded = stackalloc byte[LengthInBytes];
        var status = Base64Url.DecodeFromUtf8(utf8, decoded, out _, out var written);
        if (status != System.Buffers.OperationStatus.Done || written != LengthInBytes)
            throw new FormatException("Signature base64url value is not a valid 64-byte signature.");
        return new Signature(decoded.ToArray());
    }

    /// <summary>Value equality over the raw bytes (not reference equality).</summary>
    public bool Equals(Signature other)
        => _bytes.AsSpan().SequenceEqual(other._bytes.AsSpan());

    /// <summary>Hash code derived from the raw bytes; stable across instances with equal content.</summary>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.AddBytes(_bytes);
        return hash.ToHashCode();
    }

    /// <summary>String form for diagnostics — the base64url signature.</summary>
    public override string ToString() => ToBase64Url();
}

internal sealed class SignatureJsonConverter : JsonConverter<Signature>
{
    public override Signature Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString() ?? throw new JsonException("Signature must be a base64url string.");
        return Signature.FromBase64Url(str);
    }

    public override void Write(Utf8JsonWriter writer, Signature value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToBase64Url());
    }
}
