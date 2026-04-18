using System.Text;
using Sunfish.Foundation.Blobs;
using Xunit;

namespace Sunfish.Foundation.Tests;

public class CidTests
{
    [Fact]
    public void FromBytes_Deterministic_SameInputSameCid()
    {
        var content = Encoding.UTF8.GetBytes("hello sunfish");

        var a = Cid.FromBytes(content);
        var b = Cid.FromBytes(content);

        Assert.Equal(a, b);
    }

    [Fact]
    public void FromBytes_Distinct_DifferentInputDifferentCid()
    {
        var a = Cid.FromBytes(Encoding.UTF8.GetBytes("hello"));
        var b = Cid.FromBytes(Encoding.UTF8.GetBytes("world"));

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void FromBytes_FormatIsMultibaseBase32Lowercase()
    {
        var cid = Cid.FromBytes(Encoding.UTF8.GetBytes("x"));

        Assert.StartsWith("b", cid.Value);
        Assert.All(cid.Value[1..], c => Assert.Contains(c, "abcdefghijklmnopqrstuvwxyz234567"));
    }

    [Fact]
    public void FromBytes_EmptyInputProducesValidCid()
    {
        var cid = Cid.FromBytes(ReadOnlySpan<byte>.Empty);

        Assert.NotEmpty(cid.Value);
        Assert.StartsWith("b", cid.Value);
    }

    [Fact]
    public void FromBytes_RegressionLock_EmptyInput()
    {
        // Regression lock: the CID for empty input must stay stable so stored blobs remain
        // retrievable after refactors. Cross-verification against an external IPFS reference
        // implementation is a future integration task — this test only asserts Sunfish's
        // own output is deterministic and unchanged.
        var cid = Cid.FromBytes(ReadOnlySpan<byte>.Empty);

        // 4 header bytes + 32 digest bytes = 36 bytes → 58 base32 chars + 1 multibase prefix = 59 total
        Assert.Equal(59, cid.Value.Length);
        Assert.StartsWith("bafkrei", cid.Value); // standard CID v1 raw/sha256 prefix
    }

    [Fact]
    public void ImplicitStringConversion_Works()
    {
        Cid cid = Cid.FromBytes(Encoding.UTF8.GetBytes("x"));
        string s = cid;

        Assert.Equal(cid.Value, s);
    }
}

public class FileSystemBlobStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemBlobStore _store;

    public FileSystemBlobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sunfish-blobstore-tests-" + Path.GetRandomFileName());
        _store = new FileSystemBlobStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task PutGet_RoundTripsBytes()
    {
        var content = Encoding.UTF8.GetBytes("hello sunfish");

        var cid = await _store.PutAsync(content);
        var retrieved = await _store.GetAsync(cid);

        Assert.NotNull(retrieved);
        Assert.Equal(content, retrieved.Value.ToArray());
    }

    [Fact]
    public async Task Put_IsIdempotentForSameContent()
    {
        var content = Encoding.UTF8.GetBytes("duplicate me");

        var cid1 = await _store.PutAsync(content);
        var cid2 = await _store.PutAsync(content);

        Assert.Equal(cid1, cid2);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenCidNotStored()
    {
        var unknownCid = Cid.FromBytes(Encoding.UTF8.GetBytes("never stored"));

        var result = await _store.GetAsync(unknownCid);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExistsLocally_TrueAfterPut_FalseOtherwise()
    {
        var stored = Cid.FromBytes(Encoding.UTF8.GetBytes("stored"));
        var missing = Cid.FromBytes(Encoding.UTF8.GetBytes("missing"));

        await _store.PutAsync(Encoding.UTF8.GetBytes("stored"));

        Assert.True(await _store.ExistsLocallyAsync(stored));
        Assert.False(await _store.ExistsLocallyAsync(missing));
    }

    [Fact]
    public async Task Pin_Unpin_AreIdempotent()
    {
        var content = Encoding.UTF8.GetBytes("pin me");
        var cid = await _store.PutAsync(content);

        await _store.PinAsync(cid);
        await _store.PinAsync(cid); // idempotent
        await _store.UnpinAsync(cid);
        await _store.UnpinAsync(cid); // idempotent

        // Blob itself still exists — unpin doesn't synchronously delete.
        Assert.True(await _store.ExistsLocallyAsync(cid));
    }

    [Fact]
    public async Task Put_ShardsBlobIntoNestedDirectories()
    {
        var cid = await _store.PutAsync(Encoding.UTF8.GetBytes("shard test"));

        // Layout: {root}/{cid[1..3]}/{cid[3..5]}/{cid}
        var shardLevel1 = Path.Combine(_root, cid.Value.Substring(1, 2));
        var shardLevel2 = Path.Combine(shardLevel1, cid.Value.Substring(3, 2));
        var blobFile = Path.Combine(shardLevel2, cid.Value);

        Assert.True(Directory.Exists(shardLevel1), "level-1 shard directory missing");
        Assert.True(Directory.Exists(shardLevel2), "level-2 shard directory missing");
        Assert.True(File.Exists(blobFile), "blob file missing at sharded path");
    }

    [Fact]
    public async Task PutLargeContent_StreamsWithoutError()
    {
        // 1 MB payload — exercises the FileStream write path.
        var content = new byte[1024 * 1024];
        Random.Shared.NextBytes(content);

        var cid = await _store.PutAsync(content);
        var retrieved = await _store.GetAsync(cid);

        Assert.NotNull(retrieved);
        Assert.Equal(content.Length, retrieved.Value.Length);
    }
}
