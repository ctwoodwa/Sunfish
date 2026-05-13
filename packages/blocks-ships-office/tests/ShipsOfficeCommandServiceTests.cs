using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.Kernel.Audit;
using Xunit;
using KernelAuditRecord = Sunfish.Kernel.Audit.AuditRecord;
using KernelAuditQuery = Sunfish.Kernel.Audit.AuditQuery;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public class ShipsOfficeCommandServiceTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly ShipsOfficeDocumentId DocId = new("bundle:test-doc");
    private static readonly ActorId TestActor = new("actor-xo");
    private static readonly PrincipalId TestPrincipalId = PrincipalId.FromBytes(new byte[32]);
    private static readonly Principal TestPrincipal = new Individual(TestPrincipalId);

    private static PermissionDecision.Granted GrantedDecision => new(
        Role: ShipRole.XO,
        DecidedAt: DateTimeOffset.UtcNow,
        Proof: null);

    private static PermissionDecision.Denied DeniedDecision => new(
        Reason: DenialReason.NoMatchingRole,
        ReasonDisplay: "Insufficient role.",
        Remediation: new Remediation(RemediationKind.None, "Contact XO.", null, null, null),
        DecidedAt: DateTimeOffset.UtcNow);

    private static IOperationSigner MakeSigner()
    {
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(TestPrincipalId);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    Payload: call.Arg<AuditPayload>()!,
                    IssuerId: TestPrincipalId,
                    IssuedAt: call.Arg<DateTimeOffset>(),
                    Nonce: call.Arg<Guid>(),
                    Signature: Signature.FromBytes(new byte[64]))));
        return signer;
    }

    private static ShipsOfficeCommandService Build(
        IPermissionResolver? permissionResolver = null,
        IActorPrincipalResolver? actorResolver = null,
        IAuditContextProvider? actorContext = null,
        IAuditTrail? auditTrail = null,
        IOperationSigner? signer = null,
        IShipsOfficeDataProvider? dataProvider = null,
        ShipsOfficeOptions? options = null)
    {
        // Default mocks return safe values.
        if (actorContext is null)
        {
            var ctx = Substitute.For<IAuditContextProvider>();
            ctx.GetActor().Returns(TestActor);
            actorContext = ctx;
        }

        if (actorResolver is null)
        {
            var resolver = Substitute.For<IActorPrincipalResolver>();
            resolver.ResolveAsync(Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
                .Returns(TestPrincipal);
            actorResolver = resolver;
        }

        if (permissionResolver is null)
        {
            var perm = Substitute.For<IPermissionResolver>();
            perm.ResolveAsync(
                    Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                    Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                    Arg.Any<CancellationToken>())
                .Returns(GrantedDecision);
            permissionResolver = perm;
        }

        if (auditTrail is null)
            auditTrail = Substitute.For<IAuditTrail>();

        if (signer is null)
            signer = MakeSigner();

        if (dataProvider is null)
        {
            var dp = Substitute.For<IShipsOfficeDataProvider>();
            dp.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
                .Returns(new ShipsOfficeSnapshot(
                    Documents: Array.Empty<ShipsOfficeDocumentView>(),
                    TotalCount: 0,
                    AsOf: DateTimeOffset.UtcNow));
            dataProvider = dp;
        }

        return new ShipsOfficeCommandService(
            permissionResolver, actorResolver, actorContext,
            auditTrail, signer, dataProvider,
            Options.Create(options ?? new ShipsOfficeOptions()));
    }

    // ─── PublishAsync ordering ─────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_permission_check_first_then_audit_pre_op()
    {
        var callOrder = new List<string>();

        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("permission"); return GrantedDecision; });

        var trail = new RecordingAuditTrail(callOrder, "audit");

        var svc = Build(permissionResolver: perm, auditTrail: trail);
        var outcome = await svc.PublishAsync(TenantA, DocId);

        Assert.Equal(PublishOutcome.Published, outcome);
        Assert.Equal(2, callOrder.Count);
        Assert.Equal("permission", callOrder[0]);
        Assert.Equal("audit", callOrder[1]);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficeDocumentPublished, trail.Records[0].EventType);
    }

    [Fact]
    public async Task PublishAsync_emits_ShipsOfficePublishRejected_on_denial()
    {
        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(DeniedDecision);

        var trail = new RecordingAuditTrail();
        var svc = Build(permissionResolver: perm, auditTrail: trail);
        await svc.PublishAsync(TenantA, DocId);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficePublishRejected, trail.Records[0].EventType);
    }

    [Fact]
    public async Task PublishAsync_returns_Rejected_on_denial()
    {
        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(DeniedDecision);

        var svc = Build(permissionResolver: perm);
        var outcome = await svc.PublishAsync(TenantA, DocId);

        Assert.Equal(PublishOutcome.Rejected, outcome);
    }

    [Fact]
    public async Task PublishAsync_no_state_change_on_denial()
    {
        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(DeniedDecision);

        var trail = new RecordingAuditTrail();
        var dp = Substitute.For<IShipsOfficeDataProvider>();

        var svc = Build(permissionResolver: perm, auditTrail: trail, dataProvider: dp);
        var outcome = await svc.PublishAsync(TenantA, DocId);

        Assert.Equal(PublishOutcome.Rejected, outcome);
        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficePublishRejected, trail.Records[0].EventType);
        // RequireSecondActorPublish=false (default) — data provider not consulted on denied path.
        await dp.DidNotReceive().GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
    }

    // ─── ArchiveAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ArchiveAsync_no_audit_on_denial()
    {
        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(DeniedDecision);

        var trail = new RecordingAuditTrail();
        var svc = Build(permissionResolver: perm, auditTrail: trail);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.ArchiveAsync(TenantA, DocId));

        Assert.Empty(trail.Records);
    }

    [Fact]
    public async Task ArchiveAsync_emits_ShipsOfficeDocumentArchived_pre_op_on_pass()
    {
        var callOrder = new List<string>();

        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("permission"); return GrantedDecision; });

        var trail = new RecordingAuditTrail(callOrder, "audit");
        var svc = Build(permissionResolver: perm, auditTrail: trail);

        await svc.ArchiveAsync(TenantA, DocId);

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("permission", callOrder[0]);
        Assert.Equal("audit", callOrder[1]);

        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficeDocumentArchived, trail.Records[0].EventType);
    }

    // ─── RequireSecondActorPublish ─────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_self_publish_rejected_when_RequireSecondActorPublish_true()
    {
        // Set up data provider to return a doc last-modified by TestActor.
        var doc = new ShipsOfficeDocumentView(
            Id: DocId,
            Kind: ShipsOfficeDocumentKind.BundleManifest,
            Title: "Test Bundle",
            Status: DocumentStatus.Draft,
            UpdatedAt: DateTimeOffset.UtcNow,
            LastModifiedBy: TestActor,
            VersionLabel: "v1");

        var dp = Substitute.For<IShipsOfficeDataProvider>();
        dp.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ShipsOfficeSnapshot(
                Documents: new[] { doc },
                TotalCount: 1,
                AsOf: DateTimeOffset.UtcNow));

        var trail = new RecordingAuditTrail();
        var svc = Build(
            auditTrail: trail,
            dataProvider: dp,
            options: new ShipsOfficeOptions { RequireSecondActorPublish = true });

        var outcome = await svc.PublishAsync(TenantA, DocId);

        Assert.Equal(PublishOutcome.Rejected, outcome);
        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficePublishRejected, trail.Records[0].EventType);
    }

    [Fact]
    public async Task PublishAsync_self_publish_allowed_when_RequireSecondActorPublish_false()
    {
        // RequireSecondActorPublish=false (default) — self-publish should succeed.
        var doc = new ShipsOfficeDocumentView(
            Id: DocId,
            Kind: ShipsOfficeDocumentKind.BundleManifest,
            Title: "Test Bundle",
            Status: DocumentStatus.Draft,
            UpdatedAt: DateTimeOffset.UtcNow,
            LastModifiedBy: TestActor,
            VersionLabel: "v1");

        var dp = Substitute.For<IShipsOfficeDataProvider>();
        dp.GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(new ShipsOfficeSnapshot(
                Documents: new[] { doc },
                TotalCount: 1,
                AsOf: DateTimeOffset.UtcNow));

        var trail = new RecordingAuditTrail();
        var svc = Build(
            auditTrail: trail,
            dataProvider: dp,
            options: new ShipsOfficeOptions { RequireSecondActorPublish = false });

        var outcome = await svc.PublishAsync(TenantA, DocId);

        Assert.Equal(PublishOutcome.Published, outcome);
        // RequireSecondActorPublish=false — data provider not consulted.
        await dp.DidNotReceive().GetSnapshotAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>());
        Assert.Single(trail.Records);
        Assert.Equal(AuditEventType.ShipsOfficeDocumentPublished, trail.Records[0].EventType);
    }

    // ─── Fail-closed audit trail (B5 invariant) ───────────────────────────────

    [Fact]
    public async Task PublishAsync_audit_trail_failure_propagates_does_not_silently_succeed()
    {
        // RequireEmitAsync must NOT swallow: if audit is unavailable, operation must abort.
        var throwingTrail = new ThrowingAuditTrail(new InvalidOperationException("audit backend down"));
        var svc = Build(auditTrail: throwingTrail);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.PublishAsync(TenantA, DocId));
    }

    [Fact]
    public async Task ArchiveAsync_audit_trail_failure_propagates_does_not_silently_succeed()
    {
        var throwingTrail = new ThrowingAuditTrail(new InvalidOperationException("audit backend down"));
        var svc = Build(auditTrail: throwingTrail);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.ArchiveAsync(TenantA, DocId));
    }

    [Fact]
    public async Task PublishAsync_rejection_audit_failure_does_not_mask_denial()
    {
        // TryEmitRejectionAsync is best-effort: audit failure on the rejection path
        // must NOT promote a denial into an unhandled exception.
        var perm = Substitute.For<IPermissionResolver>();
        perm.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(), Arg.Any<ShipLocation>(),
                Arg.Any<DeckDepth>(), Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(DeniedDecision);

        var throwingTrail = new ThrowingAuditTrail(new InvalidOperationException("audit backend down"));
        var svc = Build(permissionResolver: perm, auditTrail: throwingTrail);

        // Should return Rejected — not throw — even though audit backend is down.
        var outcome = await svc.PublishAsync(TenantA, DocId);
        Assert.Equal(PublishOutcome.Rejected, outcome);
    }

    // ─── DI registration ──────────────────────────────────────────────────────

    [Fact]
    public void AddSunfishShipsOfficeDefaults_RegistersCommandService()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSunfishShipsOffice();
        services.AddSunfishShipsOfficeDefaults();
        // Verify the interface is registered (cannot build SP without all deps — just check descriptor exists).
        var descriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IShipsOfficeCommandService));
        Assert.NotNull(descriptor);
        Assert.Equal(
            Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton,
            descriptor.Lifetime);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private sealed class ThrowingAuditTrail : IAuditTrail
    {
        private readonly Exception _ex;
        public ThrowingAuditTrail(Exception ex) => _ex = ex;

        public ValueTask AppendAsync(KernelAuditRecord record, CancellationToken ct = default)
            => throw _ex;

        public System.Collections.Generic.IAsyncEnumerable<KernelAuditRecord> QueryAsync(
            KernelAuditQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        private readonly List<string>? _callOrder;
        private readonly string? _orderTag;
        private readonly object _lock = new();

        public List<KernelAuditRecord> Records { get; } = new();

        public RecordingAuditTrail(List<string>? callOrder = null, string? orderTag = null)
        {
            _callOrder = callOrder;
            _orderTag = orderTag;
        }

        public ValueTask AppendAsync(KernelAuditRecord record, CancellationToken ct = default)
        {
            lock (_lock)
            {
                Records.Add(record);
                if (_callOrder is not null && _orderTag is not null)
                    _callOrder.Add(_orderTag);
            }
            return ValueTask.CompletedTask;
        }

        public System.Collections.Generic.IAsyncEnumerable<KernelAuditRecord> QueryAsync(
            KernelAuditQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
