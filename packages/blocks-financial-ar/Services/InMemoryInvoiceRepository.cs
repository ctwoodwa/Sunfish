using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// In-memory <see cref="IInvoiceRepository"/>. State lives in a single
/// <c>ConcurrentDictionary</c> keyed by <see cref="InvoiceId"/>;
/// secondary queries (by chart, by number, by customer) scan the values
/// — fine for the in-memory v1 with O(invoices) on a single tenant. A
/// SQLite-backed implementation lands in the follow-on substrate
/// hand-off and shadows this binding.
/// </summary>
public sealed class InMemoryInvoiceRepository : IInvoiceRepository
{
    private readonly ConcurrentDictionary<InvoiceId, Invoice> _invoices = new();

    /// <inheritdoc />
    public Task UpsertAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (_invoices.TryGetValue(invoice.Id, out var existing) && existing.DeletedAtUtc is not null)
            throw new InvalidOperationException($"Invoice '{invoice.Id.Value}' is tombstoned; further mutations are not permitted.");

        // Drafts may carry an empty InvoiceNumber (PR 3 mints on Issue).
        // Issued+ invoices MUST match the canonical numbering format —
        // a malformed number would surface as a bad ERPNext-importer
        // payload or a misuse of `Invoice.Create` with hand-rolled string.
        if (invoice.Status != Models.InvoiceStatus.Draft
            && !InvoiceNumberFormat.IsWellFormed(invoice.InvoiceNumber))
        {
            throw new InvalidOperationException(
                $"Invoice '{invoice.Id.Value}' is in status '{invoice.Status}' but its InvoiceNumber '{invoice.InvoiceNumber}' does not match the canonical format 'INV-YYYY-MM-DD-{{Replica}}-{{NNNN}}'.");
        }

        _invoices[invoice.Id] = invoice;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Invoice?> GetAsync(InvoiceId id, CancellationToken cancellationToken = default)
    {
        _invoices.TryGetValue(id, out var inv);
        return Task.FromResult(inv is null || inv.DeletedAtUtc is not null ? null : inv);
    }

    /// <inheritdoc />
    public Task<Invoice?> GetByNumberAsync(ChartOfAccountsId chartId, string invoiceNumber, CancellationToken cancellationToken = default)
    {
        var hit = _invoices.Values.FirstOrDefault(i =>
            i.DeletedAtUtc is null
            && i.ChartId == chartId
            && string.Equals(i.InvoiceNumber, invoiceNumber, StringComparison.Ordinal));
        return Task.FromResult<Invoice?>(hit);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Invoice>> ListByChartAsync(ChartOfAccountsId chartId, CancellationToken cancellationToken = default)
    {
        var rows = _invoices.Values
            .Where(i => i.DeletedAtUtc is null && i.ChartId == chartId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(rows);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Invoice>> ListByCustomerAsync(ChartOfAccountsId chartId, PartyId customerId, CancellationToken cancellationToken = default)
    {
        var rows = _invoices.Values
            .Where(i => i.DeletedAtUtc is null && i.ChartId == chartId && i.CustomerId == customerId)
            .ToList();
        return Task.FromResult<IReadOnlyList<Invoice>>(rows);
    }

    /// <inheritdoc />
    public Task<bool> SoftDeleteAsync(InvoiceId id, PartyId actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!_invoices.TryGetValue(id, out var inv)) return Task.FromResult(false);
        if (inv.DeletedAtUtc is not null) return Task.FromResult(true); // idempotent

        var now = Instant.Now;
        _invoices[id] = inv with
        {
            DeletedAtUtc = now,
            DeletedBy = actor,
            DeletedReason = reason,
            UpdatedAtUtc = now,
            UpdatedBy = actor,
            Version = inv.Version + 1,
        };
        return Task.FromResult(true);
    }
}
