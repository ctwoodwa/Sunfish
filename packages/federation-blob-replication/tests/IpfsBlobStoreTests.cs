using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Blobs;
using Xunit;

namespace Sunfish.Federation.BlobReplication.Tests;

[Collection("Kubo")]
public sealed class IpfsBlobStoreTests
{
    private readonly KuboSingleNodeFixture _kubo;
    private readonly IpfsBlobStore _store;

    public IpfsBlobStoreTests(KuboSingleNodeFixture kubo)
    {
        _kubo = kubo;
        var options = Options.Create(new KuboBlobStoreOptions
        {
            RpcEndpoint = kubo.RpcEndpoint,
            PinOnPut = true,
        });
        _store = new IpfsBlobStore(kubo.KuboClient, options);
    }

    [Fact]
    public async Task PutGet_RoundTrip_ProducesStableCid()
    {
        var bytes = Encoding.UTF8.GetBytes("sunfish federation roundtrip");

        var first = await _store.PutAsync(bytes);
        var second = await _store.PutAsync(bytes);

        Assert.Equal(first.Value, second.Value);

        var fetched = await _store.GetAsync(first);
        Assert.NotNull(fetched);
        Assert.Equal(bytes, fetched!.Value.ToArray());
    }

    [Fact]
    public async Task Put_ComputesCidThatMatchesFoundationCidFromBytes()
    {
        // Critical cross-validation: Sunfish's own CID for bytes X must equal the CID Kubo assigns
        // for the same bytes — otherwise FileSystemBlobStore and IpfsBlobStore can't interoperate.
        var bytes = Encoding.UTF8.GetBytes("hello sunfish");
        var sunfishCid = Cid.FromBytes(bytes);

        var kuboCid = await _store.PutAsync(bytes);

        Assert.Equal(sunfishCid.Value, kuboCid.Value);
    }

    [Fact]
    public async Task PinUnpin_ReflectsInExistsLocally()
    {
        var bytes = Encoding.UTF8.GetBytes($"pin-unpin-{Guid.NewGuid():N}");
        var cid = await _store.PutAsync(bytes);

        Assert.True(await _store.ExistsLocallyAsync(cid));

        await _store.UnpinAsync(cid);
        Assert.False(await _store.ExistsLocallyAsync(cid));

        await _store.PinAsync(cid);
        Assert.True(await _store.ExistsLocallyAsync(cid));
    }

    [Fact]
    public async Task Get_UnknownCid_ReturnsNull()
    {
        // A valid-shape CID that was never put — use random bytes and compute its CID locally
        // without any PutAsync call.
        var nonce = new byte[64];
        RandomNumberGenerator.Fill(nonce);
        var cid = Cid.FromBytes(nonce);

        // GetAsync will ask Kubo to fetch the CID; on a sealed test network it cannot resolve it
        // and returns null. We use a short cancellation so we don't wait for DHT timeouts.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            var result = await _store.GetAsync(cid, cts.Token);
            Assert.Null(result);
        }
        catch (OperationCanceledException)
        {
            // Acceptable — Kubo may hang on the DHT lookup rather than returning "not found"
            // on an unreachable block. This is equivalent to "not locally available".
        }
    }

    [Fact]
    public async Task Put_LargeContent_RoundtripsOk()
    {
        // 1 MB exceeds Kubo's default 256 KiB chunk size, so Kubo stores this as a UnixFS DAG
        // with a dag-pb root CID (bafybei...). The content still roundtrips byte-for-byte via
        // Kubo's cat endpoint, and stable CIDs are still produced across identical payloads —
        // Sunfish's raw-block CID parity check is documented as holding only for single-block
        // content (see IpfsBlobStore.PutAsync). This test guards the large-payload path.
        var bytes = new byte[1 * 1024 * 1024];
        RandomNumberGenerator.Fill(bytes);

        var first = await _store.PutAsync(bytes);
        var second = await _store.PutAsync(bytes);
        Assert.Equal(first.Value, second.Value);

        var fetched = await _store.GetAsync(first);
        Assert.NotNull(fetched);
        Assert.Equal(bytes.Length, fetched!.Value.Length);
        Assert.True(bytes.AsSpan().SequenceEqual(fetched.Value.Span));
    }
}
