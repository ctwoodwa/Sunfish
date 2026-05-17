using System.Collections.Concurrent;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Models.Events;
using Sunfish.Blocks.FinancialTax.Seeds;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// In-memory <see cref="ITaxFormLineMapStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for
/// tests + the desktop in-process scenario; SQLite-backed
/// implementation lands in a later persistence-layer hand-off.
///
/// <para>
/// <see cref="SeedScheduleEAsync"/> idempotency: if any active row
/// already exists for the (chartId, ScheduleE, taxYear) trio, we
/// return <c>0</c> and don't overwrite. Preserves user edits per
/// the hand-off's mutability discipline.
/// </para>
///
/// <para>
/// Emits <c>Reports.TaxFormLineMapEdited</c> on every
/// <see cref="UpsertAsync"/> that changes an existing row (initial
/// inserts + seeds do not emit). See <see cref="DomainEventEnvelope{T}"/>
/// for envelope semantics.
/// </para>
/// </summary>
public sealed class InMemoryTaxFormLineMapStore : ITaxFormLineMapStore
{
    private readonly ConcurrentDictionary<TaxFormLineMapId, TaxFormLineMap> _rows = new();
    private readonly IDomainEventPublisher _events;

    public InMemoryTaxFormLineMapStore(IDomainEventPublisher? events = null)
    {
        _events = events ?? new NoopDomainEventPublisher();
    }

    /// <inheritdoc />
    public Task<TaxFormLineMap?> GetAsync(TaxFormLineMapId id, CancellationToken cancellationToken = default)
    {
        if (_rows.TryGetValue(id, out var row) && row.DeletedAtUtc is null)
        {
            return Task.FromResult<TaxFormLineMap?>(row);
        }
        return Task.FromResult<TaxFormLineMap?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxFormLineMap>> GetForFormAsync(
        FL.ChartOfAccountsId chartId,
        TaxFormKind formKind,
        int taxYear,
        CancellationToken cancellationToken = default)
    {
        var hits = _rows.Values
            .Where(r => r.DeletedAtUtc is null
                && r.ChartId == chartId
                && r.FormKind == formKind
                && r.TaxYear == taxYear
                && r.IsActive)
            .OrderBy(r => r.Line, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxFormLineMap>>(hits);
    }

    /// <inheritdoc />
    public async Task UpsertAsync(TaxFormLineMap map, CancellationToken cancellationToken = default)
    {
        if (map is null) throw new ArgumentNullException(nameof(map));
        var now = Instant.Now;
        var existed = _rows.TryGetValue(map.Id, out var existing);
        var stamped = existed
            ? map with { UpdatedAtUtc = now, Version = existing!.Version + 1 }
            : map with { CreatedAtUtc = map.CreatedAtUtc ?? now, UpdatedAtUtc = now };
        _rows[stamped.Id] = stamped;

        // Only emit on real edits, not on first-insert / seeds — keeps
        // the seed path quiet (would otherwise produce ~20 events on
        // SeedScheduleEAsync).
        if (existed)
        {
            var envelope = DomainEventEnvelopeFactory.Build(
                eventType: FinancialTaxEventNames.TaxFormLineMapEdited,
                payload: new TaxFormLineMapEdited(
                    MapId: stamped.Id,
                    ChartId: stamped.ChartId,
                    FormKind: stamped.FormKind,
                    TaxYear: stamped.TaxYear,
                    Line: stamped.Line,
                    PriorSelectors: existing!.AccountSelectors,
                    NewSelectors: stamped.AccountSelectors,
                    NewVersion: stamped.Version,
                    EditedByPrincipalId: null),
                idempotencyKey: $"{FinancialTaxEventNames.TaxFormLineMapEdited}|__system__|{stamped.Id}|v{stamped.Version}");
            await _events.PublishAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<int> SeedScheduleEAsync(
        FL.ChartOfAccountsId chartId,
        int taxYear,
        CancellationToken cancellationToken = default)
    {
        // Idempotency: bail (returning 0) when any active Schedule E
        // row already exists for this chart + year. Preserves edits.
        var alreadyExists = _rows.Values.Any(r =>
            r.DeletedAtUtc is null
            && r.ChartId == chartId
            && r.FormKind == TaxFormKind.ScheduleE
            && r.TaxYear == taxYear);
        if (alreadyExists)
        {
            return Task.FromResult(0);
        }

        var seed = DefaultTaxFormLineMap.ScheduleE(chartId, taxYear);
        foreach (var row in seed)
        {
            _rows[row.Id] = row;
        }
        return Task.FromResult(seed.Count);
    }

    /// <inheritdoc />
    public Task SoftDeleteAsync(TaxFormLineMapId id, Instant deletedAtUtc, CancellationToken cancellationToken = default)
    {
        if (_rows.TryGetValue(id, out var existing) && existing.DeletedAtUtc is null)
        {
            _rows[id] = existing with { DeletedAtUtc = deletedAtUtc, UpdatedAtUtc = deletedAtUtc };
        }
        return Task.CompletedTask;
    }
}
