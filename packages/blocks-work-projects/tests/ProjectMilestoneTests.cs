using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="ProjectMilestone"/> per Stage
/// 02 §2.3.
/// </summary>
public sealed class ProjectMilestoneTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly ProjectId Pid = ProjectId.NewId();

    [Fact]
    public void Create_KindPayment_WithoutAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Final payment",
            MilestoneKind.Payment, new DateOnly(2026, 6, 1), Actor, Instant.Now));
    }

    [Fact]
    public void Create_KindPayment_WithoutCurrency_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Final payment",
            MilestoneKind.Payment, new DateOnly(2026, 6, 1), Actor, Instant.Now,
            paymentAmount: 5000m));
    }

    [Fact]
    public void Create_TriggersInvoice_WithoutCustomer_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Final payment",
            MilestoneKind.Payment, new DateOnly(2026, 6, 1), Actor, Instant.Now,
            paymentAmount: 5000m, paymentCurrency: "USD",
            triggersInvoice: true));
    }

    [Fact]
    public void Create_TriggersInvoice_WithCustomer_Succeeds()
    {
        var m = ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Final payment",
            MilestoneKind.Payment, new DateOnly(2026, 6, 1), Actor, Instant.Now,
            paymentAmount: 5000m, paymentCurrency: "USD",
            triggersInvoice: true, customerPartyId: Customer);
        Assert.True(m.TriggersInvoice);
    }

    [Fact]
    public void Create_WeightOutOfBounds_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Test",
            MilestoneKind.Schedule, new DateOnly(2026, 6, 1), Actor, Instant.Now,
            weight: 1.5m));
    }

    [Fact]
    public void Achieve_SetsActualDateAndStatus()
    {
        var m = NewSchedule();
        m.Achieve(new DateOnly(2026, 5, 28), Actor, Instant.Now);
        Assert.Equal(MilestoneStatus.Achieved, m.Status);
        Assert.Equal(new DateOnly(2026, 5, 28), m.ActualDate);
    }

    [Fact]
    public void MarkAtRisk_FromPending_Succeeds()
    {
        var m = NewSchedule();
        m.MarkAtRisk(Actor, Instant.Now);
        Assert.Equal(MilestoneStatus.AtRisk, m.Status);
    }

    [Fact]
    public void Cancel_SetsStatus()
    {
        var m = NewSchedule();
        m.Cancel(Actor, Instant.Now);
        Assert.Equal(MilestoneStatus.Cancelled, m.Status);
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "  ", "Name",
            MilestoneKind.Schedule, new DateOnly(2026, 6, 1), Actor, Instant.Now));
    }

    private static ProjectMilestone NewSchedule()
        => ProjectMilestone.Create(
            Tenant, MilestoneId.NewId(), Pid, "M1", "Schedule milestone",
            MilestoneKind.Schedule, new DateOnly(2026, 6, 1), Actor, Instant.Now);
}
