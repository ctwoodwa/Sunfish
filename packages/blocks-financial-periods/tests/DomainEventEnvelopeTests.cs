using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using static Sunfish.Blocks.FinancialPeriods.Tests.PeriodCloseServiceSoftCloseTests;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 PR 3b — coverage for
/// <see cref="DomainEventEnvelope{TPayload}"/> wrapping inside
/// <see cref="PeriodCloseService"/> per
/// <c>xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md</c>.
/// </summary>
public sealed class DomainEventEnvelopeTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task SoftClose_PopulatesEnvelopeWithCanonicalEventType()
    {
        var (sut, periods, _, events) = NewHarness();
        var (_, period) = await SeedAsync(periods);

        await sut.SoftCloseAsync(period.Id);

        var envelope = Assert.Single(events.Envelopes);
        var eventType = (string)envelope.GetType().GetProperty("EventType")!.GetValue(envelope)!;
        Assert.Equal("Financial.PeriodSoftClosed", eventType);
    }

    [Fact]
    public async Task SoftClose_PopulatesIdempotencyKey_DeterministicPerStateTransition()
    {
        var (sut, periods, _, events) = NewHarness(tenantId: new TenantId("tenant-x"));
        var (_, period) = await SeedAsync(periods);

        await sut.SoftCloseAsync(period.Id);

        var envelope = Assert.Single(events.Envelopes);
        var idemKey = (string)envelope.GetType().GetProperty("IdempotencyKey")!.GetValue(envelope)!;
        Assert.Equal(
            $"Financial.PeriodSoftClosed|tenant-x|{period.Id.Value}|SoftClosed",
            idemKey);
    }

    [Fact]
    public async Task Envelope_PopulatesTenantIdAndReplicaIdFromCtor()
    {
        var tenant  = new TenantId("acero-properties");
        var replica = new ReplicaId("mac-mini-01");
        var (sut, periods, _, events) = NewHarness(tenantId: tenant, replicaId: replica);
        var (_, period) = await SeedAsync(periods);

        await sut.SoftCloseAsync(period.Id);

        var envelope = Assert.Single(events.Envelopes);
        Assert.Equal(tenant,  (TenantId)envelope.GetType().GetProperty("TenantId")!.GetValue(envelope)!);
        Assert.Equal(replica, (ReplicaId)envelope.GetType().GetProperty("OriginatingReplicaId")!.GetValue(envelope)!);
    }

    [Fact]
    public async Task Envelope_DefaultsTenantAndReplicaToSystemSentinels()
    {
        var (sut, periods, _, events) = NewHarness();
        var (_, period) = await SeedAsync(periods);

        await sut.SoftCloseAsync(period.Id);

        var envelope = Assert.Single(events.Envelopes);
        var tenant  = (TenantId)envelope.GetType().GetProperty("TenantId")!.GetValue(envelope)!;
        var replica = (ReplicaId)envelope.GetType().GetProperty("OriginatingReplicaId")!.GetValue(envelope)!;
        Assert.True(tenant.IsSystemSentinel);
        Assert.True(replica.IsSystemSentinel);
    }

    [Fact]
    public async Task Envelope_PopulatesSchemaVersion_1()
    {
        var (sut, periods, _, events) = NewHarness();
        var (_, period) = await SeedAsync(periods);

        await sut.SoftCloseAsync(period.Id);

        var envelope = Assert.Single(events.Envelopes);
        var schemaVersion = (int)envelope.GetType().GetProperty("SchemaVersion")!.GetValue(envelope)!;
        Assert.Equal(1, schemaVersion);
    }

    [Fact]
    public async Task Envelope_EventIdIsNonEmptyAndUnique_AcrossEmissions()
    {
        var (sut, periods, _, events) = NewHarness();
        var (_, p1) = await SeedAsync(periods);
        var (_, p2) = await SeedAsync(periods);

        await sut.SoftCloseAsync(p1.Id);
        await sut.SoftCloseAsync(p2.Id);

        Assert.Equal(2, events.Envelopes.Count);
        var ids = events.Envelopes
            .Select(e => (string)e.GetType().GetProperty("EventId")!.GetValue(e)!)
            .ToList();
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        Assert.Equal(2, ids.Distinct().Count());
    }

    [Fact]
    public async Task Lock_AutoSoftClose_EnvelopesHaveDistinctEventTypes()
    {
        var (sut, periods, _, events) = NewHarness();
        var (_, period) = await SeedAsync(periods, status: FiscalPeriodStatus.Open);

        await sut.LockAsync(period.Id);

        Assert.Equal(2, events.Envelopes.Count);
        var types = events.Envelopes
            .Select(e => (string)e.GetType().GetProperty("EventType")!.GetValue(e)!)
            .ToList();
        Assert.Equal(new[] { "Financial.PeriodSoftClosed", "Financial.PeriodLocked" }, types);
    }

    [Fact]
    public async Task Unlock_EnvelopeUsesPeriodOpenedEventType_WithSoftClosedStatusInIdempotencyKey()
    {
        var (sut, periods, _, events) = NewHarness(tenantId: new TenantId("t1"));
        var (_, period) = await SeedAsync(periods, status: FiscalPeriodStatus.Locked);

        await sut.UnlockAsync(period.Id, auditMemo: "audit");

        var envelope = Assert.Single(events.Envelopes);
        var eventType = (string)envelope.GetType().GetProperty("EventType")!.GetValue(envelope)!;
        var idemKey   = (string)envelope.GetType().GetProperty("IdempotencyKey")!.GetValue(envelope)!;
        Assert.Equal("Financial.PeriodOpened", eventType);
        // Unlock targets SoftClosed (not Open) — the idempotency key
        // carries the destination status so unlock and reopen emit
        // distinct keys for the same period.
        Assert.EndsWith("|SoftClosed", idemKey);
    }

    [Fact]
    public async Task NoopPublisher_AcceptsEnvelope_WithoutThrowing()
    {
        var noop = new NoopDomainEventPublisher();
        var envelope = new DomainEventEnvelope<PeriodSoftClosed>
        {
            EventId              = Guid.NewGuid().ToString(),
            EventType            = "Financial.PeriodSoftClosed",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = TenantId.System,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = "test|key|0|SoftClosed",
            Payload              = new PeriodSoftClosed(
                FiscalPeriodId.NewId(), Chart, null),
        };

        await noop.PublishAsync(envelope);
        // Reaching this line is the assertion — Noop must not throw.
    }

    // ----- helpers ---------------------------------------------------

    private static (PeriodCloseService Sut,
        InMemoryFiscalPeriodRepository Periods,
        InMemoryFiscalYearRepository Years,
        CapturingEventPublisher Events) NewHarness(
            TenantId? tenantId = null,
            ReplicaId? replicaId = null)
    {
        var periods = new InMemoryFiscalPeriodRepository();
        var years   = new InMemoryFiscalYearRepository();
        var events  = new CapturingEventPublisher();
        var sut     = new PeriodCloseService(
            periods, years, events, TimeProvider.System,
            tenantId: tenantId, replicaId: replicaId);
        return (sut, periods, years, events);
    }

    private static async Task<(FiscalYear Year, FiscalPeriod Period)> SeedAsync(
        InMemoryFiscalPeriodRepository periods,
        FiscalPeriodStatus status = FiscalPeriodStatus.Open)
    {
        var fy = FiscalYear.CreateOpen(
            FiscalYearId.NewId(), Chart, "2026",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        var basePeriod = FiscalPeriod.CreateOpen(
            FiscalPeriodId.NewId(), fy.ChartId, fy.Id,
            FiscalPeriodKind.Monthly, "2026-M06",
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

        var period = status switch
        {
            FiscalPeriodStatus.SoftClosed => basePeriod with
            {
                Status          = FiscalPeriodStatus.SoftClosed,
                SoftClosedAtUtc = Instant.Now,
            },
            FiscalPeriodStatus.Locked => basePeriod with
            {
                Status          = FiscalPeriodStatus.Locked,
                SoftClosedAtUtc = Instant.Now,
                LockedAtUtc     = Instant.Now,
            },
            _ => basePeriod,
        };
        await periods.InsertAsync(period);
        return (fy, period);
    }
}
