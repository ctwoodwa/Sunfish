using System.Text;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class CanonicalJsonTests
{
    [Fact]
    public void Serialize_SortsObjectKeysAlphabetically()
    {
        var unsorted = new { z = 1, a = 2, m = 3 };

        var bytes = CanonicalJson.Serialize(unsorted);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Equal("{\"a\":2,\"m\":3,\"z\":1}", json);
    }

    [Fact]
    public void Serialize_IsStable_RegardlessOfInputOrder()
    {
        var a = new Dictionary<string, int> { ["b"] = 2, ["a"] = 1, ["c"] = 3 };
        var b = new Dictionary<string, int> { ["c"] = 3, ["a"] = 1, ["b"] = 2 };

        Assert.Equal(CanonicalJson.Serialize(a), CanonicalJson.Serialize(b));
    }

    [Fact]
    public void SerializeSignable_EnvelopeKeysAndPayloadAreSortedTogether()
    {
        var payload = new { zeta = 1, alpha = 2 };
        var issuer = KeyPair.Generate().PrincipalId;
        var issuedAt = new DateTimeOffset(2026, 4, 17, 0, 0, 0, TimeSpan.Zero);
        var nonce = Guid.Parse("11111111-2222-3333-4444-555555555555");

        var bytes = CanonicalJson.SerializeSignable(payload, issuer, issuedAt, nonce);
        var json = Encoding.UTF8.GetString(bytes);

        // Envelope keys: issuedAt, issuerId, nonce, payload (already alphabetical).
        // Payload keys inside must also be sorted alphabetically.
        var issuedAtIdx = json.IndexOf("\"issuedAt\"", StringComparison.Ordinal);
        var issuerIdIdx = json.IndexOf("\"issuerId\"", StringComparison.Ordinal);
        var nonceIdx = json.IndexOf("\"nonce\"", StringComparison.Ordinal);
        var payloadIdx = json.IndexOf("\"payload\"", StringComparison.Ordinal);
        var alphaIdx = json.IndexOf("\"alpha\"", StringComparison.Ordinal);
        var zetaIdx = json.IndexOf("\"zeta\"", StringComparison.Ordinal);

        Assert.True(issuedAtIdx < issuerIdIdx);
        Assert.True(issuerIdIdx < nonceIdx);
        Assert.True(nonceIdx < payloadIdx);
        Assert.True(alphaIdx < zetaIdx);
    }

    [Fact]
    public void Serialize_HandlesNullValues()
    {
        var value = new Dictionary<string, object?> { ["b"] = null, ["a"] = "x" };

        var bytes = CanonicalJson.Serialize(value);
        var json = Encoding.UTF8.GetString(bytes);

        Assert.Equal("{\"a\":\"x\",\"b\":null}", json);
    }

    [Fact]
    public void Serialize_RecursivelySortsNestedObjects()
    {
        var value = new
        {
            outer = new { z = 1, a = 2 },
            alpha = new object[] { new { y = 1, x = 2 }, new { b = 1, a = 2 } },
        };

        var bytes = CanonicalJson.Serialize(value);
        var json = Encoding.UTF8.GetString(bytes);

        // Top-level keys sorted: "alpha" before "outer".
        Assert.StartsWith("{\"alpha\":", json);
        // Nested object keys sorted: "a" before "z" in outer.
        Assert.Contains("\"outer\":{\"a\":2,\"z\":1}", json);
        // Array preserves order but each element's keys are sorted.
        Assert.Contains("[{\"x\":2,\"y\":1},{\"a\":2,\"b\":1}]", json);
    }
}
