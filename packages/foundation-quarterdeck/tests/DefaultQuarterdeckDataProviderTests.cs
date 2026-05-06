using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.MissionSpace;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Xunit;

namespace Sunfish.Foundation.Quarterdeck.Tests;

/// <summary>
/// W#51 Phase 2 — coverage for
/// <see cref="DefaultQuarterdeckDataProvider"/> per ADR 0080 §2.3.
/// </summary>
public class DefaultQuarterdeckDataProviderTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly ActorId ActorA = new("alice");

    private static IActorPrincipalResolver TestResolver()
    {
        // Test fixtures use non-canonical ActorId values
        // ("alice", etc.). Register an explicit override so the
        // resolver returns a stable Principal without requiring a
        // real Ed25519 keypair in every test.
        var resolver = new InMemoryActorPrincipalResolver();
        var pid = Sunfish.Foundation.Crypto.KeyPair.Generate().PrincipalId;
        resolver.Register(ActorA, new Individual(pid));
        return resolver;
    }

    private static IPermissionResolver AlwaysGrant()
    {
        var resolver = Substitute.For<IPermissionResolver>();
        resolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult<PermissionDecision>(
                new PermissionDecision.Granted(
                    ShipRole.Captain,
                    DateTimeOffset.UtcNow,
                    Proof: null)));
        return resolver;
    }

    private static IPermissionResolver AlwaysDeny()
    {
        var resolver = Substitute.For<IPermissionResolver>();
        resolver.ResolveAsync(
                Arg.Any<TenantId>(), Arg.Any<Principal>(),
                Arg.Any<ShipLocation>(), Arg.Any<DeckDepth>(),
                Arg.Any<ShipAction>(), Arg.Any<Resource?>(),
                Arg.Any<CancellationToken>())
            .Returns(ci => ValueTask.FromResult<PermissionDecision>(
                new PermissionDecision.Denied(
                    Reason: DenialReason.NoMatchingRole,
                    ReasonDisplay: "Captain role required.",
                    Remediation: new Remediation(
                        Kind: RemediationKind.ContactAuthority,
                        GuidanceDisplay: "Ask Captain for access.",
                        ContactActor: null,
                        EscalationLink: null,
                        CallToActionLabel: null),
                    DecidedAt: DateTimeOffset.UtcNow)));
        return resolver;
    }

    private static IOodWatchService NoActiveWatch()
    {
        var svc = Substitute.For<IOodWatchService>();
        svc.GetActiveWatchAsync(Arg.Any<TenantId>(), Arg.Any<OodRole>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<OodWatch?>(null));
        return svc;
    }

    private static IStandingOrderRepository EmptyRepo()
    {
        var repo = Substitute.For<IStandingOrderRepository>();
        repo.EnumerateAsync(Arg.Any<TenantId>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEmpty<StandingOrder>());
        return repo;
    }

    private static async IAsyncEnumerable<T> AsyncEmpty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }

    private static IMissionEnvelopeProvider StubEnvelope()
    {
        var prov = Substitute.For<IMissionEnvelopeProvider>();
        prov.GetCurrentAsync(Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult<MissionEnvelope>(null!));
        return prov;
    }

    private static DefaultQuarterdeckDataProvider Build(
        IActorPrincipalResolver? actorResolver = null,
        IPermissionResolver? resolver = null,
        IOodWatchService? watchService = null,
        IStandingOrderRepository? standingOrders = null,
        IMissionEnvelopeProvider? envelope = null,
        IEnumerable<IQuarterdeckAlertSource>? alertSources = null,
        IEnumerable<IDepartmentKpiSource>? kpiSources = null) =>
        new DefaultQuarterdeckDataProvider(
            actorResolver ?? TestResolver(),
            resolver ?? AlwaysGrant(),
            watchService ?? NoActiveWatch(),
            standingOrders ?? EmptyRepo(),
            envelope ?? StubEnvelope(),
            alertSources ?? Array.Empty<IQuarterdeckAlertSource>(),
            kpiSources ?? Array.Empty<IDepartmentKpiSource>());

    [Fact]
    public async Task GetSnapshot_AllDepartmentsPresent_WithAccessDecision()
    {
        var provider = Build();
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        // Six v1 departments (Wayfinder / EngineRoom / Tactical /
        // SickBay / ShipsOffice / SupplyOffice).
        Assert.Equal(6, snapshot.DepartmentLinks.Count);
        Assert.All(snapshot.DepartmentLinks, l =>
            Assert.Equal(DepartmentStatus.Accessible, l.AccessDecision));
    }

    [Fact]
    public async Task GetSnapshot_DeniedDepartment_SurfacesDenialReason_NotHidden()
    {
        var provider = Build(resolver: AlwaysDeny());
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        // Denied-not-hidden invariant per ADR 0080 §2.3 rule 4: every
        // department renders with Status=Denied + non-null reason; no
        // department disappears from the list.
        Assert.Equal(6, snapshot.DepartmentLinks.Count);
        Assert.All(snapshot.DepartmentLinks, l =>
        {
            Assert.Equal(DepartmentStatus.Denied, l.AccessDecision);
            Assert.NotNull(l.DenialReason);
            Assert.Equal("Captain role required.", l.DenialReason);
        });
    }

    [Fact]
    public async Task GetSnapshot_NoActiveWatch_ReturnsNullSummary()
    {
        var provider = Build();
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        Assert.Null(snapshot.OodWatch.OfficerOfTheDeck.CurrentActorDisplayName);
        Assert.False(snapshot.OodWatch.OfficerOfTheDeck.IsExpired);
        Assert.Null(snapshot.OodWatch.EngineeringOfficerOfTheWatch.CurrentActorDisplayName);
        Assert.False(snapshot.OodWatch.EngineeringOfficerOfTheWatch.IsExpired);
    }

    [Fact]
    public async Task GetSnapshot_EmptyRepo_RecentOrdersIsEmpty()
    {
        var provider = Build();
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);
        Assert.Empty(snapshot.RecentOrders);
    }

    [Fact]
    public async Task GetSnapshot_EmptyAlertSources_PendingAlertsIsEmpty()
    {
        var provider = Build();
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);
        Assert.Empty(snapshot.PendingAlerts);
    }

    [Fact]
    public async Task GetSnapshot_AlertWithExpiredAt_AndNoAcknowledgementRequired_IsOmitted()
    {
        var alert = new QuarterdeckAlert(
            AlertId: "src-a:01HV4G7",
            TenantId: TenantA,
            Severity: AlertSeverity.Normal,
            Title: "Old alert",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            RequiresAcknowledgement: false,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.test");

        var source = Substitute.For<IQuarterdeckAlertSource>();
        source.SourceName.Returns("sunfish.test");
        source.GetAlertsAsync(
                Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(SingleAlert(alert));

        var provider = Build(alertSources: new[] { source });
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        // Per ADR 0080 §2.3 rule 8 — expired non-ack-required alert
        // is omitted from the snapshot.
        Assert.Empty(snapshot.PendingAlerts);
    }

    [Fact]
    public async Task GetSnapshot_RequiresAcknowledgementAlert_PersistsPastExpiry()
    {
        var alert = new QuarterdeckAlert(
            AlertId: "src-a:01HV4G8",
            TenantId: TenantA,
            Severity: AlertSeverity.Emergency,
            Title: "Hull breach",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-1),
            RequiresAcknowledgement: true,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.test");

        var source = Substitute.For<IQuarterdeckAlertSource>();
        source.SourceName.Returns("sunfish.test");
        source.GetAlertsAsync(
                Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(SingleAlert(alert));

        var provider = Build(alertSources: new[] { source });
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        Assert.Single(snapshot.PendingAlerts);
        Assert.Equal(AlertSeverity.Emergency, snapshot.PendingAlerts[0].Severity);
    }

    [Fact]
    public async Task GetSnapshot_CrossTenantAlert_IsDropped()
    {
        var crossTenantAlert = new QuarterdeckAlert(
            AlertId: "src-a:cross",
            TenantId: new TenantId("tenant-b"),
            Severity: AlertSeverity.Normal,
            Title: "Cross-tenant alert (defense)",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            RequiresAcknowledgement: false,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.test");

        var source = Substitute.For<IQuarterdeckAlertSource>();
        source.SourceName.Returns("sunfish.test");
        source.GetAlertsAsync(
                Arg.Any<TenantId>(), Arg.Any<ActorId>(), Arg.Any<CancellationToken>())
            .Returns(SingleAlert(crossTenantAlert));

        var provider = Build(alertSources: new[] { source });
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);

        // Provider drops cross-tenant alerts defensively per the
        // §5.2 anti-spoofing posture.
        Assert.Empty(snapshot.PendingAlerts);
    }

    [Fact]
    public async Task GetSnapshot_EmptyActor_ThrowsArgumentException()
    {
        var provider = Build();
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.GetSnapshotAsync(TenantA, new ActorId(string.Empty)));
    }

    [Fact]
    public async Task GetSnapshot_UnresolvableActor_ReturnsAllDeniedSnapshot()
    {
        // §5.2 fail-closed: when IActorPrincipalResolver returns
        // null, every department is Denied with a clear reason.
        // Watch + envelope still resolve (host-side state); alerts +
        // KPIs are empty.
        var emptyResolver = new InMemoryActorPrincipalResolver(); // no overrides
        var provider = Build(actorResolver: emptyResolver);

        var snapshot = await provider.GetSnapshotAsync(
            TenantA, new ActorId("not-a-canonical-base64url-key"));

        Assert.All(snapshot.DepartmentLinks, l =>
            Assert.Equal(DepartmentStatus.Denied, l.AccessDecision));
        Assert.All(snapshot.DepartmentLinks, l =>
            Assert.Contains("not be resolved", l.DenialReason ?? ""));
        Assert.Empty(snapshot.PendingAlerts);
        Assert.Empty(snapshot.KpiCards);
        Assert.Empty(snapshot.RecentOrders);
    }

    [Fact]
    public async Task GetSnapshot_SnapshotAt_IsRecent()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var provider = Build();
        var snapshot = await provider.GetSnapshotAsync(TenantA, ActorA);
        var after = DateTimeOffset.UtcNow.AddSeconds(1);
        Assert.InRange(snapshot.SnapshotAt, before, after);
    }

    private static async IAsyncEnumerable<QuarterdeckAlert> SingleAlert(QuarterdeckAlert alert)
    {
        await Task.CompletedTask;
        yield return alert;
    }
}
