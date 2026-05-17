namespace Sunfish.Blocks.FinancialAr.Migration;

/// <summary>
/// Source record from an ERPNext <c>Sales Invoice</c> doctype. Field shape
/// mirrors the Frappe REST API verbatim so the importer can be fed
/// directly from a Frappe HTTP client or <c>erpnext.ts</c>.
/// </summary>
/// <param name="Name">ERPNext <c>name</c> — stable id (e.g. <c>"SINV-0001"</c>); the FK we dedupe on.</param>
/// <param name="Modified">ERPNext <c>modified</c> — version key; ordinal compare decides Skipped vs Updated.</param>
/// <param name="Customer">ERPNext <c>customer</c> — the ERPNext customer Name. The importer DOES NOT resolve this to a canonical <see cref="Sunfish.Blocks.People.Foundation.Models.PartyId"/>; the caller passes the resolved id at import time.</param>
/// <param name="PostingDate">Invoice issue date.</param>
/// <param name="DueDate">Payment-due date.</param>
/// <param name="Currency">ISO 4217 currency code; defaults to USD when null/blank.</param>
/// <param name="Items">Line items — order preserved into <see cref="Sunfish.Blocks.FinancialAr.Models.InvoiceLine.LineNumber"/>.</param>
/// <param name="Status">ERPNext <c>status</c> — one of <c>"Draft"</c>, <c>"Submitted"</c>, <c>"Paid"</c>, <c>"Cancelled"</c>, etc. Mapped to canonical <see cref="Sunfish.Blocks.FinancialAr.Models.InvoiceStatus"/> by the importer.</param>
/// <param name="GrandTotal">ERPNext <c>grand_total</c> — sum of line amounts + tax. Used as a sanity check against materialized canonical total.</param>
/// <param name="OutstandingAmount">ERPNext <c>outstanding_amount</c> — what's still owed. Drives the canonical AmountPaid / Balance derivation.</param>
public sealed record ErpnextSalesInvoiceSource(
    string Name,
    string Modified,
    string Customer,
    DateOnly PostingDate,
    DateOnly DueDate,
    string? Currency,
    IReadOnlyList<ErpnextSalesInvoiceItem> Items,
    string Status,
    decimal GrandTotal,
    decimal OutstandingAmount);

/// <summary>
/// One line of an ERPNext Sales Invoice's <c>items</c> table.
/// </summary>
/// <param name="ItemName">Human-readable description rendered on the invoice.</param>
/// <param name="Qty">Quantity (decimal — ERPNext allows fractional).</param>
/// <param name="Rate">Unit price in invoice currency.</param>
/// <param name="Amount">Pre-computed line total — used as the canonical line's Amount when present (preserves ERPNext's exact rounding).</param>
/// <param name="IncomeAccount">ERPNext <c>income_account</c> for this line; null falls back to the importer's <c>defaultIncomeAccountId</c>.</param>
/// <param name="CostCenter">Optional ERPNext cost-center; passed through as canonical PropertyId.</param>
public sealed record ErpnextSalesInvoiceItem(
    string ItemName,
    decimal Qty,
    decimal Rate,
    decimal Amount,
    string? IncomeAccount = null,
    string? CostCenter = null);
