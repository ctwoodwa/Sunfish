namespace Sunfish.Blocks.FinancialAp.Migration;

/// <summary>
/// Source record from an ERPNext <c>Purchase Invoice</c> doctype. Mirror
/// of <c>ErpnextSalesInvoiceSource</c> on the AR side; field shape
/// matches the Frappe REST API so the importer can be fed directly
/// from a Frappe HTTP client or <c>erpnext.ts</c>.
/// </summary>
/// <param name="Name">ERPNext <c>name</c> — stable id (e.g. <c>"PINV-0001"</c>); the FK we dedupe on.</param>
/// <param name="Modified">ERPNext <c>modified</c> — version key; ordinal compare decides Skipped vs Updated.</param>
/// <param name="Supplier">ERPNext <c>supplier</c> — vendor's ERPNext name. Importer does NOT resolve to canonical PartyId; caller passes resolved id.</param>
/// <param name="BillNo">Vendor's own document number from ERPNext <c>bill_no</c>; falls back to <see cref="Name"/> when blank (some workflows skip bill_no).</param>
/// <param name="PostingDate">ERPNext <c>posting_date</c> — bill date.</param>
/// <param name="DueDate">Payment-due date.</param>
/// <param name="BillDate">Vendor-side bill date from ERPNext <c>bill_date</c>; falls back to <see cref="PostingDate"/> when null.</param>
/// <param name="Currency">ISO 4217 currency; defaults to USD when null/blank.</param>
/// <param name="Items">Line items — order preserved.</param>
/// <param name="Status">ERPNext <c>status</c>. Mapped to canonical <see cref="Sunfish.Blocks.FinancialAp.Models.BillStatus"/> by the importer.</param>
/// <param name="GrandTotal">ERPNext <c>grand_total</c>.</param>
/// <param name="OutstandingAmount">ERPNext <c>outstanding_amount</c>. Drives canonical AmountPaid + Balance.</param>
public sealed record ErpnextPurchaseInvoiceSource(
    string Name,
    string Modified,
    string Supplier,
    string? BillNo,
    DateOnly PostingDate,
    DateOnly DueDate,
    DateOnly? BillDate,
    string? Currency,
    IReadOnlyList<ErpnextPurchaseInvoiceItem> Items,
    string Status,
    decimal GrandTotal,
    decimal OutstandingAmount);

/// <summary>One line of an ERPNext Purchase Invoice's <c>items</c> table.</summary>
/// <param name="ItemName">Human-readable description rendered on the bill.</param>
/// <param name="Qty">Quantity.</param>
/// <param name="Rate">Unit price.</param>
/// <param name="Amount">Pre-computed line total — preserved bit-for-bit when present.</param>
/// <param name="ExpenseAccount">ERPNext <c>expense_account</c> for this line; null falls back to the importer's <c>defaultExpenseAccountId</c>.</param>
/// <param name="CostCenter">Optional ERPNext cost-center; passed through as canonical PropertyId.</param>
public sealed record ErpnextPurchaseInvoiceItem(
    string ItemName,
    decimal Qty,
    decimal Rate,
    decimal Amount,
    string? ExpenseAccount = null,
    string? CostCenter = null);
