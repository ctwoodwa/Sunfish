using Sunfish.Blocks.WorkOrders.Events;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Blocks.WorkOrders.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 PR 4 — coverage for <see cref="InMemoryWorkOrderService"/>
/// + tenant-isolation gate per hand-off H5.
/// </summary>
public sealed class InMemoryWorkOrderServiceTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly TenantId OtherTenant = new("other-tenant-2");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task CreateWorkOrder_ValidInput_ReturnsNew()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Test repair", WorkOrderKind.Repair, Priority.Normal, Actor);

        Assert.NotNull(wo);
        Assert.Equal(WorkOrderStatus.New, wo.Status);
        Assert.Single(events.Events.OfType<WorkOrderCreatedEvent>());
    }

    [Fact]
    public async Task TransitionWorkOrder_ValidTransition_Success()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkOrderKind.Task, Priority.Normal, Actor);

        var triaged = await svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Triaged, Actor);

        Assert.Equal(WorkOrderStatus.Triaged, triaged.Status);
    }

    [Fact]
    public async Task TransitionWorkOrder_InvalidTransition_Throws()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkOrderKind.Task, Priority.Normal, Actor);

        await Assert.ThrowsAsync<InvalidStatusTransitionException>(
            () => svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Completed, Actor));
    }

    [Fact]
    public async Task TransitionToCompleted_EmitsWorkOrderCompletedEvent()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkOrderKind.Task, Priority.Normal, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Triaged, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Scheduled, Actor);
        await svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.InProgress, Actor);

        await svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Completed, Actor);

        Assert.Single(events.Events.OfType<WorkOrderCompletedEvent>());
    }

    [Fact]
    public async Task AssignWorkOrder_SetsAssigneeAndContractor()
    {
        var (svc, _, events) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkOrderKind.Task, Priority.Normal, Actor);
        var assignee = Guid.NewGuid();
        var contractorId = Guid.NewGuid();

        var assigned = await svc.AssignAsync(Tenant, wo.Id, assignee, contractorId, Actor);

        Assert.Equal(assignee, assigned.AssignedToPartyId);
        Assert.Equal(contractorId, assigned.ContractorId);
        Assert.Single(events.Events.OfType<WorkOrderAssignedEvent>());
    }

    [Fact]
    public async Task SoftDelete_SetsDeletedAt_CannotTransition()
    {
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Walk", WorkOrderKind.Task, Priority.Normal, Actor);

        await svc.SoftDeleteAsync(Tenant, wo.Id, Actor);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.TransitionAsync(Tenant, wo.Id, WorkOrderStatus.Triaged, Actor));
    }

    [Fact]
    public async Task GetById_WrongTenant_ReturnsNull()
    {
        // H5 invariant: cross-tenant reads must fail closed.
        var (svc, _, _) = NewHarness();
        var wo = await svc.CreateAsync(Tenant, "Tenant A repair", WorkOrderKind.Repair, Priority.Normal, Actor);

        var crossTenantRead = await svc.GetByIdAsync(OtherTenant, wo.Id);

        Assert.Null(crossTenantRead);
    }

    [Fact]
    public async Task CreateRepairTicket_ReturnsPersistedTicket()
    {
        var (svc, _, _) = NewHarness();
        var ticket = await svc.CreateRepairTicketAsync(
            Tenant, "Sink clogged", Actor, description: "Kitchen sink");

        Assert.NotNull(ticket);
        Assert.Equal("Sink clogged", ticket.Title);
    }

    private static (InMemoryWorkOrderService Svc, InMemoryWorkOrderRepository Repo, InMemoryWorkOrderEventPublisher Events) NewHarness()
    {
        var repo = new InMemoryWorkOrderRepository();
        var events = new InMemoryWorkOrderEventPublisher();
        var svc = new InMemoryWorkOrderService(repo, events);
        return (svc, repo, events);
    }
}
