using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialAp.Services;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAp.Migration;

/// <summary>
/// Default <see cref="IErpnextPurchaseInvoiceImporter"/>. Uses
/// <see cref="IBillRepository"/> directly. The bill carries the
/// vendor's own number from ERPNext's <c>bill_no</c> (falling back to
/// <c>name</c> when bill_no is blank) — AP doesn't mint canonical
/// numbers like AR does.
/// </summary>
public sealed class ErpnextPurchaseInvoiceImporter : IErpnextPurchaseInvoiceImporter
{
    private const string ExternalRefPrefix = "erpnext:pinv:";
    private const string ModifiedKeyPrefix = "erpnextModified:";

    private readonly IBillRepository _bills;

    public ErpnextPurchaseInvoiceImporter(IBillRepository bills)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
    }

    /// <inheritdoc />
    public async Task<ImportOutcome<Bill>> UpsertPurchaseInvoiceAsync(
        ErpnextPurchaseInvoiceSource source,
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId vendorPartyId,
        GLAccountId apAccountId,
        GLAccountId defaultExpenseAccountId,
        CancellationToken cancellationToken = default)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (string.IsNullOrWhiteSpace(source.Name))
            return new ImportOutcome<Bill>(ImportOutcomeKind.Failed, null, "ERPNext PurchaseInvoice.name is empty.");
        if (string.IsNullOrWhiteSpace(source.Supplier))
            return new ImportOutcome<Bill>(ImportOutcomeKind.Failed, null, "ERPNext PurchaseInvoice.supplier is empty.");
        if (source.Items is null || source.Items.Count == 0)
            return new ImportOutcome<Bill>(ImportOutcomeKind.Failed, null, "ERPNext PurchaseInvoice has no items.");

        var externalRef = ExternalRefPrefix + source.Name;
        var modifiedMarker = ModifiedKeyPrefix + source.Modified;

        var existing = await _bills.GetByExternalRefAsync(chartId, externalRef, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null
            && existing.Notes is not null
            && existing.Notes.Contains(modifiedMarker, StringComparison.Ordinal))
        {
            return new ImportOutcome<Bill>(
                ImportOutcomeKind.Skipped,
                existing,
                $"Already imported at modified={source.Modified}.");
        }

        var canonicalStatus = MapStatus(source.Status);
        var amountPaid = source.GrandTotal - source.OutstandingAmount;
        if (amountPaid < 0m) amountPaid = 0m;

        var billId = existing?.Id ?? BillId.NewId();
        var billNumber = string.IsNullOrWhiteSpace(source.BillNo) ? source.Name : source.BillNo!;
        var lines = new List<BillLine>(source.Items.Count);
        var lineNo = 1;
        foreach (var item in source.Items)
        {
            var debitAccount = !string.IsNullOrEmpty(item.ExpenseAccount)
                ? new GLAccountId(item.ExpenseAccount!)
                : defaultExpenseAccountId;
            var amount = item.Amount > 0m
                ? item.Amount
                : decimal.Round(item.Qty * item.Rate, 2, MidpointRounding.ToEven);

            lines.Add(new BillLine
            {
                Id = BillLineId.NewId(),
                BillId = billId,
                LineNumber = lineNo++,
                Description = item.ItemName,
                Quantity = item.Qty,
                UnitPrice = item.Rate,
                Amount = amount,
                DebitAccountId = debitAccount,
                PropertyId = item.CostCenter,
            });
        }

        var currency = string.IsNullOrWhiteSpace(source.Currency) ? "USD" : source.Currency!;

        var now = Instant.Now;
        var subtotal = lines.Sum(l => l.Amount);
        var taxTotal = source.GrandTotal - subtotal;
        if (taxTotal < 0m) taxTotal = 0m;
        var total = subtotal + taxTotal;
        var balance = total - amountPaid;
        if (balance < 0m) balance = 0m;

        var bill = new Bill
        {
            Id = billId,
            TenantId = tenantId,
            ChartId = chartId,
            BillNumber = billNumber,
            VendorId = vendorPartyId,
            BillDate = source.BillDate ?? source.PostingDate,
            DueDate = source.DueDate,
            ReceivedDate = source.PostingDate,
            Currency = currency,
            Lines = lines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            AmountPaid = amountPaid,
            Balance = balance,
            Status = canonicalStatus,
            ApAccountId = apAccountId,
            ExternalRef = externalRef,
            Notes = modifiedMarker,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            CreatedBy = existing?.CreatedBy,
            UpdatedAtUtc = now,
            Version = (existing?.Version ?? 0L) + 1L,
        };

        await _bills.UpsertAsync(bill, cancellationToken).ConfigureAwait(false);

        return existing is null
            ? new ImportOutcome<Bill>(ImportOutcomeKind.Inserted, bill, $"Imported {source.Name}.")
            : new ImportOutcome<Bill>(ImportOutcomeKind.Updated, bill, $"Reconciled {source.Name} to modified={source.Modified}.");
    }

    /// <summary>
    /// Map ERPNext Purchase Invoice <c>status</c> codes to canonical AP.
    ///
    /// <list type="bullet">
    /// <item><c>"Draft"</c> → <see cref="BillStatus.Draft"/></item>
    /// <item><c>"Submitted"</c>, <c>"Overdue"</c>, <c>"Return"</c>, <c>"Debit Note Issued"</c> → <see cref="BillStatus.Received"/></item>
    /// <item><c>"Partly Paid"</c>, <c>"Partly Paid and Discounted"</c> → <see cref="BillStatus.PartiallyPaid"/></item>
    /// <item><c>"Paid"</c>, <c>"Paid and Discounted"</c> → <see cref="BillStatus.Paid"/></item>
    /// <item><c>"Cancelled"</c> → <see cref="BillStatus.Voided"/></item>
    /// <item>Unknown / empty → <see cref="BillStatus.Draft"/> as the safest non-posting state.</item>
    /// </list>
    /// </summary>
    public static BillStatus MapStatus(string? erpnextStatus) =>
        erpnextStatus?.Trim() switch
        {
            "Draft" or null or "" => BillStatus.Draft,
            "Submitted" or "Overdue" or "Return" or "Debit Note Issued" => BillStatus.Received,
            "Partly Paid" or "Partly Paid and Discounted" => BillStatus.PartiallyPaid,
            "Paid" or "Paid and Discounted" => BillStatus.Paid,
            "Cancelled" => BillStatus.Voided,
            _ => BillStatus.Draft,
        };
}
