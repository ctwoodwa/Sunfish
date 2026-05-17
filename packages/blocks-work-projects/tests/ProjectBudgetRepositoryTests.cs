using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Blocks.WorkProjects.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="InMemoryProjectBudgetRepository"/>
/// per Stage 02 §2.4 revision discipline.
/// </summary>
public sealed class ProjectBudgetRepositoryTests
{
    private static readonly TenantId Tenant = new("budget-test");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public async Task InsertRevision_FirstRevision_RevisionNumberIs1()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        var rev = await sut.InsertRevisionAsync(
            Tenant, projectId,
            effectiveFrom: new DateOnly(2026, 1, 1),
            lines: new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD") },
            createdBy: Actor,
            createdAt: Instant.Now);

        Assert.Equal(1, rev.RevisionNumber);
        Assert.Null(rev.SupersededAt);
    }

    [Fact]
    public async Task InsertRevision_SecondRevision_RevisionNumberIs2_AndPriorIsSuperseded()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        var r1 = await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 1, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD") },
            Actor, Instant.Now);

        var r2 = await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 4, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 12_000m, "USD") },
            Actor, Instant.Now);

        Assert.Equal(2, r2.RevisionNumber);
        var r1Reloaded = await sut.GetAsync(r1.Id);
        Assert.NotNull(r1Reloaded);
        Assert.NotNull(r1Reloaded!.SupersededAt);
        Assert.Equal(new DateOnly(2026, 3, 31), r1Reloaded.EffectiveUntil);
    }

    [Fact]
    public async Task InsertRevision_SameEffectiveFromAsPrior_Throws_OverlappingBudgetRevision()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();
        await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 1, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD") },
            Actor, Instant.Now);

        await Assert.ThrowsAsync<OverlappingBudgetRevisionException>(() =>
            sut.InsertRevisionAsync(
                Tenant, projectId, new DateOnly(2026, 1, 1),
                new[] { new ProjectBudgetLineDraft(BudgetCategory.Materials, 5_000m, "USD") },
                Actor, Instant.Now));
    }

    [Fact]
    public async Task InsertRevision_DuplicateCategory_Throws()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.InsertRevisionAsync(
                Tenant, projectId, new DateOnly(2026, 1, 1),
                new[]
                {
                    new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD"),
                    new ProjectBudgetLineDraft(BudgetCategory.Labor, 5_000m, "USD"),
                },
                Actor, Instant.Now));
    }

    [Fact]
    public async Task InsertRevision_EmptyLines_Throws()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.InsertRevisionAsync(
                Tenant, projectId, new DateOnly(2026, 1, 1),
                Array.Empty<ProjectBudgetLineDraft>(),
                Actor, Instant.Now));
    }

    [Fact]
    public async Task InsertRevision_NegativeAmount_Throws()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            sut.InsertRevisionAsync(
                Tenant, projectId, new DateOnly(2026, 1, 1),
                new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, -100m, "USD") },
                Actor, Instant.Now));
    }

    [Fact]
    public async Task InsertRevision_InvalidCurrency_Throws()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.InsertRevisionAsync(
                Tenant, projectId, new DateOnly(2026, 1, 1),
                new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 100m, "Dollars") },
                Actor, Instant.Now));
    }

    [Fact]
    public async Task GetCurrent_ReturnsLatestNonSuperseded()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();
        await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 1, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD") },
            Actor, Instant.Now);
        var r2 = await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 4, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 12_000m, "USD") },
            Actor, Instant.Now);

        var current = await sut.GetCurrentAsync(projectId);

        Assert.NotNull(current);
        Assert.Equal(r2.Id, current!.Id);
    }

    [Fact]
    public async Task GetRevisions_ReturnsAllRevisionsInChronologicalOrder()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();
        await sut.InsertRevisionAsync(Tenant, projectId, new DateOnly(2026, 1, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD") }, Actor, Instant.Now);
        await sut.InsertRevisionAsync(Tenant, projectId, new DateOnly(2026, 4, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 12_000m, "USD") }, Actor, Instant.Now);
        await sut.InsertRevisionAsync(Tenant, projectId, new DateOnly(2026, 7, 1),
            new[] { new ProjectBudgetLineDraft(BudgetCategory.Labor, 15_000m, "USD") }, Actor, Instant.Now);

        var revs = await sut.GetRevisionsAsync(projectId);

        Assert.Equal(3, revs.Count);
        Assert.Equal(new[] { 1, 2, 3 }, revs.Select(r => r.RevisionNumber));
    }

    [Fact]
    public async Task GetLines_ReturnsAllLinesForRevision()
    {
        var sut = new InMemoryProjectBudgetRepository();
        var projectId = ProjectId.NewId();
        var rev = await sut.InsertRevisionAsync(
            Tenant, projectId, new DateOnly(2026, 1, 1),
            new[]
            {
                new ProjectBudgetLineDraft(BudgetCategory.Labor, 10_000m, "USD"),
                new ProjectBudgetLineDraft(BudgetCategory.Materials, 5_000m, "USD"),
                new ProjectBudgetLineDraft(BudgetCategory.Equipment, 2_000m, "USD"),
            },
            Actor, Instant.Now);

        var lines = await sut.GetLinesAsync(rev.Id);

        Assert.Equal(3, lines.Count);
    }
}
