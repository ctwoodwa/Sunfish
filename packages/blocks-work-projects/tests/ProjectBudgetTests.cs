using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="ProjectBudget"/> +
/// <see cref="ProjectBudgetLine"/> entity invariants.
/// </summary>
public sealed class ProjectBudgetTests
{
    private static readonly TenantId Tenant = new("budget-entity-test");
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly ProjectId Pid = ProjectId.NewId();

    [Fact]
    public void Create_RevisionNumberZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectBudget.Create(
            Tenant, ProjectBudgetId.NewId(), Pid,
            revisionNumber: 0,
            effectiveFrom: new DateOnly(2026, 1, 1),
            createdBy: Actor,
            createdAt: Instant.Now));
    }

    [Fact]
    public void CreateLine_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectBudgetLine.Create(
            Tenant, ProjectBudgetLineId.NewId(), ProjectBudgetId.NewId(),
            BudgetCategory.Labor, -100m, "USD", Actor, Instant.Now));
    }

    [Fact]
    public void CreateLine_NormalizesCurrencyToUpperInvariant()
    {
        var line = ProjectBudgetLine.Create(
            Tenant, ProjectBudgetLineId.NewId(), ProjectBudgetId.NewId(),
            BudgetCategory.Labor, 100m, "usd", Actor, Instant.Now);
        Assert.Equal("USD", line.Currency);
    }
}
