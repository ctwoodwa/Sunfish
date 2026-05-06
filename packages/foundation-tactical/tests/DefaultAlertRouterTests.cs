using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Tactical.Tests;

public class DefaultAlertRouterTests
{
    private static readonly TenantId TenantA = new("alpha");

    private static TacticalAlert MakeAlert(
        string alertId = "sunfish.test:01HV4G7",
        string ruleName = "sunfish.test.rule",
        AlertRoutingPolicy policy = AlertRoutingPolicy.HighPriorityLookout,
        TenantId? tenantId = null) =>
        new(
            AlertId: alertId,
            TenantId: tenantId ?? TenantA,
            RuleName: ruleName,
            Severity: AlertSeverity.Medium,
            RoutingPolicy: policy,
            Title: "Test alert",
            Summary: "",
            DetectedAt: DateTimeOffset.UtcNow,
            Status: AlertStatus.Active,
            RequiresAcknowledgement: false,
            RunbookStepIds: Array.Empty<string>(),
            AcknowledgedBy: null,
            AcknowledgedAt: null);

    private static IOperationSigner StubSigner()
    {
        var principalId = PrincipalId.FromBytes(new byte[32]);
        var signer = Substitute.For<IOperationSigner>();
        signer.IssuerId.Returns(principalId);
        signer.SignAsync(
                Arg.Any<AuditPayload>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call => new ValueTask<SignedOperation<AuditPayload>>(
                new SignedOperation<AuditPayload>(
                    Payload: call.Arg<AuditPayload>()!,
                    IssuerId: principalId,
                    IssuedAt: call.Arg<DateTimeOffset>(),
                    Nonce: call.Arg<Guid>(),
                    Signature: Signature.FromBytes(new byte[64]))));
        return signer;
    }

    private static DefaultAlertRouter Build(
        TacticalOptions? options = null,
        ILookout? lookout = null,
        ISonarStore? sonar = null,
        IAuditTrail? audit = null,
        IOperationSigner? signer = null,
        ITenantContext? tenantContext = null) =>
        new(
            Options.Create(options ?? new TacticalOptions()),
            lookout ?? Substitute.For<ILookout>(),
            sonar ?? Substitute.For<ISonarStore>(),
            audit,
            signer,
            tenantContext);

    [Fact]
    public async Task RouteAsync_RejectsInvalidAlertIdRegex()
    {
        var trail = new RecordingAuditTrail();
        var lookout = Substitute.For<ILookout>();
        var router = Build(audit: trail, signer: StubSigner(), lookout: lookout);

        await router.RouteAsync(MakeAlert(alertId: "bad id with spaces"));

        await lookout.DidNotReceive().WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
        Assert.Single(trail.Records,
            r => r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied));
        var denial = trail.Records.First(r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied));
        Assert.Equal("invalid-alert-id", (string?)denial.Payload.Payload.Body["denial_reason"]);
    }

    [Fact]
    public async Task RouteAsync_EmitsTacticalAuthorizationDenied_OnRateBreach()
    {
        var trail = new RecordingAuditTrail();
        var options = new TacticalOptions { MaxAlertsPerMinutePerRule = 2 };
        var lookout = Substitute.For<ILookout>();
        var router = Build(options: options, audit: trail, signer: StubSigner(), lookout: lookout);

        await router.RouteAsync(MakeAlert(alertId: "sunfish.test:1"));
        await router.RouteAsync(MakeAlert(alertId: "sunfish.test:2"));
        await router.RouteAsync(MakeAlert(alertId: "sunfish.test:3"));

        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "rule-rate-limit".Equals(r.Payload.Payload.Body["denial_reason"]));
        // Lookout should have received only the first two routings.
        await lookout.Received(2).WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_EmitsAnomalyDetected_BeforeRouting()
    {
        var trail = new RecordingAuditTrail();
        var lookout = Substitute.For<ILookout>();
        var router = Build(audit: trail, signer: StubSigner(), lookout: lookout);

        var orderedSeen = new List<string>();
        lookout.WhenForAnyArgs(l => l.WriteAsync(default!, default))
            .Do(_ => orderedSeen.Add("LOOKOUT"));

        await router.RouteAsync(MakeAlert());

        // The trail recorded events in order; verify AnomalyDetected
        // appears before the lookout dispatch by inspecting the
        // trail order alongside our marker.
        Assert.Contains(trail.Records, r => r.EventType.Equals(AuditEventType.AnomalyDetected));
        Assert.Contains(trail.Records, r => r.EventType.Equals(AuditEventType.AlertRouted));
        // Order: AnomalyDetected then AlertRouted then LOOKOUT marker.
        var indexAnomaly = trail.Records.FindIndex(r => r.EventType.Equals(AuditEventType.AnomalyDetected));
        var indexRouted = trail.Records.FindIndex(r => r.EventType.Equals(AuditEventType.AlertRouted));
        Assert.True(indexAnomaly < indexRouted, "AnomalyDetected must precede AlertRouted.");
        Assert.Single(orderedSeen);
    }

    [Fact]
    public async Task RouteAsync_RoutesHighPriority_ToILookout()
    {
        var lookout = Substitute.For<ILookout>();
        var sonar = Substitute.For<ISonarStore>();
        var router = Build(lookout: lookout, sonar: sonar, signer: StubSigner(), audit: new RecordingAuditTrail());

        await router.RouteAsync(MakeAlert(policy: AlertRoutingPolicy.HighPriorityLookout));

        await lookout.Received(1).WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
        await sonar.DidNotReceive().WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_RoutesInformational_ToISonarStore()
    {
        var lookout = Substitute.For<ILookout>();
        var sonar = Substitute.For<ISonarStore>();
        var router = Build(lookout: lookout, sonar: sonar, signer: StubSigner(), audit: new RecordingAuditTrail());

        await router.RouteAsync(MakeAlert(policy: AlertRoutingPolicy.InformationalSonar));

        await sonar.Received(1).WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
        await lookout.DidNotReceive().WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RouteAsync_DowngradesUnlistedPrefix_HighPriority_ToInformationalSonar()
    {
        var trail = new RecordingAuditTrail();
        var lookout = Substitute.For<ILookout>();
        var sonar = Substitute.For<ISonarStore>();
        var options = new TacticalOptions
        {
            // Only "sunfish." prefix allowed; "third-party.*" is not.
            AllowedHighPriorityRulePrefixes = new[] { "sunfish.*" },
        };
        var router = Build(options: options, lookout: lookout, sonar: sonar,
            audit: trail, signer: StubSigner());

        await router.RouteAsync(MakeAlert(
            ruleName: "third-party.suspicious",
            policy: AlertRoutingPolicy.HighPriorityLookout));

        // Downgraded → Sonar, not Lookout.
        await lookout.DidNotReceive().WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
        await sonar.Received(1).WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());

        // Denial audit emitted with the downgrade reason.
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "high-priority-routing-not-allowlisted".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    [Fact]
    public async Task RouteAsync_TenantMismatch_ThrowsAndEmitsDenial()
    {
        var trail = new RecordingAuditTrail();
        var ambient = new FakeTenantContext(
            new TenantMetadata { Id = new TenantId("beta"), Name = "Beta Tenant" });

        var router = Build(audit: trail, signer: StubSigner(), tenantContext: ambient);

        var ex = await Assert.ThrowsAsync<TacticalUnauthorizedException>(() =>
            router.RouteAsync(MakeAlert(tenantId: TenantA)).AsTask());

        Assert.Contains("alpha", ex.Message);
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "tenant-mismatch".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    [Fact]
    public async Task RouteAsync_NoAuditTrailOrSigner_StillRoutesButSkipsAudit()
    {
        var lookout = Substitute.For<ILookout>();
        var router = Build(lookout: lookout, audit: null, signer: null);

        await router.RouteAsync(MakeAlert());

        await lookout.Received(1).WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Per W#52 P2 council Major M2: registered-but-unresolved tenant
    /// context (Tenant == null) → tenant-unresolved denial + throw.
    /// Silent pass-through would be a §Trust hole.
    /// </summary>
    [Fact]
    public async Task RouteAsync_UnresolvedTenantContext_ThrowsAndEmitsDenial()
    {
        var trail = new RecordingAuditTrail();
        var ambient = new FakeTenantContext(tenant: null);

        var router = Build(audit: trail, signer: StubSigner(), tenantContext: ambient);

        await Assert.ThrowsAsync<TacticalUnauthorizedException>(() =>
            router.RouteAsync(MakeAlert()).AsTask());
        Assert.Contains(trail.Records, r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "tenant-unresolved".Equals(r.Payload.Payload.Body["denial_reason"]));
    }

    /// <summary>
    /// Per W#52 P2 council Major M1: AuditSignatureException propagates;
    /// audit-by-construction (ADR 0049) — unsigned-but-routed is a
    /// §Trust violation worse than a missed route. Pinned against future
    /// refactors that might silently downgrade signature failures.
    /// </summary>
    [Fact]
    public async Task RouteAsync_AuditSignatureException_PropagatesAndPreventsDispatch()
    {
        var lookout = Substitute.For<ILookout>();
        var faultyTrail = new FaultyAuditTrail();
        var router = Build(audit: faultyTrail, signer: StubSigner(), lookout: lookout);

        await Assert.ThrowsAsync<AuditSignatureException>(() =>
            router.RouteAsync(MakeAlert()).AsTask());

        await lookout.DidNotReceive().WriteAsync(Arg.Any<TacticalAlert>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Per W#52 P2 council Critical C1: downgrade denial emits BEFORE
    /// AnomalyDetected so a signing failure on the denial can't leave
    /// AnomalyDetected+AlertRouted as a phantom-routing pair.
    /// </summary>
    [Fact]
    public async Task RouteAsync_DowngradePath_EmitsDenialBeforeAnomalyDetected()
    {
        var trail = new RecordingAuditTrail();
        var options = new TacticalOptions { AllowedHighPriorityRulePrefixes = new[] { "sunfish.*" } };
        var router = Build(options: options, audit: trail, signer: StubSigner());

        await router.RouteAsync(MakeAlert(
            ruleName: "third-party.suspicious",
            policy: AlertRoutingPolicy.HighPriorityLookout));

        var denialIdx = trail.Records.FindIndex(r =>
            r.EventType.Equals(AuditEventType.TacticalAuthorizationDenied) &&
            "high-priority-routing-not-allowlisted".Equals(r.Payload.Payload.Body["denial_reason"]));
        var anomalyIdx = trail.Records.FindIndex(r => r.EventType.Equals(AuditEventType.AnomalyDetected));
        var routedIdx = trail.Records.FindIndex(r => r.EventType.Equals(AuditEventType.AlertRouted));

        Assert.True(denialIdx >= 0 && anomalyIdx >= 0 && routedIdx >= 0);
        Assert.True(denialIdx < anomalyIdx,
            "Downgrade denial must be emitted before AnomalyDetected (audit-by-construction).");
        Assert.True(anomalyIdx < routedIdx);
    }

    private sealed class FaultyAuditTrail : IAuditTrail
    {
        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
            => throw new AuditSignatureException("Synthetic signature failure for test.");
        public IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        public FakeTenantContext(TenantMetadata? tenant) { Tenant = tenant; }
        public TenantMetadata? Tenant { get; }
    }

    [Fact]
    public void Ctor_AuditTrailWithoutSigner_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(audit: new RecordingAuditTrail(), signer: null));
    }

    [Fact]
    public void Ctor_SignerWithoutAuditTrail_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Build(audit: null, signer: StubSigner()));
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        public List<AuditRecord> Records { get; } = new();
        private readonly object _gate = new();

        public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
        {
            lock (_gate) { Records.Add(record); }
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<AuditRecord> QueryAsync(AuditQuery query, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
