using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// Canonical invoice — the AR-side billable document. Carries header + line
/// items, the AR control-account handle, and lifecycle pointers into the GL
/// (<see cref="JournalEntryId"/>, <see cref="VoidedByEntryId"/>,
/// <see cref="WrittenOffByEntryId"/>) so an audit reader can pivot to the
/// posting that created/reversed/wrote-off the invoice without external joins.
///
/// <para>
/// <b>InvoiceNumber:</b> a separate field from <see cref="Id"/>. <c>Id</c>
/// is the internal opaque identifier; <c>InvoiceNumber</c> is what the
/// customer sees on the document (e.g., <c>"INV-2026-05-17-AB-0001"</c>).
/// In PR 1 the caller supplies this value directly; PR 2 introduces
/// <c>IInvoiceNumberingService</c> which mints monotonic per-replica numbers.
/// </para>
///
/// <para>
/// <b>Monetary totals are cached, not computed.</b> <see cref="Subtotal"/>,
/// <see cref="TaxTotal"/>, <see cref="Total"/>, <see cref="AmountPaid"/>,
/// and <see cref="Balance"/> are stored — write services recompute them
/// on each mutation. Caching avoids paying the rounding/aggregation cost
/// on every read and lets a Loro-replicated row carry its own consistent
/// view of the numbers.
/// </para>
/// </summary>
public sealed record Invoice
{
    /// <summary>Stable identifier.</summary>
    public required InvoiceId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The chart-of-accounts under which the invoice posts.</summary>
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>Customer-facing invoice number (e.g. <c>"INV-2026-05-17-AB-0001"</c>). PR 2 mints; PR 1 callers supply directly.</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>The Party holding the canonical <c>customer</c> role for this invoice.</summary>
    public required PartyId CustomerId { get; init; }

    /// <summary>Optional cost-center / property handle for per-property roll-ups.</summary>
    public string? PropertyId { get; init; }

    /// <summary>Date the invoice was issued (or planned to issue, for Drafts).</summary>
    public required DateOnly IssueDate { get; init; }

    /// <summary>Date the balance is due.</summary>
    public required DateOnly DueDate { get; init; }

    /// <summary>ISO 4217 currency code; defaults to USD.</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Line items in display order.</summary>
    public required IReadOnlyList<InvoiceLine> Lines { get; init; }

    /// <summary>Cached: sum of line amounts (pre-tax).</summary>
    public decimal Subtotal { get; init; }

    /// <summary>Cached: sum of per-line tax amounts.</summary>
    public decimal TaxTotal { get; init; }

    /// <summary>Cached: <see cref="Subtotal"/> + <see cref="TaxTotal"/>.</summary>
    public decimal Total { get; init; }

    /// <summary>Cached: cumulative payment applied. Bumped by the payments cluster (future hand-off).</summary>
    public decimal AmountPaid { get; init; }

    /// <summary>Cached: <see cref="Total"/> − <see cref="AmountPaid"/>.</summary>
    public decimal Balance { get; init; }

    /// <summary>Lifecycle state.</summary>
    public required InvoiceStatus Status { get; init; }

    /// <summary>The Asset/AR control-account this invoice debits.</summary>
    public required GLAccountId ArAccountId { get; init; }

    /// <summary>Optional free-text notes (printed on invoice).</summary>
    public string? Notes { get; init; }

    /// <summary>Optional payment-terms identifier (e.g., <c>"NET-30"</c>); resolved by future <c>ITermsService</c>.</summary>
    public string? TermsId { get; init; }

    /// <summary>Optional external-system reference (e.g., ERPNext <c>SINV-0001</c>) for migration / sync.</summary>
    public string? ExternalRef { get; init; }

    /// <summary>The Issue journal entry; null until <c>IInvoicePostingService.IssueAsync</c> runs (PR 3).</summary>
    public JournalEntryId? JournalEntryId { get; init; }

    /// <summary>The Void reversal journal entry; non-null only when <see cref="Status"/> = <see cref="InvoiceStatus.Voided"/>.</summary>
    public JournalEntryId? VoidedByEntryId { get; init; }

    /// <summary>The BadDebt write-off journal entry; non-null only when <see cref="Status"/> = <see cref="InvoiceStatus.WrittenOff"/>.</summary>
    public JournalEntryId? WrittenOffByEntryId { get; init; }

    // ── CRDT envelope ──
    public required Instant CreatedAtUtc { get; init; }
    public PartyId? CreatedBy { get; init; }
    public Instant UpdatedAtUtc { get; init; }
    public PartyId? UpdatedBy { get; init; }
    public Instant? DeletedAtUtc { get; init; }
    public PartyId? DeletedBy { get; init; }
    public string? DeletedReason { get; init; }
    public required long Version { get; init; }
    public IReadOnlyDictionary<string, long> RevisionVector { get; init; }
        = new Dictionary<string, long>();

    /// <summary>
    /// Construct a freshly-drafted invoice. Subtotal / TaxTotal / Total /
    /// Balance are materialized from <paramref name="lines"/> (sum + zero tax,
    /// since PR 1 lines don't yet carry computed tax — that's PR 3). The
    /// invoice starts in <see cref="InvoiceStatus.Draft"/>.
    /// </summary>
    public static Invoice Create(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        string invoiceNumber,
        PartyId customerId,
        DateOnly issueDate,
        DateOnly dueDate,
        IReadOnlyList<InvoiceLine> lines,
        GLAccountId arAccountId,
        PartyId? createdBy = null,
        InvoiceId? id = null,
        string? propertyId = null,
        string currency = "USD",
        string? notes = null,
        string? termsId = null,
        string? externalRef = null,
        Instant? createdAtUtc = null)
    {
        var now = createdAtUtc ?? Instant.Now;
        var subtotal = lines.Sum(l => l.Amount);
        var taxTotal = lines.Sum(l => l.TaxAmount);
        var total = subtotal + taxTotal;
        return new Invoice
        {
            Id = id ?? InvoiceId.NewId(),
            TenantId = tenantId,
            ChartId = chartId,
            InvoiceNumber = invoiceNumber,
            CustomerId = customerId,
            PropertyId = propertyId,
            IssueDate = issueDate,
            DueDate = dueDate,
            Currency = currency,
            Lines = lines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            AmountPaid = 0m,
            Balance = total,
            Status = InvoiceStatus.Draft,
            ArAccountId = arAccountId,
            Notes = notes,
            TermsId = termsId,
            ExternalRef = externalRef,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            Version = 1,
        };
    }

    /// <summary>True when the invoice is past <paramref name="asOf"/> with an open balance.</summary>
    public bool IsOverdueAsOf(DateOnly asOf) =>
        Status.IsOpen() && asOf > DueDate && Balance > 0m;
}
