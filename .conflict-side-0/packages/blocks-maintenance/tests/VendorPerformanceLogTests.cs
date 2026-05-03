using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// W#18 Phase 3 — append-only vendor-performance event log + work-order
/// projection.
/// </summary>
public sealed class VendorPerformanceLogTests
{
    private static readonly VendorId VendorA = VendorId.NewId();
    private static readonly VendorId VendorB = VendorId.NewId();
    private static readonly ActorId Operator = new("operator");
    private static readonly WorkOrderId Wo = WorkOrderId.NewId();

    private static VendorPerformanceRecord MakeRecord(
        VendorId vendor,
        VendorPerformanceEvent ev,
        DateTimeOffset? at = null,
        Guid? id = null) =>
        new()
        {
            Id = new VendorPerformanceRecordId(id ?? Guid.NewGuid()),
            Vendor = vendor,
            Event = ev,
            OccurredAt = at ?? DateTimeOffset.UtcNow,
            RecordedBy = Operator,
        };

    private static async Task<List<VendorPerformanceRecord>> CollectAsync(IAsyncEnumerable<VendorPerformanceRecord> source)
    {
        var list = new List<VendorPerformanceRecord>();
        await foreach (var r in source) list.Add(r);
        return list;
    }

    [Fact]
    public async Task AppendAsync_RoundTripsViaList()
    {
        var log = new InMemoryVendorPerformanceLog();
        var record = MakeRecord(VendorA, VendorPerformanceEvent.Hired);

        await log.AppendAsync(record, default);

        var listed = await CollectAsync(log.ListByVendorAsync(VendorA, skip: null, take: null, default));
        Assert.Single(listed);
        Assert.Equal(record.Id, listed[0].Id);
    }

    [Fact]
    public async Task AppendAsync_PreservesChronologicalOrder()
    {
        var log = new InMemoryVendorPerformanceLog();
        var t0 = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

        await log.AppendAsync(MakeRecord(VendorA, VendorPerformanceEvent.Hired, t0), default);
        await log.AppendAsync(MakeRecord(VendorA, VendorPerformanceEvent.JobCompleted, t0.AddDays(5)), default);
        await log.AppendAsync(MakeRecord(VendorA, VendorPerformanceEvent.JobNoShow, t0.AddDays(10)), default);

        var listed = await CollectAsync(log.ListByVendorAsync(VendorA, skip: null, take: null, default));

        Assert.Equal(3, listed.Count);
        Assert.Equal(VendorPerformanceEvent.Hired, listed[0].Event);
        Assert.Equal(VendorPerformanceEvent.JobCompleted, listed[1].Event);
        Assert.Equal(VendorPerformanceEvent.JobNoShow, listed[2].Event);
    }

    [Fact]
    public async Task AppendAsync_IdempotentOnDuplicateId()
    {
        var log = new InMemoryVendorPerformanceLog();
        var fixedId = Guid.NewGuid();
        var first = MakeRecord(VendorA, VendorPerformanceEvent.Hired, id: fixedId);
        var dup = MakeRecord(VendorA, VendorPerformanceEvent.JobCompleted, id: fixedId);

        await log.AppendAsync(first, default);
        await log.AppendAsync(dup, default);

        var listed = await CollectAsync(log.ListByVendorAsync(VendorA, skip: null, take: null, default));
        Assert.Single(listed);
        Assert.Equal(VendorPerformanceEvent.Hired, listed[0].Event); // first wins
    }

    [Fact]
    public async Task ListByVendor_IsTenantIsolated()
    {
        var log = new InMemoryVendorPerformanceLog();
        await log.AppendAsync(MakeRecord(VendorA, VendorPerformanceEvent.Hired), default);
        await log.AppendAsync(MakeRecord(VendorB, VendorPerformanceEvent.Hired), default);

        var aOnly = await CollectAsync(log.ListByVendorAsync(VendorA, skip: null, take: null, default));
        var bOnly = await CollectAsync(log.ListByVendorAsync(VendorB, skip: null, take: null, default));

        Assert.Single(aOnly);
        Assert.Equal(VendorA, aOnly[0].Vendor);
        Assert.Single(bOnly);
        Assert.Equal(VendorB, bOnly[0].Vendor);
    }

    [Fact]
    public async Task ListByVendor_UnknownVendor_ReturnsEmpty()
    {
        var log = new InMemoryVendorPerformanceLog();
        var listed = await CollectAsync(log.ListByVendorAsync(VendorId.NewId(), skip: null, take: null, default));
        Assert.Empty(listed);
    }

    [Fact]
    public async Task ListByVendor_PaginatesViaSkipAndTake()
    {
        var log = new InMemoryVendorPerformanceLog();
        var t0 = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await log.AppendAsync(MakeRecord(VendorA, VendorPerformanceEvent.JobCompleted, t0.AddMinutes(i)), default);
        }

        var page1 = await CollectAsync(log.ListByVendorAsync(VendorA, skip: 0, take: 2, default));
        var page2 = await CollectAsync(log.ListByVendorAsync(VendorA, skip: 2, take: 2, default));
        var page3 = await CollectAsync(log.ListByVendorAsync(VendorA, skip: 4, take: 2, default));

        Assert.Equal(2, page1.Count);
        Assert.Equal(2, page2.Count);
        Assert.Single(page3);
    }

    [Fact]
    public async Task ProjectFromWorkOrder_AppendsRecord()
    {
        var log = new InMemoryVendorPerformanceLog();
        var record = await log.ProjectFromWorkOrderAsync(
            VendorA,
            Wo,
            VendorPerformanceEvent.JobCompleted,
            Operator,
            DateTimeOffset.UtcNow,
            notes: "Cluster intake demo flow",
            default);

        Assert.Equal(VendorA, record.Vendor);
        Assert.Equal(Wo, record.RelatedWorkOrder);
        Assert.Equal(VendorPerformanceEvent.JobCompleted, record.Event);

        var listed = await CollectAsync(log.ListByVendorAsync(VendorA, skip: null, take: null, default));
        Assert.Contains(listed, r => r.Id == record.Id);
    }

    [Fact]
    public async Task ProjectFromWorkOrder_PreservesNullNotes()
    {
        var log = new InMemoryVendorPerformanceLog();
        var record = await log.ProjectFromWorkOrderAsync(
            VendorA, Wo, VendorPerformanceEvent.JobNoShow, Operator, DateTimeOffset.UtcNow, notes: null, default);
        Assert.Null(record.Notes);
    }

    [Fact]
    public async Task AppendAsync_RejectsNullRecord()
    {
        var log = new InMemoryVendorPerformanceLog();
        await Assert.ThrowsAsync<ArgumentNullException>(() => log.AppendAsync(null!, default));
    }

    [Fact]
    public void VendorPerformanceEvent_EnumCovers9Categories()
    {
        var values = Enum.GetValues<VendorPerformanceEvent>();
        Assert.Equal(9, values.Length);
        Assert.Contains(VendorPerformanceEvent.Hired, values);
        Assert.Contains(VendorPerformanceEvent.JobCompleted, values);
        Assert.Contains(VendorPerformanceEvent.JobNoShow, values);
        Assert.Contains(VendorPerformanceEvent.JobLate, values);
        Assert.Contains(VendorPerformanceEvent.JobCancelled, values);
        Assert.Contains(VendorPerformanceEvent.RatingAdjusted, values);
        Assert.Contains(VendorPerformanceEvent.InsuranceLapse, values);
        Assert.Contains(VendorPerformanceEvent.Suspended, values);
        Assert.Contains(VendorPerformanceEvent.Retired, values);
    }
}
