using Sunfish.Blocks.WorkOrders.Models;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="RepairTicket"/>.
/// </summary>
public sealed class RepairTicketTests
{
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() => RepairTicket.Create("  ", Actor));
    }

    [Fact]
    public void ConvertTo_LinksToWorkOrder()
    {
        var ticket = RepairTicket.Create("Sink clogged", Actor);
        var woId = WorkOrderId.NewId();

        ticket.ConvertTo(woId, Actor);

        Assert.Equal(woId, ticket.ConvertedToWorkOrderId);
    }

    [Fact]
    public void ConvertTo_AlreadyConverted_Throws()
    {
        var ticket = RepairTicket.Create("Sink clogged", Actor);
        ticket.ConvertTo(WorkOrderId.NewId(), Actor);

        Assert.Throws<InvalidOperationException>(
            () => ticket.ConvertTo(WorkOrderId.NewId(), Actor));
    }
}
