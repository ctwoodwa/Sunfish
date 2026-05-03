using Sunfish.Federation.Common;
using Sunfish.Foundation.Transport.Audit;
using Xunit;

namespace Sunfish.Foundation.Transport.Tests;

public sealed class TransportAuditPayloadsTests
{
    private static readonly PeerId Peer = new("peer-a");

    [Fact]
    public void TierSelected_ShapeIsAlphabetized()
    {
        var p = TransportAuditPayloads.TierSelected(Peer, TransportTier.MeshVpn, "headscale");

        Assert.Equal("headscale", p.Body["adapter_name"]);
        Assert.Equal("peer-a", p.Body["peer_id"]);
        Assert.Equal("MeshVpn", p.Body["tier"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void TierSelected_T1HasNullAdapterName()
    {
        var p = TransportAuditPayloads.TierSelected(Peer, TransportTier.LocalNetwork, adapterName: null);
        Assert.Null(p.Body["adapter_name"]);
        Assert.Equal("LocalNetwork", p.Body["tier"]);
    }

    [Fact]
    public void MeshDeviceRegistered_ShapeIsAlphabetized()
    {
        var p = TransportAuditPayloads.MeshDeviceRegistered(Peer, "headscale", "anchor-1");
        Assert.Equal("headscale", p.Body["adapter_name"]);
        Assert.Equal("anchor-1", p.Body["device_name"]);
        Assert.Equal("peer-a", p.Body["peer_id"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void MeshHandshakeCompleted_ShapeIsAlphabetized()
    {
        var p = TransportAuditPayloads.MeshHandshakeCompleted(Peer, "headscale");
        Assert.Equal("headscale", p.Body["adapter_name"]);
        Assert.Equal("peer-a", p.Body["peer_id"]);
        Assert.Equal(2, p.Body.Count);
    }

    [Fact]
    public void MeshTransportFailed_IncludesReason()
    {
        var p = TransportAuditPayloads.MeshTransportFailed(Peer, "headscale", "resolve-miss");
        Assert.Equal("headscale", p.Body["adapter_name"]);
        Assert.Equal("peer-a", p.Body["peer_id"]);
        Assert.Equal("resolve-miss", p.Body["reason"]);
        Assert.Equal(3, p.Body.Count);
    }

    [Fact]
    public void TransportFallbackToRelay_OutcomeSelected()
    {
        var p = TransportAuditPayloads.TransportFallbackToRelay(Peer, "Selected");
        Assert.Equal("Selected", p.Body["outcome"]);
        Assert.Equal("peer-a", p.Body["peer_id"]);
        Assert.Equal(2, p.Body.Count);
    }

    [Fact]
    public void TransportFallbackToRelay_OutcomeFailed()
    {
        var p = TransportAuditPayloads.TransportFallbackToRelay(Peer, "Failed");
        Assert.Equal("Failed", p.Body["outcome"]);
    }
}
