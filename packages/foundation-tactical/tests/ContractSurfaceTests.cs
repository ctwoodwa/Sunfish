using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Tactical;
using Sunfish.Kernel.Audit;
using Xunit;

namespace Sunfish.Foundation.Tactical.Tests;

/// <summary>
/// W#52 Phase 1 contract-surface tests. Verifies every interface +
/// data type required by the hand-off §1.8 acceptance gate is
/// present with the documented method shapes. Phase 2 brings
/// behavioural tests against concrete implementations; Phase 1's
/// goal is to guarantee Phase 2 implementers + cohort sibling
/// authors find the surface as documented.
/// </summary>
public class ContractSurfaceTests
{
    [Fact]
    public void ITacticalRule_has_required_members()
    {
        var t = typeof(ITacticalRule);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetProperty(nameof(ITacticalRule.RuleName)));
        Assert.NotNull(t.GetProperty(nameof(ITacticalRule.DefaultSeverity)));
        Assert.NotNull(t.GetProperty(nameof(ITacticalRule.DefaultRoutingPolicy)));
        var evaluate = t.GetMethod(nameof(ITacticalRule.Evaluate));
        Assert.NotNull(evaluate);
        Assert.Equal(typeof(bool), evaluate!.ReturnType);
        Assert.Equal(2, evaluate.GetParameters().Length);
    }

    [Fact]
    public void ITacticalRuleEngine_has_required_members()
    {
        var t = typeof(ITacticalRuleEngine);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ITacticalRuleEngine.RegisterRule)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalRuleEngine.Evaluate)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalRuleEngine.EvaluateStreamAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalRuleEngine.GetRegisteredRules)));
    }

    [Fact]
    public void IAlertRouter_has_required_members()
    {
        var t = typeof(IAlertRouter);
        Assert.True(t.IsInterface);
        var route = t.GetMethod(nameof(IAlertRouter.RouteAsync));
        Assert.NotNull(route);
        Assert.Equal(2, route!.GetParameters().Length);
    }

    [Fact]
    public void ISonarStore_has_required_members()
    {
        var t = typeof(ISonarStore);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ISonarStore.WriteAsync)));
        Assert.NotNull(t.GetMethod(nameof(ISonarStore.GetActiveAlerts)));
    }

    [Fact]
    public void ILookout_has_required_members()
    {
        var t = typeof(ILookout);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ILookout.WriteAsync)));
        Assert.NotNull(t.GetMethod(nameof(ILookout.GetActiveLookoutAlerts)));
        Assert.NotNull(t.GetMethod(nameof(ILookout.SubscribeLookoutAsync)));
    }

    [Fact]
    public void ITacticalDataProvider_has_required_members()
    {
        var t = typeof(ITacticalDataProvider);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ITacticalDataProvider.GetSnapshotAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalDataProvider.GetAlertsAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalDataProvider.GetActiveIncidentsAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalDataProvider.SubscribeSnapshotAsync)));
    }

    [Fact]
    public void ITacticalCommandService_has_required_members()
    {
        var t = typeof(ITacticalCommandService);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ITacticalCommandService.AcknowledgeAlertAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalCommandService.OpenIncidentAsync)));
        Assert.NotNull(t.GetMethod(nameof(ITacticalCommandService.CloseIncidentAsync)));
    }

    [Fact]
    public void IThreatTriggerService_has_required_members()
    {
        var t = typeof(IThreatTriggerService);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(IThreatTriggerService.RegisterTemplate)));
        Assert.NotNull(t.GetMethod(nameof(IThreatTriggerService.TryIssueAsync)));
    }

    [Fact]
    public void ISystemPrincipalProvider_has_required_members()
    {
        var t = typeof(ISystemPrincipalProvider);
        Assert.True(t.IsInterface);
        Assert.NotNull(t.GetMethod(nameof(ISystemPrincipalProvider.GetSystemPrincipalAsync)));
    }

    [Fact]
    public void TacticalOptions_Default_values_are_within_normative_bounds()
    {
        var defaults = TacticalOptions.Default;
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.HeartbeatInterval);
        Assert.Equal(200, defaults.MaxActiveAlerts);
        Assert.Equal(TimeSpan.FromHours(24), defaults.AlertTtl);
        Assert.Equal(100, defaults.SignalBatchSize);
        Assert.Equal(50, defaults.MaxActiveIncidents);
        Assert.Equal(3, defaults.MaxEmergencyOrdersPerMinute);
        Assert.Equal(60, defaults.MaxAlertsPerMinutePerRule);

        // Normative lower bounds
        Assert.True(defaults.HeartbeatInterval > TimeSpan.Zero);
        Assert.True(defaults.AlertTtl > TimeSpan.Zero);
        Assert.True(defaults.MaxActiveAlerts > 0);
        Assert.True(defaults.SignalBatchSize > 0);
        Assert.True(defaults.MaxActiveIncidents > 0);
        Assert.True(defaults.MaxEmergencyOrdersPerMinute > 0);
        Assert.True(defaults.MaxAlertsPerMinutePerRule > 0);
    }

    [Fact]
    public void AuditEventType_constants_have_expected_string_values()
    {
        Assert.Equal("AnomalyDetected", AuditEventType.AnomalyDetected.Value);
        Assert.Equal("AlertRouted", AuditEventType.AlertRouted.Value);
        Assert.Equal("TacticalAlertExpired", AuditEventType.TacticalAlertExpired.Value);
        Assert.Equal("LookoutAlertEvicted", AuditEventType.LookoutAlertEvicted.Value);
        Assert.Equal("TacticalAlertAcknowledgementRequested", AuditEventType.TacticalAlertAcknowledgementRequested.Value);
        Assert.Equal("TacticalAlertAcknowledged", AuditEventType.TacticalAlertAcknowledged.Value);
        Assert.Equal("IncidentOpenRequested", AuditEventType.IncidentOpenRequested.Value);
        Assert.Equal("IncidentOpened", AuditEventType.IncidentOpened.Value);
        Assert.Equal("IncidentCloseRequested", AuditEventType.IncidentCloseRequested.Value);
        Assert.Equal("IncidentClosed", AuditEventType.IncidentClosed.Value);
        Assert.Equal("EmergencyStandingOrderIssued", AuditEventType.EmergencyStandingOrderIssued.Value);
        Assert.Equal("EmergencyStandingOrderIssuanceFailed", AuditEventType.EmergencyStandingOrderIssuanceFailed.Value);
        Assert.Equal("TacticalAuthorizationDenied", AuditEventType.TacticalAuthorizationDenied.Value);
    }

    [Fact]
    public void ShipAction_constants_use_kebab_case_per_cohort_convention()
    {
        Assert.Equal("view-tactical", ShipAction.ViewTactical.Name);
        Assert.Equal("view-fire-control", ShipAction.ViewFireControl.Name);
        Assert.Equal("acknowledge-tactical-alert", ShipAction.AcknowledgeTacticalAlert.Name);
        Assert.Equal("open-incident", ShipAction.OpenIncident.Name);
        Assert.Equal("close-incident", ShipAction.CloseIncident.Name);
        Assert.Equal("issue-emergency-standing-order", ShipAction.IssueEmergencyStandingOrder.Name);
        Assert.Equal("manage-threat-triggers", ShipAction.ManageThreatTriggers.Name);
    }

    [Fact]
    public void TacticalUnauthorizedException_inherits_UnauthorizedAccessException()
    {
        // ADR 0081 §8 invariant: retry policies that suppress
        // UnauthorizedAccessException MUST suppress this type too;
        // retry logic MUST NOT swallow the throw.
        Assert.True(typeof(UnauthorizedAccessException).IsAssignableFrom(typeof(TacticalUnauthorizedException)));

        var ex = new TacticalUnauthorizedException("denied");
        Assert.Equal("denied", ex.Message);
    }

    [Fact]
    public void Enums_have_expected_value_counts()
    {
        Assert.Equal(10, Enum.GetValues<TacticalSignalKind>().Length);
        Assert.Equal(5, Enum.GetValues<AlertSeverity>().Length);
        Assert.Equal(2, Enum.GetValues<AlertRoutingPolicy>().Length);
        Assert.Equal(4, Enum.GetValues<AlertStatus>().Length);
        Assert.Equal(3, Enum.GetValues<IncidentStatus>().Length);
    }

    [Fact]
    public void TacticalOptions_AllowedHighPriorityRulePrefixes_DefaultsToSunfishOnly()
    {
        var defaults = TacticalOptions.Default;
        Assert.Single(defaults.AllowedHighPriorityRulePrefixes);
        Assert.Equal("sunfish.*", defaults.AllowedHighPriorityRulePrefixes[0]);
    }

    [Fact]
    public void AddSunfishTactical_BindsOptions_AndAppliesConfigure()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddSunfishTactical(opts =>
        {
            opts.HeartbeatInterval = TimeSpan.FromSeconds(15);
        });

        var sp = services.BuildServiceProvider();
        var bound = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TacticalOptions>>().Value;
        Assert.Equal(TimeSpan.FromSeconds(15), bound.HeartbeatInterval);
        // Defaults survive for unconfigured fields.
        Assert.Equal(200, bound.MaxActiveAlerts);
        Assert.Equal(3, bound.MaxEmergencyOrdersPerMinute);
        Assert.Equal("sunfish.*", bound.AllowedHighPriorityRulePrefixes[0]);
    }
}
