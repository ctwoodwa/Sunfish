using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="RemodelPhase"/>.</summary>
public sealed class RemodelPhaseTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly RemodelProjectId Rpid = RemodelProjectId.NewId();

    private static RemodelPhase MakePhase(int ordinal = 1, decimal budget = 10_000m) =>
        RemodelPhase.Create(
            Tenant, RemodelPhaseId.NewId(), Rpid, ordinal,
            name: $"phase-{ordinal}",
            budgetedAmount: budget, budgetedCurrency: "USD",
            plannedStartDate: new DateOnly(2026, 5, 1),
            plannedEndDate: new DateOnly(2026, 5, 30),
            createdBy: Actor, createdAt: Instant.Now);

    [Fact]
    public void Create_StatusIsPlanned()
    {
        Assert.Equal(PhaseStatus.Planned, MakePhase().Status);
    }

    [Fact]
    public void Create_OrdinalLessThanOne_Throws()
    {
        Assert.Throws<ArgumentException>(() => MakePhase(ordinal: 0));
    }

    [Fact]
    public void Start_TransitionsToActive()
    {
        var p = MakePhase();
        p.Start(new DateOnly(2026, 5, 5), Actor, Instant.Now);
        Assert.Equal(PhaseStatus.Active, p.Status);
        Assert.Equal(new DateOnly(2026, 5, 5), p.ActualStartDate);
    }

    [Fact]
    public void Complete_TransitionsAndSetsActualEndDate()
    {
        var p = MakePhase();
        p.Start(new DateOnly(2026, 5, 5), Actor, Instant.Now);
        p.Complete(new DateOnly(2026, 5, 28), actualAmount: 9_500m, Actor, Instant.Now);
        Assert.Equal(PhaseStatus.Complete, p.Status);
        Assert.Equal(new DateOnly(2026, 5, 28), p.ActualEndDate);
        Assert.Equal(9_500m, p.ActualAmount);
    }

    [Fact]
    public void Complete_EndBeforeStart_Throws()
    {
        var p = MakePhase();
        p.Start(new DateOnly(2026, 5, 10), Actor, Instant.Now);
        Assert.Throws<ArgumentException>(() =>
            p.Complete(new DateOnly(2026, 5, 1), 1_000m, Actor, Instant.Now));
    }

    [Fact]
    public void MarkOverBudget_RequiresAmountAboveBudget()
    {
        var p = MakePhase(budget: 10_000m);
        Assert.Throws<ArgumentException>(() =>
            p.MarkOverBudget(actualAmount: 9_000m, Actor, Instant.Now));
        p.MarkOverBudget(actualAmount: 12_000m, Actor, Instant.Now);
        Assert.Equal(PhaseStatus.OverBudget, p.Status);
    }

    [Fact]
    public void Cancel_TerminalState_Throws()
    {
        var p = MakePhase();
        p.Start(new DateOnly(2026, 5, 5), Actor, Instant.Now);
        p.Complete(new DateOnly(2026, 5, 28), null, Actor, Instant.Now);
        Assert.Throws<InvalidOperationException>(() => p.Cancel(Actor, Instant.Now));
    }
}
