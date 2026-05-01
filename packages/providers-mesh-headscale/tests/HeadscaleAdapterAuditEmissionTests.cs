using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Providers.Mesh.Headscale.Tests;

public sealed class HeadscaleAdapterAuditEmissionTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly PeerId PeerA = new("peer-a");

    [Fact]
    public async Task RegisterDeviceAsync_AuditEnabled_EmitsMeshDeviceRegistered()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var options = new HeadscaleOptions { BaseUrl = new Uri("https://h.example/"), ApiKey = "k" };
        var client = new FakeHeadscaleClient(options);
        var adapter = new HeadscaleMeshAdapter(client, options, trail, signer, TenantA);

        await adapter.RegisterDeviceAsync(new MeshDeviceRegistration
        {
            DeviceId = "ignored",
            Peer = PeerA,
            DeviceName = "anchor-1",
            Tags = new[] { "tag:env-prod" },
        }, CancellationToken.None);

        await trail.Received(1).AppendAsync(
            Arg.Is<AuditRecord>(r => r != null && r.EventType.Equals(AuditEventType.MeshDeviceRegistered) && r.TenantId.Equals(TenantA)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterDeviceAsync_AuditDisabled_DoesNotEmit()
    {
        var options = new HeadscaleOptions { BaseUrl = new Uri("https://h.example/"), ApiKey = "k" };
        var client = new FakeHeadscaleClient(options);
        var adapter = new HeadscaleMeshAdapter(client, options); // audit-disabled overload

        // Should complete without throwing — no audit emitter wired.
        await adapter.RegisterDeviceAsync(new MeshDeviceRegistration
        {
            DeviceId = "ignored",
            Peer = PeerA,
            DeviceName = "anchor-1",
            Tags = Array.Empty<string>(),
        }, CancellationToken.None);
    }

    [Fact]
    public void Constructor_AuditEnabled_RequiresAllArgs()
    {
        var trail = Substitute.For<IAuditTrail>();
        var signer = new Ed25519Signer(KeyPair.Generate());
        var options = new HeadscaleOptions { BaseUrl = new Uri("https://h.example/"), ApiKey = "k" };
        var client = new FakeHeadscaleClient(options);

        Assert.Throws<ArgumentNullException>(() => new HeadscaleMeshAdapter(client, options, null!, signer, TenantA));
        Assert.Throws<ArgumentNullException>(() => new HeadscaleMeshAdapter(client, options, trail, null!, TenantA));
        Assert.Throws<ArgumentException>(() => new HeadscaleMeshAdapter(client, options, trail, signer, default));
    }

    private sealed class FakeHeadscaleClient : HeadscaleClient
    {
        public FakeHeadscaleClient(HeadscaleOptions options)
            : base(new HttpClient { BaseAddress = options.BaseUrl }, options) { }

        public override Task<bool> HealthCheckAsync(CancellationToken ct) => Task.FromResult(true);
        public override Task<IReadOnlyList<HeadscaleNode>> ListNodesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<HeadscaleNode>>(Array.Empty<HeadscaleNode>());
        public override Task<HeadscaleNode> RegisterNodeAsync(HeadscaleRegisterRequest request, CancellationToken ct) =>
            Task.FromResult(new HeadscaleNode { Id = "issued-1", Name = request.Name, ForcedTags = request.ForcedTags });
    }
}
