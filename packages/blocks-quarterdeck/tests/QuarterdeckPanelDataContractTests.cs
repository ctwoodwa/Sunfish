using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Quarterdeck;
using Sunfish.Foundation.Wayfinder;
using Xunit;

namespace Sunfish.Blocks.Quarterdeck.Tests;

/// <summary>
/// W#51 Phase 3a — data-contract coverage for provider shapes consumed by
/// WatchStatusPanel, AlertTickerPanel, and KpiCardGrid.
/// Razor components themselves are not unit-tested here (no bUnit);
/// these tests guard the data contracts that feed the panels.
/// </summary>
public class QuarterdeckPanelDataContractTests
{
    private static readonly TenantId TenantA = new("alpha");
    private static readonly ActorId ActorA   = new("actor-a");

    // ── QuarterdeckSnapshot shape ─────────────────────────────────────────────

    [Fact]
    public void QuarterdeckSnapshot_AllRequiredFields_Populated()
    {
        var oodSummary = new OodWatchSummary(
            new OodRoleSummary(OodRole.OfficerOfTheDeck, "Alice", DateTimeOffset.UtcNow, false),
            new OodRoleSummary(OodRole.EngineeringOfficerOfTheWatch, null, null, false));

        var snapshot = new QuarterdeckSnapshot(
            OodWatch: oodSummary,
            MissionEnvelope: new MissionEnvelopeSummary(MissionEnvelopeStatus.Passed, "v1.0", DateTimeOffset.UtcNow),
            RecentOrders: [],
            PendingAlerts: [],
            KpiCards: [],
            DepartmentLinks: [],
            SnapshotAt: DateTimeOffset.UtcNow);

        Assert.Equal(MissionEnvelopeStatus.Passed, snapshot.MissionEnvelope.Status);
        Assert.Equal("Alice", snapshot.OodWatch.OfficerOfTheDeck.CurrentActorDisplayName);
        Assert.Null(snapshot.OodWatch.EngineeringOfficerOfTheWatch.CurrentActorDisplayName);
        Assert.False(snapshot.OodWatch.OfficerOfTheDeck.IsExpired);
    }

    // ── OodRoleSummary invariant — (null, true) is undefined behaviour ─────────

    [Fact]
    public void OodRoleSummary_NoWatchActive_HasNullActorAndFalseExpired()
    {
        var summary = new OodRoleSummary(OodRole.OfficerOfTheDeck, null, null, false);

        // ADR 0080 §6 a11y invariant: (null, false) = "no watch active"
        Assert.Null(summary.CurrentActorDisplayName);
        Assert.Null(summary.WatchStartedAt);
        Assert.False(summary.IsExpired);
    }

    [Fact]
    public void OodRoleSummary_ActiveWatch_HasActorAndFalseExpired()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var summary = new OodRoleSummary(OodRole.OfficerOfTheDeck, "Bob", startedAt, false);

        // ADR 0080 §6 a11y invariant: (name, false) = "active watch"
        Assert.Equal("Bob", summary.CurrentActorDisplayName);
        Assert.Equal(startedAt, summary.WatchStartedAt);
        Assert.False(summary.IsExpired);
    }

    [Fact]
    public void OodRoleSummary_ExpiredWatch_HasActorAndTrueExpired()
    {
        var startedAt = DateTimeOffset.UtcNow.AddHours(-10);
        var summary = new OodRoleSummary(OodRole.OfficerOfTheDeck, "Carol", startedAt, true);

        // ADR 0080 §6 a11y invariant: (name, true) = "active but expired watch"
        Assert.Equal("Carol", summary.CurrentActorDisplayName);
        Assert.True(summary.IsExpired);
    }

    // ── AlertSeverity → live-region politeness mapping ────────────────────────

    [Fact]
    public void AlertSeverity_EnumOrder_Emergency_IsLowest_Ordinal()
    {
        // AlertSeverity enum order is the canonical sort priority — lower ordinal sorts first.
        // Emergency (0) < High (1) < Normal (2) < Informational (3).
        Assert.True((int)AlertSeverity.Emergency < (int)AlertSeverity.High);
        Assert.True((int)AlertSeverity.High      < (int)AlertSeverity.Normal);
        Assert.True((int)AlertSeverity.Normal    < (int)AlertSeverity.Informational);
    }

    // ── DepartmentKpi denied-not-hidden invariant ─────────────────────────────

    [Fact]
    public void DepartmentKpi_Denied_RendersWithDeniedStatusNotHidden()
    {
        // ADR 0080 §2.3 rule 9: denied cards must render (denied-not-hidden);
        // Value must be a SR-meaningful string, not a punctuation glyph.
        var card = new DepartmentKpi(
            SourceName: "sunfish.engine-room",
            Label: "Engine Room",
            Value: "Permission required",
            Unit: null,
            Status: DepartmentStatus.Denied);

        Assert.Equal(DepartmentStatus.Denied, card.Status);
        Assert.False(string.IsNullOrWhiteSpace(card.Value));
        // Value must NOT be a punctuation glyph that SR reads verbosely.
        Assert.DoesNotMatch(@"^[-–—…]$", card.Value);
    }

    [Fact]
    public void DepartmentKpi_Accessible_HasNormalValue()
    {
        var card = new DepartmentKpi(
            SourceName: "sunfish.engine-room",
            Label: "Sync peers",
            Value: "3",
            Unit: "peers",
            Status: DepartmentStatus.Accessible);

        Assert.Equal(DepartmentStatus.Accessible, card.Status);
        Assert.Equal("3", card.Value);
        Assert.Equal("peers", card.Unit);
    }

    // ── IOodWatchService.GetActiveWatchAsync contract ─────────────────────────

    [Fact]
    public async Task GetActiveWatchAsync_ReturnsNull_WhenNoActiveWatch()
    {
        var watchService = Substitute.For<IOodWatchService>();
        watchService
            .GetActiveWatchAsync(TenantA, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<OodWatch?>(default(OodWatch?)));

        var result = await watchService.GetActiveWatchAsync(TenantA, OodRole.OfficerOfTheDeck);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetActiveWatchAsync_ReturnsWatch_WhenActiveWatchExists()
    {
        var watchId = OodWatchId.NewId();
        var watch = new OodWatch(
            Id: watchId,
            TenantId: TenantA,
            OnWatchActor: ActorA,
            Role: OodRole.OfficerOfTheDeck,
            StartedAt: DateTimeOffset.UtcNow.AddHours(-1),
            RelievedAt: null,
            StartedBy: ActorA,
            RelievedBy: null,
            MaxWatchDuration: TimeSpan.FromHours(4),
            State: OodWatchState.Active);

        var watchService = Substitute.For<IOodWatchService>();
        watchService
            .GetActiveWatchAsync(TenantA, OodRole.OfficerOfTheDeck, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<OodWatch?>(watch));

        var result = await watchService.GetActiveWatchAsync(TenantA, OodRole.OfficerOfTheDeck);

        Assert.NotNull(result);
        Assert.Equal(ActorA, result.OnWatchActor);
        Assert.Equal(OodWatchState.Active, result.State);
    }

    // ── QuarterdeckSnapshot.PendingAlerts sort order ──────────────────────────

    [Fact]
    public void PendingAlerts_SortOrder_EmergencyBeforeHigh()
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = new List<QuarterdeckAlert>
        {
            MakeAlert(AlertSeverity.High,      "High alert",      now.AddMinutes(-5)),
            MakeAlert(AlertSeverity.Emergency, "Emergency alert", now.AddMinutes(-1)),
            MakeAlert(AlertSeverity.Normal,    "Normal alert",    now),
        };

        // Sort ascending by AlertSeverity ordinal (Emergency=0 sorts first).
        var sorted = alerts.OrderBy(a => a.Severity).ThenByDescending(a => a.IssuedAt).ToList();

        Assert.Equal(AlertSeverity.Emergency,    sorted[0].Severity);
        Assert.Equal(AlertSeverity.High,         sorted[1].Severity);
        Assert.Equal(AlertSeverity.Normal,       sorted[2].Severity);
    }

    [Fact]
    public void PendingAlerts_WithinSameSeverity_NewestFirst()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var alerts = new List<QuarterdeckAlert>
        {
            MakeAlert(AlertSeverity.High, "Older high", baseTime.AddMinutes(-10)),
            MakeAlert(AlertSeverity.High, "Newer high", baseTime.AddMinutes(-1)),
        };

        var sorted = alerts.OrderBy(a => a.Severity).ThenByDescending(a => a.IssuedAt).ToList();

        Assert.Equal("Newer high", sorted[0].Title);
        Assert.Equal("Older high", sorted[1].Title);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static QuarterdeckAlert MakeAlert(AlertSeverity severity, string title, DateTimeOffset issuedAt) =>
        new(
            AlertId: $"test:{Guid.NewGuid():N}",
            TenantId: TenantA,
            Severity: severity,
            Title: title,
            Summary: null,
            IssuedAt: issuedAt,
            ExpiresAt: null,
            RequiresAcknowledgement: false,
            IsAcknowledged: false,
            AcknowledgedBy: null,
            AcknowledgedAt: null,
            SourceName: "test.source");
}
