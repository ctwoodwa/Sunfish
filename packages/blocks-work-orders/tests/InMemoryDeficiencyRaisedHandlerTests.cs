using Sunfish.Blocks.WorkOrders.Events;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Blocks.WorkOrders.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for
/// <see cref="InMemoryDeficiencyRaisedHandler"/> per schema §4.1
/// idempotency + severity-mapping contract.
/// </summary>
public sealed class InMemoryDeficiencyRaisedHandlerTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task Handle_NewDeficiency_CreatesWorkOrder()
    {
        var (handler, _) = NewHarness();
        var evt = new DeficiencyRaisedEvent(
            DeficiencyId: Guid.NewGuid(),
            PropertyId:   Guid.NewGuid(),
            UnitId:       null,
            AssetId:      null,
            Severity:     "minor",
            Description:  "Loose cabinet handle");

        var wo = await handler.HandleAsync(evt, Actor);

        Assert.NotNull(wo);
        Assert.Equal(WorkOrderKind.Repair, wo.Kind);
        Assert.Equal(evt.DeficiencyId, wo.DeficiencyId);
    }

    [Fact]
    public async Task Handle_ExistingDeficiency_Idempotent()
    {
        var (handler, _) = NewHarness();
        var evt = new DeficiencyRaisedEvent(
            DeficiencyId: Guid.NewGuid(),
            PropertyId:   Guid.NewGuid(),
            UnitId:       null,
            AssetId:      null,
            Severity:     "minor",
            Description:  "Loose cabinet handle");
        var first = await handler.HandleAsync(evt, Actor);

        var second = await handler.HandleAsync(evt, Actor);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task Handle_SeveritySafety_SetsSeverityAndDueBy()
    {
        var (handler, _) = NewHarness();
        var evt = new DeficiencyRaisedEvent(
            DeficiencyId: Guid.NewGuid(),
            PropertyId:   Guid.NewGuid(),
            UnitId:       null,
            AssetId:      null,
            Severity:     "safety",
            Description:  "Gas leak detected");

        var wo = await handler.HandleAsync(evt, Actor);

        Assert.Equal(WorkOrderSeverity.Safety, wo.Severity);
        Assert.Equal(Priority.Critical, wo.Priority);
        Assert.NotNull(wo.DueBy);
    }

    [Fact]
    public async Task Handle_SeverityHabitability_SetsSeverityAndDueBy()
    {
        var (handler, _) = NewHarness();
        var evt = new DeficiencyRaisedEvent(
            DeficiencyId: Guid.NewGuid(),
            PropertyId:   Guid.NewGuid(),
            UnitId:       null,
            AssetId:      null,
            Severity:     "habitability",
            Description:  "No heat in unit");

        var wo = await handler.HandleAsync(evt, Actor);

        Assert.Equal(WorkOrderSeverity.Habitability, wo.Severity);
        Assert.NotNull(wo.DueBy);
    }

    [Fact]
    public async Task Handle_UnknownSeverity_DefaultsToMajor()
    {
        var (handler, _) = NewHarness();
        var evt = new DeficiencyRaisedEvent(
            DeficiencyId: Guid.NewGuid(),
            PropertyId:   Guid.NewGuid(),
            UnitId:       null,
            AssetId:      null,
            Severity:     "blah-unrecognized",
            Description:  "Something is wrong");

        var wo = await handler.HandleAsync(evt, Actor);

        Assert.Equal(WorkOrderSeverity.Major, wo.Severity);
    }

    private static (InMemoryDeficiencyRaisedHandler Handler, InMemoryWorkOrderRepository Repo) NewHarness()
    {
        var repo = new InMemoryWorkOrderRepository();
        var events = new InMemoryWorkOrderEventPublisher();
        var svc = new InMemoryWorkOrderService(repo, events);
        var handler = new InMemoryDeficiencyRaisedHandler(repo, svc, Tenant);
        return (handler, repo);
    }
}
