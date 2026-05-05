using System;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;
using Xunit;

namespace Sunfish.Foundation.Wayfinder.Tests;

public class OodWatchShapeTests
{
    [Fact]
    public void OodWatchId_NewId_ProducesUniqueValues()
    {
        var a = OodWatchId.NewId();
        var b = OodWatchId.NewId();
        Assert.NotEqual(a, b);
        Assert.NotEqual(default, a.Value);
    }

    [Fact]
    public void OodWatch_RecordEquality_ComparesAllFields()
    {
        var id = OodWatchId.NewId();
        var tenant = new TenantId("tenant-a");
        var actor = new ActorId("actor-1");
        var startedBy = new ActorId("actor-0");
        var startedAt = DateTimeOffset.UtcNow;
        var maxDuration = TimeSpan.FromHours(8);

        var a = new OodWatch(id, tenant, actor, OodRole.OfficerOfTheDeck,
            startedAt, RelievedAt: null, startedBy, RelievedBy: null,
            maxDuration, OodWatchState.Active);
        var b = new OodWatch(id, tenant, actor, OodRole.OfficerOfTheDeck,
            startedAt, RelievedAt: null, startedBy, RelievedBy: null,
            maxDuration, OodWatchState.Active);
        Assert.Equal(a, b);

        var c = a with { State = OodWatchState.Relieved };
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void OodWatch_ImplementsIMustHaveTenant()
    {
        var watch = new OodWatch(
            OodWatchId.NewId(), new TenantId("acme"), new ActorId("alice"),
            OodRole.EngineeringOfficerOfTheWatch, DateTimeOffset.UtcNow, null,
            new ActorId("bob"), null, TimeSpan.FromHours(4), OodWatchState.Active);
        Assert.IsAssignableFrom<IMustHaveTenant>(watch);
        Assert.Equal("acme", watch.TenantId.Value);
    }

    [Fact]
    public void OodWatchConflictException_CarriesTriple()
    {
        var existing = OodWatchId.NewId();
        var tenant = new TenantId("tenant-x");
        var ex = new OodWatchConflictException(existing, tenant, OodRole.OfficerOfTheDeck);
        Assert.Equal(existing, ex.ExistingWatchId);
        Assert.Equal(tenant, ex.TenantId);
        Assert.Equal(OodRole.OfficerOfTheDeck, ex.Role);
        Assert.Contains("tenant-x", ex.Message);
        Assert.Contains("OfficerOfTheDeck", ex.Message);
    }

    [Fact]
    public void StandingOrder_IssuedDuringWatchId_DefaultsToNull()
    {
        // The 11th-positional optional param defaults to null when call-sites
        // supply only 10 args — verifies binary-compat for the W#49 P1
        // additive change to existing 3 call-sites.
        var order = new StandingOrder(
            new StandingOrderId(Guid.NewGuid()),
            new TenantId("acme"),
            new ActorId("alice"),
            DateTimeOffset.UtcNow,
            StandingOrderScope.Tenant,
            System.Array.Empty<StandingOrderTriple>(),
            "test rationale",
            ApprovalChain: null,
            new AuditRecordId(Guid.NewGuid()),
            StandingOrderState.Issued);
        Assert.Null(order.IssuedDuringWatchId);
    }
}
