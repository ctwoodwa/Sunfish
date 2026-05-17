using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialAr.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Migration;

/// <summary>
/// Default <see cref="IErpnextSalesInvoiceImporter"/>. Uses
/// <see cref="IInvoiceRepository"/> directly (read + write); the importer
/// holds no state of its own. Coordinates with
/// <see cref="IInvoiceNumberingService"/> to mint canonical invoice
/// numbers for newly-imported records (ERPNext's own <c>name</c> lives
/// on <see cref="Invoice.ExternalRef"/>; the customer-facing number is
/// a fresh canonical one so the format-gate in
/// <see cref="InMemoryInvoiceRepository.UpsertAsync"/> succeeds).
/// </summary>
public sealed class ErpnextSalesInvoiceImporter : IErpnextSalesInvoiceImporter
{
    private const string ExternalRefPrefix = "erpnext:sinv:";
    private const string ModifiedKeyPrefix = "erpnextModified:";

    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceNumberingService _numbering;

    public ErpnextSalesInvoiceImporter(
        IInvoiceRepository invoices,
        IInvoiceNumberingService numbering)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
        _numbering = numbering ?? throw new ArgumentNullException(nameof(numbering));
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<Invoice>> UpsertSalesInvoiceAsync(
        ErpnextSalesInvoiceSource source,
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId customerPartyId,
        GLAccountId arAccountId,
        GLAccountId defaultIncomeAccountId,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.Name))
            return new ImportOutcome<Invoice>(ImportOutcomeKind.Failed, null, "ERPNext SalesInvoice.name is empty.");
        if (string.IsNullOrWhiteSpace(source.Customer))
            return new ImportOutcome<Invoice>(ImportOutcomeKind.Failed, null, "ERPNext SalesInvoice.customer is empty.");
        if (source.Items is null || source.Items.Count == 0)
            return new ImportOutcome<Invoice>(ImportOutcomeKind.Failed, null, "ERPNext SalesInvoice has no items.");

        var externalRef = ExternalRefPrefix + source.Name;
        var modifiedMarker = ModifiedKeyPrefix + source.Modified;

        var existing = await FindExistingByExternalRefAsync(chartId, externalRef, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null
            && existing.Notes is not null
            && existing.Notes.Contains(modifiedMarker, StringComparison.Ordinal))
        {
            return new ImportOutcome<Invoice>(
                ImportOutcomeKind.Skipped,
                existing,
                $"Already imported at modified={source.Modified}.");
        }

        // Map status + amounts.
        var canonicalStatus = MapStatus(source.Status);
        var amountPaid = source.GrandTotal - source.OutstandingAmount;
        if (amountPaid < 0m) amountPaid = 0m;

        // Build invoice lines from items.
        var invoiceId = existing?.Id ?? InvoiceId.NewId();
        var lines = new List<InvoiceLine>(source.Items.Count);
        var lineNo = 1;
        foreach (var item in source.Items)
        {
            var income = !string.IsNullOrEmpty(item.IncomeAccount)
                ? new GLAccountId(item.IncomeAccount!)
                : defaultIncomeAccountId;
            // Prefer ERPNext's pre-computed Amount when supplied (preserves
            // their exact rounding); fall back to qty*rate if absent.
            var amount = item.Amount > 0m ? item.Amount : decimal.Round(item.Qty * item.Rate, 2, MidpointRounding.ToEven);

            lines.Add(new InvoiceLine
            {
                Id = InvoiceLineId.NewId(),
                InvoiceId = invoiceId,
                LineNumber = lineNo++,
                Description = item.ItemName,
                Quantity = item.Qty,
                UnitPrice = item.Rate,
                Amount = amount,
                IncomeAccountId = income,
                PropertyId = item.CostCenter,
            });
        }

        var currency = string.IsNullOrWhiteSpace(source.Currency) ? "USD" : source.Currency!;

        // Mint canonical number when this is a fresh import (non-Draft only —
        // Draft can have an empty number; non-Draft must match the canonical
        // format gate in InMemoryInvoiceRepository.UpsertAsync).
        string invoiceNumber;
        if (existing is null)
        {
            invoiceNumber = canonicalStatus == InvoiceStatus.Draft
                ? ""
                : await _numbering.NextNumberAsync(chartId, source.PostingDate, cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            invoiceNumber = existing.InvoiceNumber;
        }

        var now = Instant.Now;
        var subtotal = lines.Sum(l => l.Amount);
        var taxTotal = source.GrandTotal - subtotal;
        if (taxTotal < 0m) taxTotal = 0m;
        var total = subtotal + taxTotal;
        var balance = total - amountPaid;
        if (balance < 0m) balance = 0m;

        var inv = new Invoice
        {
            Id = invoiceId,
            TenantId = tenantId,
            ChartId = chartId,
            InvoiceNumber = invoiceNumber,
            CustomerId = customerPartyId,
            IssueDate = source.PostingDate,
            DueDate = source.DueDate,
            Currency = currency,
            Lines = lines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            AmountPaid = amountPaid,
            Balance = balance,
            Status = canonicalStatus,
            ArAccountId = arAccountId,
            ExternalRef = externalRef,
            Notes = modifiedMarker,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            CreatedBy = existing?.CreatedBy,
            UpdatedAtUtc = now,
            Version = (existing?.Version ?? 0L) + 1L,
        };

        await _invoices.UpsertAsync(inv, cancellationToken).ConfigureAwait(false);

        return existing is null
            ? new ImportOutcome<Invoice>(ImportOutcomeKind.Inserted, inv, $"Imported {source.Name}.")
            : new ImportOutcome<Invoice>(ImportOutcomeKind.Updated, inv, $"Reconciled {source.Name} to modified={source.Modified}.");
    }

    private async Task<Invoice?> FindExistingByExternalRefAsync(
        ChartOfAccountsId chartId,
        string externalRef,
        CancellationToken cancellationToken)
    {
        var all = await _invoices.ListByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(i =>
            string.Equals(i.ExternalRef, externalRef, StringComparison.Ordinal));
    }

    /// <summary>
    /// Map ERPNext Sales Invoice <c>status</c> codes to canonical AR.
    ///
    /// <list type="bullet">
    /// <item><c>"Draft"</c> → <see cref="InvoiceStatus.Draft"/></item>
    /// <item><c>"Submitted"</c>, <c>"Overdue"</c>, <c>"Return"</c>, <c>"Credit Note Issued"</c> → <see cref="InvoiceStatus.Issued"/></item>
    /// <item><c>"Partly Paid"</c>, <c>"Partly Paid and Discounted"</c> → <see cref="InvoiceStatus.PartiallyPaid"/></item>
    /// <item><c>"Paid"</c>, <c>"Paid and Discounted"</c> → <see cref="InvoiceStatus.Paid"/></item>
    /// <item><c>"Cancelled"</c> → <see cref="InvoiceStatus.Voided"/></item>
    /// <item>Unknown / empty → <see cref="InvoiceStatus.Draft"/> as the safest non-posting state.</item>
    /// </list>
    /// </summary>
    public static InvoiceStatus MapStatus(string? erpnextStatus) =>
        erpnextStatus?.Trim() switch
        {
            "Draft" or null or "" => InvoiceStatus.Draft,
            "Submitted" or "Overdue" or "Return" or "Credit Note Issued" => InvoiceStatus.Issued,
            "Partly Paid" or "Partly Paid and Discounted" => InvoiceStatus.PartiallyPaid,
            "Paid" or "Paid and Discounted" => InvoiceStatus.Paid,
            "Cancelled" => InvoiceStatus.Voided,
            _ => InvoiceStatus.Draft,
        };
}
