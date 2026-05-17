using System.Collections.Concurrent;
using FL = Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.FinancialLedger.Services;
using Sunfish.Blocks.FinancialTax.Models;
using Sunfish.Blocks.FinancialTax.Models.Events;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Events;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// In-memory <see cref="ITaxRateLookup"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Suitable for tests
/// + the desktop in-process scenario; SQLite-backed implementation
/// lands in a later persistence-layer hand-off.
///
/// <para>
/// Concurrency: <see cref="SupersedeAsync"/> uses a per-(TaxCode,
/// Jurisdiction) lock — held only across the expire-then-insert pair
/// — so a concurrent supersede on a different (TaxCode, Jurisdiction)
/// can run in parallel. The SQLite implementation will use a
/// transaction with row-level locking instead.
/// </para>
/// </summary>
public sealed class InMemoryTaxRateLookup : ITaxRateLookup
{
    private readonly ConcurrentDictionary<TaxRateId, TaxRate> _rows = new();
    private readonly ConcurrentDictionary<(TaxCodeId, TaxJurisdictionId), object> _supersedeLocks = new();
    private readonly IAccountResolver _accounts;
    private readonly IDomainEventPublisher _events;

    public InMemoryTaxRateLookup(IAccountResolver accounts, IDomainEventPublisher? events = null)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _events = events ?? new NoopDomainEventPublisher();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxRate>> GetActiveRatesAsync(
        TaxCodeId taxCodeId,
        DateOnly date,
        IReadOnlyCollection<TaxJurisdictionId> jurisdictionIds,
        CancellationToken cancellationToken = default)
    {
        var jurisdictionSet = new HashSet<TaxJurisdictionId>(jurisdictionIds);
        var hits = _rows.Values
            .Where(r => r.TaxCodeId == taxCodeId)
            .Where(r => jurisdictionSet.Contains(r.JurisdictionId))
            .Where(r => r.IsActiveOn(date))
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxRate>>(hits);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxRate>> GetHistoryAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        CancellationToken cancellationToken = default)
    {
        var hits = _rows.Values
            .Where(r => r.TaxCodeId == taxCodeId && r.JurisdictionId == jurisdictionId)
            .OrderBy(r => r.EffectiveDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxRate>>(hits);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TaxRate>> GetAllForTaxCodeAsync(
        TaxCodeId taxCodeId,
        CancellationToken cancellationToken = default)
    {
        var hits = _rows.Values
            .Where(r => r.TaxCodeId == taxCodeId)
            .OrderBy(r => r.JurisdictionId.Value, StringComparer.Ordinal)
            .ThenBy(r => r.EffectiveDate)
            .ToList();
        return Task.FromResult<IReadOnlyList<TaxRate>>(hits);
    }

    /// <inheritdoc />
    public async Task<TaxRateUpsertResult> UpsertAsync(
        TaxRate candidate,
        CancellationToken cancellationToken = default)
    {
        if (candidate is null) throw new ArgumentNullException(nameof(candidate));

        var accountCheck = await ValidatePayableAccountAsync(candidate.PayableAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (accountCheck.error != TaxRateValidationError.None)
        {
            return new TaxRateUpsertResult(null, accountCheck.error, accountCheck.detail);
        }

        var overlapCheck = FindOverlap(candidate);
        if (overlapCheck is not null)
        {
            return new TaxRateUpsertResult(
                null,
                TaxRateValidationError.DateRangeOverlap,
                $"Candidate {candidate.EffectiveDate}..{candidate.ExpiryDate?.ToString() ?? "open"} overlaps existing rate {overlapCheck.Id} ({overlapCheck.EffectiveDate}..{overlapCheck.ExpiryDate?.ToString() ?? "open"}).");
        }

        _rows[candidate.Id] = candidate;

        var addedEnvelope = DomainEventEnvelopeFactory.Build(
            eventType: FinancialTaxEventNames.TaxRateAdded,
            payload: new TaxRateAdded(
                candidate.Id, candidate.TaxCodeId, candidate.JurisdictionId,
                candidate.RatePercent, candidate.EffectiveDate, candidate.PayableAccountId),
            idempotencyKey: $"{FinancialTaxEventNames.TaxRateAdded}|__system__|{candidate.Id}|added");
        await _events.PublishAsync(addedEnvelope, cancellationToken).ConfigureAwait(false);

        return new TaxRateUpsertResult(candidate, TaxRateValidationError.None, null);
    }

    /// <inheritdoc />
    public async Task<TaxRateSupersedeResult> SupersedeAsync(
        TaxCodeId taxCodeId,
        TaxJurisdictionId jurisdictionId,
        decimal newRatePercent,
        DateOnly newEffectiveDate,
        FL.GLAccountId payableAccountId,
        CancellationToken cancellationToken = default)
    {
        // Resolve the payable account once up front so a bad account
        // doesn't take the lock + then bail.
        var accountCheck = await ValidatePayableAccountAsync(payableAccountId, cancellationToken)
            .ConfigureAwait(false);
        if (accountCheck.error != TaxRateValidationError.None)
        {
            return new TaxRateSupersedeResult(null, null, accountCheck.error, accountCheck.detail);
        }

        var lockKey = (taxCodeId, jurisdictionId);
        var lockObj = _supersedeLocks.GetOrAdd(lockKey, _ => new object());

        TaxRate? oldRate;
        TaxRate? oldRateExpired;
        TaxRate? newRate;
        TaxRateValidationError error;
        string? detail;

        lock (lockObj)
        {
            // Snapshot current state under the lock.
            var current = _rows.Values
                .Where(r => r.TaxCodeId == taxCodeId
                    && r.JurisdictionId == jurisdictionId
                    && r.DeletedAtUtc is null
                    && r.ExpiryDate is null)
                .OrderByDescending(r => r.EffectiveDate)
                .FirstOrDefault();

            if (current is null)
            {
                return new TaxRateSupersedeResult(
                    null,
                    null,
                    TaxRateValidationError.NoActiveRateToSupersede,
                    $"No open-ended rate found for ({taxCodeId}, {jurisdictionId}).");
            }

            oldRate = current;

            // The new rate has to land first so we can validate range
            // overlap against the as-yet-unexpired current. We do this
            // by computing both proposed rows + then applying both
            // under the same lock — atomic from the caller's POV.
            try
            {
                newRate = TaxRate.Create(
                    taxCodeId: taxCodeId,
                    jurisdictionId: jurisdictionId,
                    ratePercent: newRatePercent,
                    effectiveDate: newEffectiveDate,
                    payableAccountId: payableAccountId);
            }
            catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException)
            {
                return new TaxRateSupersedeResult(null, null, TaxRateValidationError.None, ex.Message);
            }

            oldRateExpired = oldRate with { ExpiryDate = newEffectiveDate.AddDays(-1) };

            // Validate: oldRateExpired must not violate non-overlap
            // (it can't — we're only narrowing its window) and the
            // new rate must not overlap any OTHER row (i.e., any rate
            // that isn't the one we're about to expire).
            var conflict = FindOverlap(newRate, excludeId: oldRate.Id);
            if (conflict is not null)
            {
                return new TaxRateSupersedeResult(
                    oldRate,
                    null,
                    TaxRateValidationError.DateRangeOverlap,
                    $"New rate {newEffectiveDate}.. overlaps existing rate {conflict.Id} ({conflict.EffectiveDate}..{conflict.ExpiryDate?.ToString() ?? "open"}).");
            }

            error = TaxRateValidationError.None;
            detail = null;

            // Atomic-from-the-caller's-POV: both writes happen under
            // the lock. If we wanted true transactional rollback we'd
            // also need to defend against process kill between the two
            // _rows[..] = ..; lines — out of scope for the in-memory
            // implementation; the SQLite version handles this via a
            // transaction.
            _rows[oldRateExpired.Id] = oldRateExpired;
            _rows[newRate.Id] = newRate;
        }

        // Emit events outside the lock so handlers can't deadlock against
        // each other. Order is significant: TaxRateExpired before
        // TaxRateAdded so consumers see the supersede as a transition.
        var expiredEnvelope = DomainEventEnvelopeFactory.Build(
            eventType: FinancialTaxEventNames.TaxRateExpired,
            payload: new TaxRateExpired(
                oldRateExpired!.Id, oldRateExpired.TaxCodeId, oldRateExpired.JurisdictionId,
                oldRateExpired.ExpiryDate!.Value),
            idempotencyKey: $"{FinancialTaxEventNames.TaxRateExpired}|__system__|{oldRateExpired.Id}|expired");
        await _events.PublishAsync(expiredEnvelope, cancellationToken).ConfigureAwait(false);

        var addedEnvelope = DomainEventEnvelopeFactory.Build(
            eventType: FinancialTaxEventNames.TaxRateAdded,
            payload: new TaxRateAdded(
                newRate!.Id, newRate.TaxCodeId, newRate.JurisdictionId,
                newRate.RatePercent, newRate.EffectiveDate, newRate.PayableAccountId),
            idempotencyKey: $"{FinancialTaxEventNames.TaxRateAdded}|__system__|{newRate.Id}|added",
            causationId: expiredEnvelope.EventId);
        await _events.PublishAsync(addedEnvelope, cancellationToken).ConfigureAwait(false);

        return new TaxRateSupersedeResult(oldRateExpired, newRate, error, detail);
    }

    private async Task<(TaxRateValidationError error, string? detail)> ValidatePayableAccountAsync(
        FL.GLAccountId payableAccountId,
        CancellationToken cancellationToken)
    {
        var account = await _accounts.GetAsync(payableAccountId, cancellationToken).ConfigureAwait(false);
        if (account is null)
        {
            return (TaxRateValidationError.PayableAccountNotFound,
                    $"PayableAccountId {payableAccountId} not found.");
        }
        if (account.Type != FL.GLAccountType.Liability)
        {
            return (TaxRateValidationError.PayableAccountWrongType,
                    $"PayableAccountId {payableAccountId} has type {account.Type}, expected Liability.");
        }
        if (account.Subtype != FL.AccountSubtype.TaxesPayable)
        {
            return (TaxRateValidationError.PayableAccountWrongSubtype,
                    $"PayableAccountId {payableAccountId} has subtype {account.Subtype}, expected TaxesPayable.");
        }
        return (TaxRateValidationError.None, null);
    }

    /// <summary>
    /// Returns an existing rate whose effective-to-expiry window
    /// overlaps the candidate's for the same (TaxCodeId, JurisdictionId),
    /// or <c>null</c> when no overlap exists.
    /// </summary>
    /// <param name="candidate">The rate we want to insert.</param>
    /// <param name="excludeId">
    /// When set, skip the row with this id — used by SupersedeAsync so
    /// the about-to-be-expired current row doesn't count as an overlap
    /// against the new row that replaces it.
    /// </param>
    private TaxRate? FindOverlap(TaxRate candidate, TaxRateId? excludeId = null)
    {
        var siblings = _rows.Values
            .Where(r => r.TaxCodeId == candidate.TaxCodeId
                && r.JurisdictionId == candidate.JurisdictionId
                && r.DeletedAtUtc is null);
        foreach (var sibling in siblings)
        {
            if (excludeId is not null && sibling.Id == excludeId.Value) continue;
            if (Overlaps(candidate, sibling)) return sibling;
        }
        return null;
    }

    private static bool Overlaps(TaxRate a, TaxRate b)
    {
        // Treat null ExpiryDate as DateOnly.MaxValue for the comparison.
        var aEnd = a.ExpiryDate ?? DateOnly.MaxValue;
        var bEnd = b.ExpiryDate ?? DateOnly.MaxValue;
        // Two closed ranges overlap iff start_a <= end_b AND start_b <= end_a.
        return a.EffectiveDate <= bEnd && b.EffectiveDate <= aEnd;
    }
}
