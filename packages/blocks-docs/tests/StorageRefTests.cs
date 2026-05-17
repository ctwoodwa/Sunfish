using System.Text.Json;
using Sunfish.Blocks.Docs.Models;
using Xunit;

namespace Sunfish.Blocks.Docs.Tests;

public class StorageRefTests
{
    [Fact]
    public void ForInline_SetsKindAndBytes()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var sr = StorageRef.ForInline(bytes);
        Assert.Equal(StorageRefKind.Inline, sr.Kind);
        Assert.NotNull(sr.InlineBytes);
        Assert.Equal(3, sr.InlineBytes!.Value.Length);
        Assert.Null(sr.FoundationCid);
        Assert.Null(sr.ExternalUrl);
    }

    [Fact]
    public void ForFoundationBlob_SetsKindAndCid()
    {
        var sr = StorageRef.ForFoundationBlob("bafy-test-cid");
        Assert.Equal(StorageRefKind.FoundationBlob, sr.Kind);
        Assert.Equal("bafy-test-cid", sr.FoundationCid);
        Assert.Null(sr.InlineBytes);
        Assert.Null(sr.ExternalUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForFoundationBlob_BlankCid_Throws(string cid)
    {
        Assert.Throws<ArgumentException>(() => StorageRef.ForFoundationBlob(cid));
    }

    [Fact]
    public void ForExternalUrl_SetsKindAndUrl()
    {
        var sr = StorageRef.ForExternalUrl("https://example.com/blob/123");
        Assert.Equal(StorageRefKind.ExternalUrl, sr.Kind);
        Assert.Equal("https://example.com/blob/123", sr.ExternalUrl);
        Assert.Null(sr.InlineBytes);
        Assert.Null(sr.FoundationCid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ForExternalUrl_BlankUrl_Throws(string url)
    {
        Assert.Throws<ArgumentException>(() => StorageRef.ForExternalUrl(url));
    }

    [Fact]
    public void StorageRefKind_JsonRoundtrip_LowercaseCamelCase()
    {
        Assert.Equal("\"inline\"",          JsonSerializer.Serialize(StorageRefKind.Inline));
        Assert.Equal("\"foundationBlob\"",  JsonSerializer.Serialize(StorageRefKind.FoundationBlob));
        Assert.Equal("\"externalUrl\"",     JsonSerializer.Serialize(StorageRefKind.ExternalUrl));

        Assert.Equal(StorageRefKind.FoundationBlob, JsonSerializer.Deserialize<StorageRefKind>("\"foundationBlob\""));
    }

    [Fact]
    public void StorageRefKind_UnknownJson_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<StorageRefKind>("\"unknown\""));
    }
}
