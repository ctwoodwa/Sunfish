using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialPeriods.Financial;
using Sunfish.Blocks.FinancialPeriods.Models;
using Sunfish.Blocks.FinancialPeriods.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialPeriods.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="PeriodCloseService.SoftCloseAsync"/>
/// per Stage 02 §6.5(a) soft-close transition rules.
/// </summary>
public sealed class PeriodCloseServiceSoftCloseTests
{
    private static readonly ChartOfAccountsId Chart = ChartOfAccountsId.NewId();

    [Fact]
    public async Task SoftClose_OpenPeriod_TransitionsToSoftClosed()
    {
        var h = new Harness();
        var (fy, period) = await h.SeedYearAndPeriodAsync();

        var result = await h.Sut.SoftCloseAsync(period.Id);

        Assert.True(result.IsSuccess, result.Detail);
        Assert.NotNull(result.Period);
        Assert.Equal(FiscalPeriodStatus.SoftClosed, result.Period!.Status);
    }

    [Fact]
    public async Task SoftClose_PopulatesSoftClosedAtUtc()
    {
        var fixedInstant = new Instant(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var h = new Harness(time: new FixedTimeProvider(fixedInstant.Value));
        var (_, period) = await h.SeedYearAndPeriodAsync();

        var result = await h.Sut.SoftCloseAsync(period.Id);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Period!.SoftClosedAtUtc);
        Assert.Equal(fixedInstant.Value, result.Period.SoftClosedAtUtc!.Value.Value);
    }

    [Fact]
    public async Task SoftClose_AlreadySoftClosed_ReturnsAlreadySoftClosedError()
    {
        var h = new Harness();
        var (_, period) = await h.SeedYearAndPeriodAsync(periodStatus: FiscalPeriodStatus.SoftClosed);

        var result = await h.Sut.SoftCloseAsync(period.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodAlreadySoftClosed, result.Error);
        Assert.NotNull(result.Period);
    }

    [Fact]
    public async Task SoftClose_LockedPeriod_ReturnsPeriodLockedError()
    {
        var h = new Harness();
        var (_, period) = await h.SeedYearAndPeriodAsync(periodStatus: FiscalPeriodStatus.Locked);

        var result = await h.Sut.SoftCloseAsync(period.Id);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodLocked, result.Error);
    }

    [Fact]
    public async Task SoftClose_UnknownPeriod_ReturnsPeriodNotFound()
    {
        var h = new Harness();
        var unknown = FiscalPeriodId.NewId();

        var result = await h.Sut.SoftCloseAsync(unknown);

        Assert.False(result.IsSuccess);
        Assert.Equal(PeriodCloseError.PeriodNotFound, result.Error);
        Assert.Equal(unknown.Value, result.Detail);
    }

    [Fact]
    public async Task SoftClose_EmitsPeriodSoftClosedEvent()
    {
        var h = new Harness();
        var (_, period) = await h.SeedYearAndPeriodAsync();

        await h.Sut.SoftCloseAsync(period.Id);

        var evt = Assert.Single(h.Events.Published.OfType<PeriodSoftClosed>().ToList());
        Assert.Equal(period.Id, evt.PeriodId);
        Assert.Equal(period.ChartId, evt.ChartId);
        Assert.Null(evt.ClosedByPrincipalId);
    }

    [Fact]
    public async Task SoftClose_PropagatesClosedByPrincipalId_IntoEvent()
    {
        var h = new Harness();
        var (_, period) = await h.SeedYearAndPeriodAsync();

        await h.Sut.SoftCloseAsync(period.Id, closedByPrincipalId: "principal-admin-1");

        var evt = Assert.Single(h.Events.Published.OfType<PeriodSoftClosed>().ToList());
        Assert.Equal("principal-admin-1", evt.ClosedByPrincipalId);
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public InMemoryFiscalYearRepository Years { get; } = new();
        public CapturingEventPublisher Events { get; } = new();
        public TimeProvider Time { get; }
        public PeriodCloseService Sut { get; }

        public Harness(TimeProvider? time = null)
        {
            Time = time ?? TimeProvider.System;
            Sut  = new PeriodCloseService(Periods, Years, Events, Time);
        }

        public async Task<(FiscalYear Year, FiscalPeriod Period)> SeedYearAndPeriodAsync(
            FiscalPeriodStatus periodStatus = FiscalPeriodStatus.Open)
        {
            var fy = FiscalYear.CreateOpen(
                FiscalYearId.NewId(), Chart, "2026",
                new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
            await Years.InsertAsync(fy);

            var basePeriod = FiscalPeriod.CreateOpen(
                FiscalPeriodId.NewId(), fy.ChartId, fy.Id,
                FiscalPeriodKind.Monthly, "2026-M06",
                new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30));

            var period = periodStatus switch
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

            await Periods.InsertAsync(period);
            return (fy, period);
        }
    }

    internal sealed class CapturingEventPublisher : IDomainEventPublisher
    {
        /// <summary>Captured envelopes in publish order.</summary>
        public List<object> Envelopes { get; } = new();

        /// <summary>
        /// Convenience accessor that flattens envelopes back to their
        /// payloads in publish order — most assertions only care about
        /// the payload shape (e.g., <c>.OfType&lt;PeriodSoftClosed&gt;()</c>).
        /// </summary>
        /// <remarks>
        /// <b>Temporary reflection shim:</b> kept so PR 3b doesn't have
        /// to rewrite the 7+ existing tests that use
        /// <c>.Published.OfType&lt;TPayload&gt;()</c>. Remove this
        /// accessor + migrate callers to
        /// <c>.Envelopes.Cast&lt;DomainEventEnvelope&lt;TPayload&gt;&gt;().Select(e =&gt; e.Payload)</c>
        /// once <c>foundation-events</c> lands a typed envelope-
        /// accessor pattern across the cluster sweep PRs. Otherwise
        /// the reflection pattern will copy-paste into tax / AR / AP /
        /// people / work clusters as they widen.
        /// </remarks>
        public List<object> Published =>
            Envelopes
                .Select(e => e.GetType().GetProperty(nameof(DomainEventEnvelope<object>.Payload))!.GetValue(e)!)
                .ToList();

        public Task PublishAsync<TPayload>(
            DomainEventEnvelope<TPayload> envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            Envelopes.Add(envelope);
            return Task.CompletedTask;
        }
    }

    internal sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
