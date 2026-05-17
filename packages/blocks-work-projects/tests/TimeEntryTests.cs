using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="TimeEntry"/> per Stage 02 §2.20.
/// </summary>
public sealed class TimeEntryTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid Approver = Guid.NewGuid();

    private static TimeEntry MakeOpen(ProjectId? pid = null, Guid? woid = null, Guid? mtid = null, bool billable = true)
        => TimeEntry.Open(
            Tenant, TimeEntryId.NewId(), Worker, ActivityKind.Labor,
            new Instant(new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero)),
            Worker, Instant.Now,
            projectId: pid ?? (woid is null && mtid is null ? ProjectId.NewId() : null),
            workOrderId: woid, maintenanceTaskId: mtid, billable: billable);

    [Fact]
    public void Open_RequiresExactlyOneTarget_Zero()
    {
        Assert.Throws<ArgumentException>(() => TimeEntry.Open(
            Tenant, TimeEntryId.NewId(), Worker, ActivityKind.Labor,
            Instant.Now, Worker, Instant.Now));
    }

    [Fact]
    public void Open_RequiresExactlyOneTarget_Two()
    {
        Assert.Throws<ArgumentException>(() => TimeEntry.Open(
            Tenant, TimeEntryId.NewId(), Worker, ActivityKind.Labor,
            Instant.Now, Worker, Instant.Now,
            projectId: ProjectId.NewId(),
            workOrderId: Guid.NewGuid()));
    }

    [Fact]
    public void Open_StartsInOpenStatus_NoEndedAt()
    {
        var e = MakeOpen();
        Assert.Equal(TimeEntryStatus.Open, e.Status);
        Assert.Null(e.EndedAt);
        Assert.Equal(0, e.DurationMinutes);
    }

    [Fact]
    public void Stop_EndedAtBeforeStartedAt_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<ArgumentException>(() =>
            e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 8, 0, 0, TimeSpan.Zero)),
                hourlyRate: null, rateCurrency: null, updatedBy: Worker));
    }

    [Fact]
    public void Stop_ComputesDurationAndAmount()
    {
        var e = MakeOpen();
        e.Stop(
            new Instant(new DateTimeOffset(2026, 5, 16, 11, 30, 0, TimeSpan.Zero)),
            hourlyRate: 80m, rateCurrency: "usd", updatedBy: Worker);
        Assert.Equal(150, e.DurationMinutes);
        Assert.Equal(200m, e.Amount);
        Assert.Equal("USD", e.HourlyRateCurrency);
    }

    [Fact]
    public void Stop_RateWithoutCurrency_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<ArgumentException>(() =>
            e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
                hourlyRate: 50m, rateCurrency: null, updatedBy: Worker));
    }

    [Fact]
    public void Submit_RunningEntry_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<InvalidOperationException>(() =>
            e.Submit(Instant.Now, Worker));
    }

    [Fact]
    public void Submit_StoppedEntry_TransitionsToSubmitted()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        Assert.Equal(TimeEntryStatus.Submitted, e.Status);
        Assert.NotNull(e.SubmittedAt);
    }

    [Fact]
    public void Approve_OpenEntry_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<InvalidOperationException>(() => e.Approve(Approver, Instant.Now));
    }

    [Fact]
    public void Approve_SubmittedEntry_TransitionsToApproved()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        e.Approve(Approver, Instant.Now);
        Assert.Equal(TimeEntryStatus.Approved, e.Status);
        Assert.Equal(Approver, e.ApprovedByPartyId);
    }

    [Fact]
    public void Reject_RequiresReason()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        Assert.Throws<ArgumentException>(() => e.Reject("  ", Approver, Instant.Now));
    }

    [Fact]
    public void Reject_StoresRejecterOnRejectedByPartyId_NotApprovedByPartyId()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        e.Reject("missing detail", Approver, Instant.Now);
        Assert.Equal(TimeEntryStatus.Rejected, e.Status);
        Assert.Equal(Approver, e.RejectedByPartyId);
        Assert.NotNull(e.RejectedAt);
        Assert.Null(e.ApprovedByPartyId);
        Assert.Null(e.ApprovedAt);
        Assert.Equal("missing detail", e.RejectionReason);
    }

    [Fact]
    public void Reject_ReasonExceedsMax_Throws()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        var huge = new string('x', TimeEntry.MaxFreeTextLength + 1);
        Assert.Throws<ArgumentException>(() => e.Reject(huge, Approver, Instant.Now));
    }

    [Fact]
    public void MarkInvoiced_ApprovedEntry_TransitionsAndSetsFlag()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        e.Approve(Approver, Instant.Now);
        e.MarkInvoiced(Worker, Instant.Now);
        Assert.Equal(TimeEntryStatus.Invoiced, e.Status);
        Assert.True(e.InvoicedFlag);
    }

    [Fact]
    public void MarkInvoiced_TwiceFromReactorRetry_IsIdempotent()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        e.Approve(Approver, Instant.Now);
        e.MarkInvoiced(Worker, Instant.Now);
        var versionBefore = e.Version;
        // At-least-once reactor delivery: second call must be a no-op
        // (not throw), preserving the state machine without poison-pill.
        e.MarkInvoiced(Worker, Instant.Now);
        Assert.Equal(TimeEntryStatus.Invoiced, e.Status);
        Assert.Equal(versionBefore, e.Version);
    }

    [Fact]
    public void Stop_RoundsAmountAwayFromZero()
    {
        var e = MakeOpen();
        // 0.005 boundary: AwayFromZero rounds 0.005 -> 0.01, banker's -> 0.00.
        // rate 0.30/hr * 1 minute = 0.005 -> expected 0.01.
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 9, 1, 0, TimeSpan.Zero)),
            hourlyRate: 0.30m, rateCurrency: "USD", updatedBy: Worker);
        Assert.Equal(0.01m, e.Amount);
    }

    [Fact]
    public void Stop_InvalidCurrencyLength_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<ArgumentException>(() =>
            e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
                hourlyRate: 80m, rateCurrency: "DOLLARS", updatedBy: Worker));
    }

    [Fact]
    public void Stop_NonAsciiCurrency_Throws()
    {
        var e = MakeOpen();
        Assert.Throws<ArgumentException>(() =>
            e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
                hourlyRate: 80m, rateCurrency: "USD1", updatedBy: Worker));
    }

    [Fact]
    public void UpdateDescription_ExceedsMaxLength_Throws()
    {
        var e = MakeOpen();
        var huge = new string('x', TimeEntry.MaxFreeTextLength + 1);
        Assert.Throws<ArgumentException>(() => e.UpdateDescription(huge, Worker, Instant.Now));
    }

    [Fact]
    public void UpdateDescription_AfterApproved_Throws()
    {
        var e = MakeOpen();
        e.Stop(new Instant(new DateTimeOffset(2026, 5, 16, 10, 0, 0, TimeSpan.Zero)),
            null, null, Worker);
        e.Submit(Instant.Now, Worker);
        e.Approve(Approver, Instant.Now);
        Assert.Throws<InvalidOperationException>(() =>
            e.UpdateDescription("note", Worker, Instant.Now));
    }
}
