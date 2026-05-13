using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Quarterdeck;
using Sunfish.Foundation.Tactical;
using Xunit;

namespace Sunfish.Blocks.Tactical.Tests;

/// <summary>
/// W#52 Phase 4 — LookoutQuarterdeckAlertSource unit tests per ADR 0081 §7.2 + hand-off.
/// </summary>
public class LookoutQuarterdeckAlertSourceTests
{
    private static readonly TenantId TenantA = new("00000000-0000-0000-0000-000000000001");
    private static readonly TenantId TenantB = new("00000000-0000-0000-0000-000000000002");
    private static readonly ActorId TestActor = new("dGVzdC11c2VyLTAx");

    private sealed class FakeTenantContext : ITenantContext
    {
        public TenantMetadata? Tenant { get; init; }
    }

    private sealed class FakeLookout : ILookout
    {
        private readonly List<TacticalAlert> _alerts;

        public FakeLookout(IEnumerable<TacticalAlert> alerts)
        {
            _alerts = new List<TacticalAlert>(alerts);
        }

        public IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId) =>
            _alerts.Where(a => a.TenantId == tenantId).ToList();

        public ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default) =>
            ValueTask.CompletedTask;

        public IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(
            TenantId tenantId, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private static TacticalAlert MakeAlert(
        string alertId = "rule:001:alert001",
        TenantId? tenantId = null,
        Sunfish.Foundation.Tactical.AlertSeverity severity = Sunfish.Foundation.Tactical.AlertSeverity.High,
        AlertStatus status = AlertStatus.Active,
        DateTimeOffset? detectedAt = null,
        bool requiresAck = false,
        string title = "Test alert",
        string summary = "") =>
        new(
            AlertId: alertId,
            TenantId: tenantId ?? TenantA,
            RuleName: "TestRule",
            Severity: severity,
            RoutingPolicy: AlertRoutingPolicy.HighPriorityLookout,
            Title: title,
            Summary: summary,
            DetectedAt: detectedAt ?? DateTimeOffset.UtcNow,
            Status: status,
            RequiresAcknowledgement: requiresAck,
            RunbookStepIds: Array.Empty<string>(),
            AcknowledgedBy: null,
            AcknowledgedAt: null);

    private static LookoutQuarterdeckAlertSource MakeSource(
        IEnumerable<TacticalAlert>? alerts = null,
        TenantId? ambientTenant = null)
    {
        var tenant = ambientTenant ?? TenantA;
        var tenantContext = new FakeTenantContext
        {
            Tenant = new TenantMetadata { Id = tenant, Name = "test-tenant" }
        };
        var lookout = new FakeLookout(alerts ?? Enumerable.Empty<TacticalAlert>());
        return new LookoutQuarterdeckAlertSource(lookout, tenantContext);
    }

    [Fact]
    public void SourceName_is_sunfish_tactical_lookout()
    {
        var source = MakeSource();
        Assert.Equal("sunfish.tactical.lookout", source.SourceName);
    }

    [Fact]
    public async Task GetAlertsAsync_rejects_tenant_mismatch()
    {
        // Ambient context is TenantA; caller requests TenantB → reject, return empty.
        var alert = MakeAlert(tenantId: TenantB);
        var source = MakeSource(new[] { alert }, ambientTenant: TenantA);

        var results = await source.GetAlertsAsync(TenantB, TestActor).ToListAsync();

        Assert.Empty(results);
    }

    [Fact]
    public async Task GetAlertsAsync_filters_to_tenantId_defense_in_depth()
    {
        // Defense-in-depth: even if ILookout returns cross-tenant alerts, source filters them.
        // We inject a lookout that bypasses per-tenant filtering to simulate the vulnerability.
        var alertA = MakeAlert(alertId: "rule:001:aaa", tenantId: TenantA);
        var alertB = MakeAlert(alertId: "rule:001:bbb", tenantId: TenantB);
        var permissiveLookout = new PermissiveLookout(new[] { alertA, alertB });
        var tenantContext = new FakeTenantContext
        {
            Tenant = new TenantMetadata { Id = TenantA, Name = "test-tenant" }
        };
        var source = new LookoutQuarterdeckAlertSource(permissiveLookout, tenantContext);

        var results = await source.GetAlertsAsync(TenantA, TestActor).ToListAsync();

        Assert.All(results, r => Assert.Equal(TenantA, r.TenantId));
        Assert.DoesNotContain(results, r => r.AlertId == "rule:001:bbb");
    }

    [Fact]
    public async Task GetAlertsAsync_maps_TacticalAlert_to_QuarterdeckAlert_with_OmitForDeniedActors()
    {
        var alert = MakeAlert(
            alertId: "rule:001:xyz",
            severity: Sunfish.Foundation.Tactical.AlertSeverity.Critical,
            title: "Critical anomaly",
            summary: "Summary text",
            requiresAck: true);
        var source = MakeSource(new[] { alert });

        var results = await source.GetAlertsAsync(TenantA, TestActor).ToListAsync();

        Assert.Single(results);
        var q = results[0];
        Assert.Equal("rule:001:xyz", q.AlertId);
        Assert.Equal(TenantA, q.TenantId);
        Assert.Equal(Sunfish.Foundation.Quarterdeck.AlertSeverity.Emergency, q.Severity);
        Assert.Equal("Critical anomaly", q.Title);
        Assert.Equal("Summary text", q.Summary);
        Assert.True(q.RequiresAcknowledgement);
        Assert.Equal(AlertVisibilityPolicy.OmitForDeniedActors, q.VisibilityPolicy);
        Assert.Equal("sunfish.tactical.lookout", q.SourceName);
    }

    [Fact]
    public async Task GetAlertsAsync_caps_at_50_items()
    {
        var alerts = Enumerable.Range(1, 60)
            .Select(i => MakeAlert(
                alertId: $"rule:001:alert{i:D3}",
                detectedAt: DateTimeOffset.UtcNow.AddMinutes(-i)))
            .ToList();
        var source = MakeSource(alerts);

        var results = await source.GetAlertsAsync(TenantA, TestActor).ToListAsync();

        Assert.Equal(50, results.Count);
    }

    [Fact]
    public async Task GetAlertsAsync_sorts_DetectedAt_desc()
    {
        var now = DateTimeOffset.UtcNow;
        var alerts = new[]
        {
            MakeAlert(alertId: "rule:001:old", detectedAt: now.AddHours(-3)),
            MakeAlert(alertId: "rule:001:new", detectedAt: now.AddHours(-1)),
            MakeAlert(alertId: "rule:001:mid", detectedAt: now.AddHours(-2)),
        };
        var source = MakeSource(alerts);

        var results = await source.GetAlertsAsync(TenantA, TestActor).ToListAsync();

        Assert.Equal(3, results.Count);
        Assert.Equal("rule:001:new", results[0].AlertId);
        Assert.Equal("rule:001:mid", results[1].AlertId);
        Assert.Equal("rule:001:old", results[2].AlertId);
    }

    // Bypasses per-tenant filtering to test defense-in-depth in the source.
    private sealed class PermissiveLookout : ILookout
    {
        private readonly List<TacticalAlert> _all;
        public PermissiveLookout(IEnumerable<TacticalAlert> alerts) => _all = new List<TacticalAlert>(alerts);
        public IReadOnlyList<TacticalAlert> GetActiveLookoutAlerts(TenantId tenantId) => _all;
        public ValueTask WriteAsync(TacticalAlert alert, CancellationToken ct = default) => ValueTask.CompletedTask;
        public IAsyncEnumerable<IReadOnlyList<TacticalAlert>> SubscribeLookoutAsync(TenantId tenantId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
