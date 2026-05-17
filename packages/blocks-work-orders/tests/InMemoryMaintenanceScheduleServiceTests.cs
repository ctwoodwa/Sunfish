using Sunfish.Blocks.WorkOrders.Events;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Blocks.WorkOrders.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for
/// <see cref="InMemoryMaintenanceScheduleService.GenerateDueWorkOrdersAsync"/>
/// per schema §4.3 idempotency contract.
/// </summary>
public sealed class InMemoryMaintenanceScheduleServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly MaintenanceTaskTemplate Template = new(
        Title:    "Monthly inspection",
        Priority: Priority.Normal);

    [Fact]
    public async Task GenerateDueWorkOrders_FirstRun_CreatesWorkOrders()
    {
        var (svc, woSvc) = NewHarness();
        var ms = await svc.CreateAsync(
            Tenant, "Monthly", "FREQ=MONTHLY",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "UTC",
            taskTemplate: Template,
            createdBy: Actor,
            generateLeadDays: 0,
            lookaheadHorizonDays: 90);

        var tasks = await svc.GenerateDueWorkOrdersAsync(ms.Id, new DateOnly(2026, 1, 1), Actor);

        // Jan 1 + Feb 1 + Mar 1 + Apr 1 = 4 within the 90-day horizon.
        Assert.Equal(4, tasks.Count);
        Assert.All(tasks, t => Assert.NotNull(t.WorkOrderId));
    }

    [Fact]
    public async Task GenerateDueWorkOrders_SecondRun_Idempotent()
    {
        var (svc, _) = NewHarness();
        var ms = await svc.CreateAsync(
            Tenant, "Monthly", "FREQ=MONTHLY",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "UTC",
            taskTemplate: Template,
            createdBy: Actor,
            generateLeadDays: 0,
            lookaheadHorizonDays: 90);

        var first = await svc.GenerateDueWorkOrdersAsync(ms.Id, new DateOnly(2026, 1, 1), Actor);
        var second = await svc.GenerateDueWorkOrdersAsync(ms.Id, new DateOnly(2026, 1, 1), Actor);

        Assert.Equal(first.Count, second.Count);
        // Same tasks — same MaintenanceTask ids returned.
        Assert.Equal(
            first.Select(t => t.Id).OrderBy(i => i.Value),
            second.Select(t => t.Id).OrderBy(i => i.Value));
    }

    [Fact]
    public async Task GenerateDueWorkOrders_PausedSchedule_ReturnsEmpty()
    {
        var (svc, _) = NewHarness();
        var ms = await svc.CreateAsync(
            Tenant, "Monthly", "FREQ=MONTHLY",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "UTC",
            taskTemplate: Template,
            createdBy: Actor);
        await svc.PauseAsync(ms.Id, Actor);

        var tasks = await svc.GenerateDueWorkOrdersAsync(ms.Id, new DateOnly(2026, 1, 1), Actor);

        Assert.Empty(tasks);
    }

    [Fact]
    public async Task GenerateDueWorkOrders_CreatesPreventiveMaintenanceKind()
    {
        var (svc, woSvc) = NewHarness();
        var ms = await svc.CreateAsync(
            Tenant, "Quarterly HVAC", "FREQ=MONTHLY;INTERVAL=3",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "UTC",
            taskTemplate: Template,
            createdBy: Actor,
            generateLeadDays: 0,
            lookaheadHorizonDays: 365);

        var tasks = await svc.GenerateDueWorkOrdersAsync(ms.Id, new DateOnly(2026, 1, 1), Actor);
        Assert.NotEmpty(tasks);

        var wo = await woSvc.GetByIdAsync(Tenant, tasks[0].WorkOrderId!.Value);
        Assert.NotNull(wo);
        Assert.Equal(WorkOrderKind.PreventiveMaintenance, wo!.Kind);
    }

    private static (InMemoryMaintenanceScheduleService Svc, InMemoryWorkOrderService WoSvc) NewHarness()
    {
        var repo = new InMemoryWorkOrderRepository();
        var events = new InMemoryWorkOrderEventPublisher();
        var woSvc = new InMemoryWorkOrderService(repo, events);
        var rrule = new InMemoryRruleExpansionService();
        var svc = new InMemoryMaintenanceScheduleService(rrule, woSvc);
        return (svc, woSvc);
    }
}
