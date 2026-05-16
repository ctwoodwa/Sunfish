using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialLedger.Services;

/// <summary>
/// Resolves the fiscal period covering a given chart + date and reports
/// its current lifecycle status. Returns <c>null</c> when no period
/// covers the date — <see cref="JournalPostingService"/> translates that
/// to <see cref="PostError.NoPeriodForDate"/>.
/// </summary>
/// <remarks>
/// The shape is intentionally minimal: a flat
/// <see cref="PeriodSnapshot"/> identifier + status, no entity. The
/// authoritative <c>FiscalPeriod</c> entity lives in the
/// <c>blocks-financial-periods</c> cluster; the ledger consumes only the
/// posting-gate signal this contract carries, so the ledger stays
/// ignorant of the periods package at compile time per Stage 02
/// §6.5(a) — period-management is a sibling cluster, not a parent of
/// the ledger.
/// </remarks>
public interface IPeriodResolver
{
    /// <summary>
    /// Look up the period covering <paramref name="date"/> within the
    /// chart identified by <paramref name="chartId"/>.
    /// </summary>
    Task<PeriodSnapshot?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Minimal projection of a fiscal period as consumed by the ledger
    /// posting-gate algorithm. <see cref="PeriodId"/> is opaque to the
    /// ledger; resolvers in sibling packages own the underlying entity.
    /// </summary>
    /// <param name="PeriodId">Opaque period identifier (string form).</param>
    /// <param name="ChartId">Owning chart identifier (string form).</param>
    /// <param name="Status">Current lifecycle status.</param>
    public readonly record struct PeriodSnapshot(
        string PeriodId,
        string ChartId,
        Status Status);

    /// <summary>
    /// Lifecycle states of the resolved period as consumed by the
    /// ledger posting-gate per Stage 02 §6.1. The authoritative enum
    /// (with audit fields, transition rules, and richer cases) lives in
    /// the <c>blocks-financial-periods</c> cluster.
    /// </summary>
    public enum Status
    {
        /// <summary>Postings allowed.</summary>
        Open,

        /// <summary>Postings blocked for regular users; <c>FinancialAdmin</c> role bypasses.</summary>
        SoftClosed,

        /// <summary>Postings blocked for all users; reversal must use a later open period.</summary>
        Locked,
    }
}

/// <summary>
/// In-memory <see cref="IPeriodResolver"/>. By default returns an
/// always-Open period (for tests that don't care about period-gating).
/// Test setup can swap the returned status via
/// <see cref="WithStatus"/>.
/// </summary>
public sealed class InMemoryPeriodResolver : IPeriodResolver
{
    private IPeriodResolver.Status _status = IPeriodResolver.Status.Open;

    /// <summary>
    /// Configure the status the resolver will report on the next call.
    /// </summary>
    public InMemoryPeriodResolver WithStatus(IPeriodResolver.Status status)
    {
        _status = status;
        return this;
    }

    /// <inheritdoc />
    public Task<IPeriodResolver.PeriodSnapshot?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IPeriodResolver.PeriodSnapshot?>(
            new IPeriodResolver.PeriodSnapshot(
                PeriodId: FiscalPeriodId.NewId().Value,
                ChartId:  chartId.Value,
                Status:   _status));
}

/// <summary>
/// <see cref="IPeriodResolver"/> that always returns <c>null</c> —
/// triggers <see cref="PostError.NoPeriodForDate"/> in the posting service.
/// </summary>
public sealed class NullPeriodResolver : IPeriodResolver
{
    /// <inheritdoc />
    public Task<IPeriodResolver.PeriodSnapshot?> ResolveAsync(
        ChartOfAccountsId chartId,
        DateOnly date,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IPeriodResolver.PeriodSnapshot?>(null);
}
