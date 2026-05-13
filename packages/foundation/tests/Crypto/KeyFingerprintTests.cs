using System;
using System.Linq;
using System.Text.Json;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Foundation.Tests.Crypto;

public class KeyFingerprintTests
{
    private static string ValidFingerprint() =>
        string.Join(":", Enumerable.Range(0, 32).Select(_ => "AB"));

    [Fact]
    public void CanonicalLength_Is95()
    {
        Assert.Equal(95, KeyFingerprint.CanonicalLength);
        Assert.Equal(KeyFingerprint.CanonicalLength, ValidFingerprint().Length);
    }

    [Fact]
    public void Parse_Valid_Succeeds()
    {
        var v = ValidFingerprint();
        var fp = KeyFingerprint.Parse(v);
        Assert.Equal(v, fp.Value);
        Assert.Equal(v, fp.ToString());
    }

    [Fact]
    public void Parse_WrongLength_Throws()
    {
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse("AB:CD"));
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse(string.Empty));
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse(new string('A', 94)));
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse(new string('A', 96)));
    }

    [Fact]
    public void Parse_NoColons_Throws()
    {
        // 95 chars but no separators at correct positions.
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse(new string('A', 95)));
    }

    [Fact]
    public void Parse_BadHexDigit_Throws()
    {
        // Replace one hex char with a non-hex char.
        var bad = ValidFingerprint().Replace("AB", "AZ", StringComparison.Ordinal);
        Assert.NotEqual(ValidFingerprint(), bad);
        Assert.Throws<FormatException>(() => KeyFingerprint.Parse(bad));
    }

    [Fact]
    public void Parse_LowercaseHex_Succeeds()
    {
        var lower = string.Join(":", Enumerable.Range(0, 32).Select(_ => "ab"));
        var fp = KeyFingerprint.Parse(lower);
        Assert.Equal(lower, fp.Value);
    }

    [Fact]
    public void Parse_MixedCaseHex_Succeeds()
    {
        var mixed = string.Join(":", Enumerable.Range(0, 32).Select(i => i % 2 == 0 ? "Ab" : "cD"));
        var fp = KeyFingerprint.Parse(mixed);
        Assert.Equal(mixed, fp.Value);
    }

    [Fact]
    public void IsValid_NullReturnsFalse()
    {
        Assert.False(KeyFingerprint.IsValid(null));
    }

    [Fact]
    public void IsValid_ColonInWrongPosition_ReturnsFalse()
    {
        // Move one colon to position 0.
        var bad = ":" + ValidFingerprint().Substring(1).Replace(":", "X").Substring(0, 94);
        Assert.False(KeyFingerprint.IsValid(bad));
    }

    [Fact]
    public void JsonRoundTrip_PreservesValue()
    {
        var fp = KeyFingerprint.Parse(ValidFingerprint());
        var json = JsonSerializer.Serialize(fp);
        Assert.Equal($"\"{ValidFingerprint()}\"", json);
        var restored = JsonSerializer.Deserialize<KeyFingerprint>(json);
        Assert.Equal(fp, restored);
    }

    [Fact]
    public void JsonDeserialize_InvalidString_Throws()
    {
        Assert.Throws<FormatException>(() =>
            JsonSerializer.Deserialize<KeyFingerprint>("\"not-a-fingerprint\""));
    }

    [Fact]
    public void RecordEquality_SameValueAreEqual()
    {
        var v = ValidFingerprint();
        var a = KeyFingerprint.Parse(v);
        var b = KeyFingerprint.Parse(v);
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void FromPublicKey_Deterministic()
    {
        var key = new byte[32];
        var fp1 = KeyFingerprint.FromPublicKey(key);
        var fp2 = KeyFingerprint.FromPublicKey(key);
        Assert.Equal(fp1, fp2);
    }

    [Fact]
    public void FromPublicKey_ProducesCanonicalFormat()
    {
        var key = new byte[32];
        var fp = KeyFingerprint.FromPublicKey(key);
        Assert.True(KeyFingerprint.IsValid(fp.Value));
        Assert.Equal(KeyFingerprint.CanonicalLength, fp.Value.Length);
    }

    [Fact]
    public void FromPublicKey_DifferentInputsDifferentFingerprints()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        key2[0] = 1;
        Assert.NotEqual(KeyFingerprint.FromPublicKey(key1), KeyFingerprint.FromPublicKey(key2));
    }

    [Fact]
    public void FromPublicKey_RoundTripsViaParse()
    {
        var key = new byte[32];
        var fp = KeyFingerprint.FromPublicKey(key);
        var reparsed = KeyFingerprint.Parse(fp.Value);
        Assert.Equal(fp, reparsed);
    }
}
