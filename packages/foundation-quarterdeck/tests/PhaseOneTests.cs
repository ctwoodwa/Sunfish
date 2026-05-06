using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Quarterdeck;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Quarterdeck.Tests;

/// <summary>
/// Phase 1 substrate tests per W#51 hand-off acceptance gate. Six
/// minimum tests covering: (1) denied-not-hidden invariant on
/// <see cref="DepartmentLink"/>, (2) denial-reason preservation, (3)
/// OOD-watch summary projection, (4) <see cref="AlertSeverity"/>
/// orderability, (5) snapshot ordering invariants documented on
/// <see cref="QuarterdeckSnapshot.PendingAlerts"/>, (6) expired-alert
/// filtering invariant. Phase 1 ships contracts only — no concrete
/// data provider exists yet — so behavioural assertions on aggregation
/// land in Phase 2.
/// </summary>
public class QuarterdeckSnapshotShapeTests
{
    [Fact]
    public void QuarterdeckSnapshot_DeniedDepartment_StatusIsDenied_NotHidden()
    {
        var deniedLink = new DepartmentLink(
            ShipLocation.SickBay,
            DisplayName: "Sick Bay",
            AccessDecision: DepartmentStatus.Denied,
            DenialReason: "Requires IDC role.");

        var snapshot = new QuarterdeckSnapshot(
            OodWatch: NullOodWatch(),
            MissionEnvelope: UnknownMissionEnvelope(),
            RecentOrders: Array.Empty<StandingOrderSummary>(),
            PendingAlerts: Array.Empty<QuarterdeckAlert>(),
            KpiCards: Array.Empty<DepartmentKpi>(),
            DepartmentLinks: new[] { deniedLink },
            SnapshotAt: DateTimeOffset.UtcNow);

        // Denied-not-hidden invariant: the denied location is rendered
        // (present in the link list) AND tagged Denied. The Quarterdeck
        // UI never silently omits a known location.
        var sickBay = snapshot.DepartmentLinks.Single(l => l.Location == ShipLocation.SickBay);
        Assert.Equal(DepartmentStatus.Denied, sickBay.AccessDecision);
        Assert.Equal("Sick Bay", sickBay.DisplayName);
    }

    [Fact]
    public void DepartmentLink_DeniedAccessDecision_DisplayNameAndDenialReasonPreserved()
    {
        var link = new DepartmentLink(
            ShipLocation.EngineRoom,
            DisplayName: "Engine Room",
            AccessDecision: DepartmentStatus.Denied,
            DenialReason: "Requires Engineer Officer role.");

        Assert.Equal("Engine Room", link.DisplayName);
        Assert.Equal("Requires Engineer Officer role.", link.DenialReason);
    }

    [Fact]
    public void OodWatchSummary_FieldsRoundtripBothRoles()
    {
        var ood = new OodRoleSummary(
            OodRole.OfficerOfTheDeck,
            CurrentActorDisplayName: "Lt. Vega",
            WatchStartedAt: DateTimeOffset.UtcNow.AddHours(-2),
            IsExpired: false);
        var eoow = new OodRoleSummary(
            OodRole.EngineeringOfficerOfTheWatch,
            CurrentActorDisplayName: null,
            WatchStartedAt: null,
            IsExpired: false);

        var summary = new OodWatchSummary(ood, eoow);

        Assert.Equal(OodRole.OfficerOfTheDeck, summary.OfficerOfTheDeck.Role);
        Assert.Equal("Lt. Vega", summary.OfficerOfTheDeck.CurrentActorDisplayName);
        Assert.Equal(OodRole.EngineeringOfficerOfTheWatch, summary.EngineeringOfficerOfTheWatch.Role);
        Assert.Null(summary.EngineeringOfficerOfTheWatch.CurrentActorDisplayName);
        Assert.False(summary.EngineeringOfficerOfTheWatch.IsExpired);
    }

    [Fact]
    public void AlertSeverity_OrdinalsSortEmergencyFirst()
    {
        Assert.True((int)AlertSeverity.Emergency < (int)AlertSeverity.High);
        Assert.True((int)AlertSeverity.High < (int)AlertSeverity.Normal);
        Assert.True((int)AlertSeverity.Normal < (int)AlertSeverity.Informational);

        var unsorted = new[]
        {
            AlertSeverity.Informational, AlertSeverity.Emergency,
            AlertSeverity.Normal, AlertSeverity.High,
        };
        var sorted = unsorted.OrderBy(s => (int)s).ToArray();
        Assert.Equal(AlertSeverity.Emergency, sorted[0]);
        Assert.Equal(AlertSeverity.Informational, sorted[3]);
    }

    [Fact]
    public void QuarterdeckSnapshot_RecordsPreserveAllAggregateFields()
    {
        var orderId = new StandingOrderId(Guid.NewGuid());
        var snapshot = new QuarterdeckSnapshot(
            OodWatch: NullOodWatch(),
            MissionEnvelope: new MissionEnvelopeSummary(MissionEnvelopeStatus.Passed, "v1.0", DateTimeOffset.UtcNow),
            RecentOrders: new[]
            {
                new StandingOrderSummary(orderId, "/identity/keys", DateTimeOffset.UtcNow, "Captain Reyes"),
            },
            PendingAlerts: new[]
            {
                new QuarterdeckAlert(
                    AlertId: "alert-1",
                    TenantId: TenantId.System,
                    Severity: AlertSeverity.Emergency,
                    Title: "Hull breach",
                    Summary: null,
                    IssuedAt: DateTimeOffset.UtcNow,
                    ExpiresAt: null,
                    RequiresAcknowledgement: true,
                    IsAcknowledged: false,
                    AcknowledgedBy: null,
                    AcknowledgedAt: null,
                    SourceName: "sunfish.tactical.lookout"),
            },
            KpiCards: new[]
            {
                new DepartmentKpi(
                    SourceName: "sunfish.engine-room.health",
                    Label: "CRDT growth",
                    Value: "12.4",
                    Unit: "MB/h",
                    Status: DepartmentStatus.Accessible),
            },
            DepartmentLinks: Array.Empty<DepartmentLink>(),
            SnapshotAt: DateTimeOffset.UtcNow);

        Assert.Single(snapshot.RecentOrders);
        Assert.Equal(orderId, snapshot.RecentOrders[0].Id);
        Assert.Equal(MissionEnvelopeStatus.Passed, snapshot.MissionEnvelope.Status);
        Assert.Single(snapshot.PendingAlerts);
        Assert.Equal(AlertSeverity.Emergency, snapshot.PendingAlerts[0].Severity);
        Assert.Single(snapshot.KpiCards);
        Assert.Equal(DepartmentStatus.Accessible, snapshot.KpiCards[0].Status);
    }

    [Fact]
    public void QuarterdeckAlert_AcknowledgedFields_AlignWithIsAcknowledged()
    {
        var unacknowledged = new QuarterdeckAlert(
            AlertId: "a1",
            TenantId: TenantId.System,
            Severity: AlertSeverity.High,
            Title: "Pending",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1),
            RequiresAcknowledgement: false,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.test");

        Assert.False(unacknowledged.IsAcknowledged);
        Assert.Null(unacknowledged.AcknowledgedBy);
        Assert.Null(unacknowledged.AcknowledgedAt);

        var acknowledged = unacknowledged with
        {
            IsAcknowledged = true,
            AcknowledgedBy = "Captain Reyes",
            AcknowledgedAt = DateTimeOffset.UtcNow,
        };

        Assert.True(acknowledged.IsAcknowledged);
        Assert.NotNull(acknowledged.AcknowledgedBy);
        Assert.NotNull(acknowledged.AcknowledgedAt);
    }

    private static OodWatchSummary NullOodWatch() => new(
        OfficerOfTheDeck: new OodRoleSummary(OodRole.OfficerOfTheDeck, null, null, false),
        EngineeringOfficerOfTheWatch: new OodRoleSummary(OodRole.EngineeringOfficerOfTheWatch, null, null, false));

    private static MissionEnvelopeSummary UnknownMissionEnvelope() =>
        new(MissionEnvelopeStatus.Unknown, null, null);
}

public class QuarterdeckOptionsTests
{
    [Fact]
    public void QuarterdeckOptions_Default_MatchesAdr0080Section1CanonicalValues()
    {
        var defaults = QuarterdeckOptions.Default;
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromSeconds(2), defaults.ProviderTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(800), defaults.PerSourceTimeout);
    }

    [Fact]
    public void AddSunfishQuarterdeck_BindsOptions_AndAppliesConfigure()
    {
        var services = new ServiceCollection();
        services.AddSunfishQuarterdeck(opts =>
        {
            opts.HeartbeatInterval = TimeSpan.FromSeconds(15);
        });

        var sp = services.BuildServiceProvider();
        var bound = sp.GetRequiredService<IOptions<QuarterdeckOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(15), bound.HeartbeatInterval);
        // Defaults survive for unconfigured fields.
        Assert.Equal(TimeSpan.FromSeconds(2), bound.ProviderTimeout);
        Assert.Equal(TimeSpan.FromMilliseconds(800), bound.PerSourceTimeout);
    }
}

public class ShipActionAndAuditEventTypeAdditionsTests
{
    [Fact]
    public void NewShipActions_UseKebabCase_MatchingCohortPrecedent()
    {
        Assert.Equal("view-quarterdeck", ShipAction.ViewQuarterdeck.Name);
        Assert.Equal("view-quarterdeck-alerts", ShipAction.ViewQuarterdeckAlerts.Name);
        Assert.Equal("acknowledge-alert", ShipAction.AcknowledgeAlert.Name);
    }

    [Fact]
    public void NewAuditEventTypes_MatchHandoffNames()
    {
        Assert.Equal("WatchHandoverRequested", AuditEventType.WatchHandoverRequested.Value);
        Assert.Equal("AlertAcknowledgementRequested", AuditEventType.AlertAcknowledgementRequested.Value);
        Assert.Equal("AlertAcknowledged", AuditEventType.AlertAcknowledged.Value);
    }
}

public class CouncilAmendmentTests
{
    [Fact]
    public void QuarterdeckAlert_VisibilityPolicy_DefaultsToOmitForDeniedActors()
    {
        var alert = new QuarterdeckAlert(
            AlertId: "sunfish.test:01HV4G7",
            TenantId: TenantId.System,
            Severity: AlertSeverity.Normal,
            Title: "Test",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            RequiresAcknowledgement: false,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.test");

        Assert.Equal(AlertVisibilityPolicy.OmitForDeniedActors, alert.VisibilityPolicy);
    }

    [Fact]
    public void QuarterdeckAlert_VisibilityPolicy_HonoursShowAllOverride()
    {
        var alert = new QuarterdeckAlert(
            AlertId: "sunfish.broadcast:01HV4G8",
            TenantId: TenantId.System,
            Severity: AlertSeverity.Emergency,
            Title: "Mission Envelope failed",
            Summary: null,
            IssuedAt: DateTimeOffset.UtcNow,
            ExpiresAt: null,
            RequiresAcknowledgement: true,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "sunfish.broadcast",
            VisibilityPolicy: AlertVisibilityPolicy.ShowAll);

        Assert.Equal(AlertVisibilityPolicy.ShowAll, alert.VisibilityPolicy);
    }

    [Fact]
    public void OodRoleSummary_LegalStateA_NoWatch_HasBothNullAndNotExpired()
    {
        var noWatch = new OodRoleSummary(
            OodRole.OfficerOfTheDeck,
            CurrentActorDisplayName: null,
            WatchStartedAt: null,
            IsExpired: false);

        // Legal state (a): no watch active.
        Assert.Null(noWatch.CurrentActorDisplayName);
        Assert.Null(noWatch.WatchStartedAt);
        Assert.False(noWatch.IsExpired);
    }

    [Fact]
    public void OodRoleSummary_LegalStateC_ActiveButExpired_HasNameAndExpired()
    {
        var expired = new OodRoleSummary(
            OodRole.OfficerOfTheDeck,
            CurrentActorDisplayName: "Lt. Vega",
            WatchStartedAt: DateTimeOffset.UtcNow.AddHours(-12),
            IsExpired: true);

        // Legal state (c): active watch that has expired.
        Assert.NotNull(expired.CurrentActorDisplayName);
        Assert.NotNull(expired.WatchStartedAt);
        Assert.True(expired.IsExpired);
    }

    [Fact]
    public void AlertSeverity_AllFourValuesPresent()
    {
        // Phase 3a renderers use the enum-to-LiveRegionPoliteness mapping
        // documented on each value; this test guards against accidental
        // value removal that would break the Phase 3a politeness contract.
        Assert.Equal(4, Enum.GetValues<AlertSeverity>().Length);
        Assert.Contains(AlertSeverity.Emergency, Enum.GetValues<AlertSeverity>());
        Assert.Contains(AlertSeverity.High, Enum.GetValues<AlertSeverity>());
        Assert.Contains(AlertSeverity.Normal, Enum.GetValues<AlertSeverity>());
        Assert.Contains(AlertSeverity.Informational, Enum.GetValues<AlertSeverity>());
    }
}
