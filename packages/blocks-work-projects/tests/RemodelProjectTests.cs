using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>W#60 P4 — coverage for <see cref="RemodelProject"/> per Stage 02 §2.8.</summary>
public sealed class RemodelProjectTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();

    private static RemodelProject MakeBase(bool permitRequired = false) =>
        RemodelProject.Create(
            Tenant, RemodelProjectId.NewId(), ProjectId.NewId(),
            scopeStatement: "Replace kitchen cabinetry and counters.",
            remodelKind: RemodelKind.Kitchen,
            permitRequired: permitRequired,
            inspectionsRequired: null,
            createdBy: Actor,
            createdAt: Instant.Now);

    [Fact]
    public void Create_ValidInput_NotYetCapitalized()
    {
        var rp = MakeBase();
        Assert.Null(rp.CapitalizedAt);
        Assert.Null(rp.PermitNumber);
        Assert.False(rp.PermitRequired);
    }

    [Fact]
    public void Create_EmptyScope_Throws()
    {
        Assert.Throws<ArgumentException>(() => RemodelProject.Create(
            Tenant, RemodelProjectId.NewId(), ProjectId.NewId(),
            scopeStatement: " ",
            remodelKind: RemodelKind.Bath, permitRequired: false,
            inspectionsRequired: null, createdBy: Actor, createdAt: Instant.Now));
    }

    [Fact]
    public void SetPermit_PermitNotRequired_Throws()
    {
        var rp = MakeBase(permitRequired: false);
        Assert.Throws<InvalidOperationException>(() =>
            rp.SetPermit("ABC-123", new DateOnly(2026, 5, 1), Actor, Instant.Now));
    }

    [Fact]
    public void SetPermit_RequiresNonEmptyPermitNumber()
    {
        var rp = MakeBase(permitRequired: true);
        Assert.Throws<ArgumentException>(() =>
            rp.SetPermit("  ", new DateOnly(2026, 5, 1), Actor, Instant.Now));
    }

    [Fact]
    public void Capitalize_EmptyAccount_Throws()
    {
        var rp = MakeBase();
        Assert.Throws<ArgumentException>(() => rp.Capitalize(
            Guid.Empty, new DateOnly(2026, 5, 16), 50_000m, "USD", Actor, Instant.Now));
    }

    [Fact]
    public void Capitalize_NonPositiveAmount_Throws()
    {
        var rp = MakeBase();
        Assert.Throws<ArgumentException>(() => rp.Capitalize(
            Guid.NewGuid(), new DateOnly(2026, 5, 16), 0m, "USD", Actor, Instant.Now));
    }

    [Fact]
    public void Capitalize_AmountExceedsCeiling_Throws()
    {
        var rp = MakeBase();
        Assert.Throws<ArgumentException>(() => rp.Capitalize(
            Guid.NewGuid(), new DateOnly(2026, 5, 16),
            RemodelProject.MaxCapitalizedAmount + 1m, "USD", Actor, Instant.Now));
    }

    [Fact]
    public void Create_InspectionsExceedsMaxCount_Throws()
    {
        var huge = Enumerable.Range(0, RemodelProject.MaxInspectionsCount + 1).Select(i => $"i{i}").ToList();
        Assert.Throws<ArgumentException>(() => RemodelProject.Create(
            Tenant, RemodelProjectId.NewId(), ProjectId.NewId(),
            scopeStatement: "scope",
            remodelKind: RemodelKind.Bath, permitRequired: false,
            inspectionsRequired: huge, createdBy: Actor, createdAt: Instant.Now));
    }

    [Fact]
    public void SetPermit_PermitNumberTooLong_Throws()
    {
        var rp = MakeBase(permitRequired: true);
        var huge = new string('x', RemodelProject.MaxPermitNumberLength + 1);
        Assert.Throws<ArgumentException>(() =>
            rp.SetPermit(huge, new DateOnly(2026, 5, 1), Actor, Instant.Now));
    }

    [Fact]
    public void Capitalize_SetsCapitalizedAtAndIsOneShot()
    {
        var rp = MakeBase();
        var now = Instant.Now;
        rp.Capitalize(Guid.NewGuid(), new DateOnly(2026, 5, 16), 75_000m, "usd", Actor, now);
        Assert.NotNull(rp.CapitalizedAt);
        Assert.Equal(75_000m, rp.CapitalizedAmount);
        Assert.Equal("USD", rp.CapitalizedCurrency);
        Assert.Throws<InvalidOperationException>(() => rp.Capitalize(
            Guid.NewGuid(), new DateOnly(2026, 6, 1), 80_000m, "USD", Actor, Instant.Now));
    }
}
