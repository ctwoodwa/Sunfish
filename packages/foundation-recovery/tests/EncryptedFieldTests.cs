using System;
using System.Linq;
using System.Text.Json;
using Sunfish.Foundation.Recovery;
using Xunit;

namespace Sunfish.Foundation.Recovery.Tests;

public class EncryptedFieldTests
{
    private static EncryptedField Sample()
    {
        var ciphertext = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE };
        var nonce = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C };
        return new EncryptedField(ciphertext, nonce, KeyVersion: 1);
    }

    [Fact]
    public void Json_RoundTrip_PreservesAllFields()
    {
        var original = Sample();

        var json = JsonSerializer.Serialize(original);
        var rehydrated = JsonSerializer.Deserialize<EncryptedField>(json);

        Assert.Equal(original.KeyVersion, rehydrated.KeyVersion);
        Assert.True(original.Ciphertext.Span.SequenceEqual(rehydrated.Ciphertext.Span));
        Assert.True(original.Nonce.Span.SequenceEqual(rehydrated.Nonce.Span));
    }

    [Fact]
    public void Json_Shape_UsesShortPropertyNamesAndBase64Url()
    {
        var json = JsonSerializer.Serialize(Sample());

        Assert.Contains("\"ct\":", json);
        Assert.Contains("\"nonce\":", json);
        Assert.Contains("\"kv\":1", json);
        Assert.DoesNotContain("=", json);
        Assert.DoesNotContain("+", json);
        Assert.DoesNotContain("/", json);
    }

    [Fact]
    public void Json_Deserialize_RejectsMissingFields()
    {
        const string missingNonce = "{\"ct\":\"3q2-78r-\",\"kv\":1}";
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EncryptedField>(missingNonce));
    }

    [Fact]
    public void Equality_TreatsValueTypesAsEqualByContent()
    {
        var ciphertext = new byte[] { 1, 2, 3 };
        var nonce = new byte[] { 4, 5, 6 };
        var a = new EncryptedField(ciphertext, nonce, 1);
        var b = new EncryptedField(ciphertext, nonce, 1);
        var c = new EncryptedField(ciphertext, nonce, 2);

        Assert.True(a.Equals(b));
        Assert.False(a.Equals(c));
    }

    [Fact]
    public void ToString_DoesNotLeakCiphertextOrNonce()
    {
        var field = Sample();
        var rendered = field.ToString();

        Assert.DoesNotContain("DE", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AD", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BEEF", rendered, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KeyVersion=1", rendered);
        Assert.Contains("CiphertextLength=6", rendered);
        Assert.Contains("NonceLength=12", rendered);
    }

    [Fact]
    public void Json_AcceptsKeyVersionGreaterThanOne()
    {
        var original = new EncryptedField(new byte[] { 9, 9 }, new byte[] { 8, 8, 8 }, 7);

        var json = JsonSerializer.Serialize(original);
        var rehydrated = JsonSerializer.Deserialize<EncryptedField>(json);

        Assert.Equal(7, rehydrated.KeyVersion);
    }
}
