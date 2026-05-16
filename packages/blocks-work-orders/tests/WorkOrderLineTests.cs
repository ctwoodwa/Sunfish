using Sunfish.Blocks.WorkOrders.Models;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkOrderLine"/>.
/// </summary>
public sealed class WorkOrderLineTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly WorkOrderId Wo = WorkOrderId.NewId();

    [Fact]
    public void Create_EstimatedAmount_ComputedFromQtyAndPrice()
    {
        var line = WorkOrderLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkOrderLineKind.Labor,
            description: "Plumber 2 hours",
            createdBy: Actor,
            quantity: 2m,
            unitPrice: 95m,
            unitOfMeasure: "hr",
            currency: "USD");

        Assert.Equal(190m, line.EstimatedAmount);
    }

    [Fact]
    public void Create_ExplicitEstimate_OverridesDerivation()
    {
        var line = WorkOrderLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkOrderLineKind.Material,
            description: "Faucet kit",
            createdBy: Actor,
            quantity: 1m,
            unitPrice: 45m,
            estimatedAmount: 60m);

        Assert.Equal(60m, line.EstimatedAmount);
    }

    [Fact]
    public void Create_EmptyDescription_Throws()
    {
        Assert.Throws<ArgumentException>(() => WorkOrderLine.Create(
            workOrderId: Wo,
            lineNumber: 1,
            kind: WorkOrderLineKind.Labor,
            description: "  ",
            createdBy: Actor));
    }

    [Fact]
    public void SetActual_RecordsActualAmount()
    {
        var line = WorkOrderLine.Create(
            Wo, 1, WorkOrderLineKind.Material, "Pipe", Actor,
            quantity: 1m, unitPrice: 20m);

        line.SetActual(25m, Actor);

        Assert.Equal(25m, line.ActualAmount);
    }
}
