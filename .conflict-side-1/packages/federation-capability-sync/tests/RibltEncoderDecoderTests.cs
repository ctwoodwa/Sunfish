using Sunfish.Federation.CapabilitySync.Riblt;
using Xunit;

namespace Sunfish.Federation.CapabilitySync.Tests;

public class RibltEncoderDecoderTests
{
    private static RibltItem NewItem()
        => RibltItem.FromIdentity(unchecked((ulong)Random.Shared.NextInt64()));

    [Fact]
    public void EncodeDecode_NoDifference_DecodesSuccessfully_ZeroItems()
    {
        var items = new[] { NewItem(), NewItem(), NewItem(), NewItem(), NewItem() };
        var encoder = new RibltEncoder(items);
        var symbols = encoder.Batch(0, 16);

        var result = RibltDecoder.TryDecode(symbols, items);

        Assert.Equal(RibltDecodeOutcome.Success, result.Outcome);
        Assert.Empty(result.RemoteOnly);
        Assert.Empty(result.LocalOnly);
    }

    [Fact]
    public void EncodeDecode_RemoteHasExtras_DecodesThem()
    {
        var shared = new[] { NewItem(), NewItem(), NewItem(), NewItem() };
        var extra = NewItem();
        var remoteItems = shared.Append(extra).ToArray();

        var encoder = new RibltEncoder(remoteItems);
        var symbols = encoder.Batch(0, 32);

        var result = RibltDecoder.TryDecode(symbols, shared);

        Assert.Equal(RibltDecodeOutcome.Success, result.Outcome);
        Assert.Single(result.RemoteOnly);
        Assert.Empty(result.LocalOnly);
        Assert.Equal(extra.Hash, result.RemoteOnly[0].Hash);
        Assert.Equal(extra.Checksum, result.RemoteOnly[0].Checksum);
    }

    [Fact]
    public void EncodeDecode_LocalHasExtras_DecodesThem()
    {
        var shared = new[] { NewItem(), NewItem(), NewItem(), NewItem() };
        var onlyLocal = NewItem();
        var localItems = shared.Append(onlyLocal).ToArray();

        var encoder = new RibltEncoder(shared);
        var symbols = encoder.Batch(0, 32);

        var result = RibltDecoder.TryDecode(symbols, localItems);

        Assert.Equal(RibltDecodeOutcome.Success, result.Outcome);
        Assert.Empty(result.RemoteOnly);
        Assert.Single(result.LocalOnly);
        Assert.Equal(onlyLocal.Hash, result.LocalOnly[0].Hash);
        Assert.Equal(onlyLocal.Checksum, result.LocalOnly[0].Checksum);
    }

    [Fact]
    public void EncodeDecode_BothSidesDiverge_DecodesBoth()
    {
        var shared = new[] { NewItem(), NewItem(), NewItem() };
        var remoteOnlyExtras = new[] { NewItem(), NewItem(), NewItem() };
        var localOnlyExtras = new[] { NewItem(), NewItem() };

        var remoteItems = shared.Concat(remoteOnlyExtras).ToArray();
        var localItems = shared.Concat(localOnlyExtras).ToArray();

        var encoder = new RibltEncoder(remoteItems);
        var symbols = encoder.Batch(0, 64);

        var result = RibltDecoder.TryDecode(symbols, localItems);

        Assert.Equal(RibltDecodeOutcome.Success, result.Outcome);
        Assert.Equal(3, result.RemoteOnly.Count);
        Assert.Equal(2, result.LocalOnly.Count);
        foreach (var r in remoteOnlyExtras)
            Assert.Contains(result.RemoteOnly, x => x.Hash == r.Hash && x.Checksum == r.Checksum);
        foreach (var l in localOnlyExtras)
            Assert.Contains(result.LocalOnly, x => x.Hash == l.Hash && x.Checksum == l.Checksum);
    }
}
