using System;
using System.Buffers.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Recovery;

/// <summary>
/// JSON converter for <see cref="EncryptedField"/>. Serializes as
/// <c>{ "ct": "&lt;base64url&gt;", "nonce": "&lt;base64url&gt;", "kv": &lt;int&gt; }</c>.
/// </summary>
internal sealed class EncryptedFieldJsonConverter : JsonConverter<EncryptedField>
{
    private const string CiphertextProperty = "ct";
    private const string NonceProperty = "nonce";
    private const string KeyVersionProperty = "kv";

    public override EncryptedField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of EncryptedField object.");
        }

        ReadOnlyMemory<byte>? ciphertext = null;
        ReadOnlyMemory<byte>? nonce = null;
        int? keyVersion = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (ciphertext is null || nonce is null || keyVersion is null)
                {
                    throw new JsonException("EncryptedField requires 'ct', 'nonce', and 'kv'.");
                }
                return new EncryptedField(ciphertext.Value, nonce.Value, keyVersion.Value);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected EncryptedField property name.");
            }

            var name = reader.GetString();
            reader.Read();

            switch (name)
            {
                case CiphertextProperty:
                    ciphertext = DecodeBase64Url(reader.GetString());
                    break;
                case NonceProperty:
                    nonce = DecodeBase64Url(reader.GetString());
                    break;
                case KeyVersionProperty:
                    keyVersion = reader.GetInt32();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        throw new JsonException("Unexpected end of JSON within EncryptedField.");
    }

    public override void Write(Utf8JsonWriter writer, EncryptedField value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(CiphertextProperty, EncodeBase64Url(value.Ciphertext.Span));
        writer.WriteString(NonceProperty, EncodeBase64Url(value.Nonce.Span));
        writer.WriteNumber(KeyVersionProperty, value.KeyVersion);
        writer.WriteEndObject();
    }

    private static string EncodeBase64Url(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }
        var standard = Convert.ToBase64String(bytes);
        return standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static ReadOnlyMemory<byte> DecodeBase64Url(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        var standard = input.Replace('-', '+').Replace('_', '/');
        var padding = standard.Length % 4;
        if (padding == 2)
        {
            standard += "==";
        }
        else if (padding == 3)
        {
            standard += "=";
        }
        else if (padding == 1)
        {
            throw new JsonException("Invalid base64url: malformed length.");
        }
        try
        {
            return Convert.FromBase64String(standard);
        }
        catch (FormatException ex)
        {
            throw new JsonException("Invalid base64url in EncryptedField.", ex);
        }
    }
}
