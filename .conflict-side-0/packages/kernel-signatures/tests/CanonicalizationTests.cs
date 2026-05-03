using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sunfish.Kernel.Signatures.Canonicalization;
using Sunfish.Kernel.Signatures.Models;
using Xunit;

namespace Sunfish.Kernel.Signatures.Tests;

/// <summary>
/// W#21 Phase 2: canonicalization tests covering the JSON canonicalizer
/// (key-reorder + whitespace invariance), UTF-8 NFC normalization, the
/// PDF/A stub, and the new ContentHash overloads.
/// </summary>
public sealed class CanonicalizationTests
{
    // ─────────── JsonCanonicalCanonicalizer ───────────

    [Fact]
    public void Json_KeyReorderProducesIdenticalBytes()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        var a = JsonNode.Parse(@"{""b"":1,""a"":2}")!;
        var b = JsonNode.Parse(@"{""a"":2,""b"":1}")!;

        var aBytes = canonicalizer.Canonicalize(a);
        var bBytes = canonicalizer.Canonicalize(b);

        Assert.Equal(aBytes, bBytes);
    }

    [Fact]
    public void Json_WhitespaceProducesIdenticalBytes()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        var compact = JsonNode.Parse(@"{""a"":1,""b"":2}")!;
        var spaced = JsonNode.Parse("{ \"a\":  1,\n  \"b\":2 }")!;

        var compactBytes = canonicalizer.Canonicalize(compact);
        var spacedBytes = canonicalizer.Canonicalize(spaced);

        Assert.Equal(compactBytes, spacedBytes);
    }

    [Fact]
    public void Json_NestedObjectKeys_AlsoSorted()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        var a = JsonNode.Parse(@"{""outer"":{""b"":1,""a"":2}}")!;
        var b = JsonNode.Parse(@"{""outer"":{""a"":2,""b"":1}}")!;

        Assert.Equal(canonicalizer.Canonicalize(a), canonicalizer.Canonicalize(b));
    }

    [Fact]
    public void Json_ArrayOrder_Preserved()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        var a = JsonNode.Parse(@"[1,2,3]")!;
        var b = JsonNode.Parse(@"[3,2,1]")!;

        Assert.NotEqual(canonicalizer.Canonicalize(a), canonicalizer.Canonicalize(b));
    }

    [Fact]
    public void Json_AcceptsArbitraryClrObject()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        var dict = new Dictionary<string, object?> { ["alpha"] = "beta", ["gamma"] = 42 };

        var bytes = canonicalizer.Canonicalize(dict);

        var parsed = JsonNode.Parse(Encoding.UTF8.GetString(bytes))!;
        Assert.Equal("beta", parsed["alpha"]!.GetValue<string>());
        Assert.Equal(42, parsed["gamma"]!.GetValue<int>());
    }

    [Fact]
    public void Json_KindIdentifier_Stable()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        Assert.Equal("json-canonical/rfc-8785-pragmatic", canonicalizer.CanonicalizationKind);
    }

    [Fact]
    public void Json_RejectsNullContent()
    {
        var canonicalizer = new JsonCanonicalCanonicalizer();
        Assert.Throws<ArgumentNullException>(() => canonicalizer.Canonicalize(null!));
    }

    // ─────────── Utf8NfcCanonicalizer ───────────

    [Fact]
    public void Utf8Nfc_NormalizesEquivalentForms()
    {
        var canonicalizer = new Utf8NfcCanonicalizer();
        var precomposed = "café";       // U+00E9 = é
        var decomposed = "café";      // e + U+0301
        Assert.Equal(canonicalizer.Canonicalize(precomposed), canonicalizer.Canonicalize(decomposed));
    }

    [Fact]
    public void Utf8Nfc_RejectsNonString()
    {
        var canonicalizer = new Utf8NfcCanonicalizer();
        Assert.Throws<ArgumentException>(() => canonicalizer.Canonicalize(42));
    }

    [Fact]
    public void Utf8Nfc_KindIdentifier_Stable()
    {
        var canonicalizer = new Utf8NfcCanonicalizer();
        Assert.Equal("utf-8-nfc", canonicalizer.CanonicalizationKind);
    }

    // ─────────── PdfACanonicalizer (stub) ───────────

    [Fact]
    public void PdfA_StubThrows_WithGuidance()
    {
        var canonicalizer = new PdfACanonicalizer();
        var ex = Assert.Throws<NotImplementedException>(() => canonicalizer.Canonicalize(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
        Assert.Contains("PDF/A canonicalization is deferred", ex.Message);
        Assert.Contains("ADR 0054 amendment A1", ex.Message);
    }

    [Fact]
    public void PdfA_KindIdentifier_Stable()
    {
        var canonicalizer = new PdfACanonicalizer();
        Assert.Equal("pdf-a-1b", canonicalizer.CanonicalizationKind);
    }

    // ─────────── ContentHash overloads ───────────

    [Fact]
    public void ContentHash_ComputeFromJson_KeyReorderInvariant()
    {
        var a = JsonNode.Parse(@"{""b"":1,""a"":2}")!;
        var b = JsonNode.Parse(@"{""a"":2,""b"":1}")!;

        var ha = ContentHash.ComputeFromJson(a);
        var hb = ContentHash.ComputeFromJson(b);

        Assert.True(ha.ConstantTimeEquals(hb));
    }

    [Fact]
    public void ContentHash_ComputeFromJsonObject_DeterministicOverDictionary()
    {
        var d1 = new Dictionary<string, object?> { ["b"] = 1, ["a"] = 2 };
        var d2 = new Dictionary<string, object?> { ["a"] = 2, ["b"] = 1 };

        var h1 = ContentHash.ComputeFromJsonObject(d1);
        var h2 = ContentHash.ComputeFromJsonObject(d2);

        Assert.True(h1.ConstantTimeEquals(h2));
    }

    [Fact]
    public void ContentHash_ComputeFromJson_RejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => ContentHash.ComputeFromJson(null!));
    }
}
