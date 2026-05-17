using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// Canonical bill — the AP-side payable document. Mirrors AR's
/// <c>Invoice</c> aggregate; differences come from the vendor side
/// of the ledger (single AP control account, vendor-supplied
/// <see cref="BillNumber"/>, optional approval gate, dispute hold).
///
/// <para>
/// <b>BillNumber is the vendor's own number</b>, not a Sunfish-
/// generated sequence. Uniqueness scope is
/// <c>(ChartId, VendorId, BillNumber)</c> — the same vendor can't
/// send us two bills with the same number on the same chart, but
/// different vendors can re-use a number like <c>"INV-001"</c>
/// without collision.
/// </para>
///
/// <para>
/// <b>Cached monetary totals.</b> Subtotal / TaxTotal / Total /
/// AmountPaid / Balance are stored and recomputed by the write
/// service on every mutation. Same rationale as AR's Invoice —
/// avoids paying the rounding/aggregation cost on every read.
/// </para>
/// </summary>
public sealed record Bill
{
    /// <summary>Stable identifier.</summary>
    public required BillId Id { get; init; }

    /// <summary>Tenant scope.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>The chart-of-accounts under which the bill posts.</summary>
    public required ChartOfAccountsId ChartId { get; init; }

    /// <summary>Vendor's own document number (e.g. <c>"VND-2026-001"</c>). Uniqueness: (ChartId, VendorId, BillNumber).</summary>
    public required string BillNumber { get; init; }

    /// <summary>The Party holding the canonical <c>vendor</c> role for this bill.</summary>
    public required PartyId VendorId { get; init; }

    /// <summary>Optional cost-center / property handle for per-property roll-ups.</summary>
    public string? PropertyId { get; init; }

    /// <summary>Date the vendor put on the bill.</summary>
    public required DateOnly BillDate { get; init; }

    /// <summary>Date the balance is due.</summary>
    public required DateOnly DueDate { get; init; }

    /// <summary>Date AP physically received the bill (may differ from <see cref="BillDate"/>).</summary>
    public required DateOnly ReceivedDate { get; init; }

    /// <summary>ISO 4217 currency code; defaults to USD.</summary>
    public string Currency { get; init; } = "USD";

    /// <summary>Line items in vendor-document order.</summary>
    public required IReadOnlyList<BillLine> Lines { get; init; }

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
    public required BillStatus Status { get; init; }

    /// <summary>The Liability/AP control-account this bill credits.</summary>
    public required GLAccountId ApAccountId { get; init; }

    /// <summary>Optional free-text notes (printed on bill review screens).</summary>
    public string? Notes { get; init; }

    /// <summary>Optional payment-terms identifier (e.g., <c>"NET-30"</c>); resolved by future <c>ITermsService</c>.</summary>
    public string? TermsId { get; init; }

    /// <summary>Optional external-system reference (e.g., ERPNext <c>PINV-0001</c>) for migration / sync.</summary>
    public string? ExternalRef { get; init; }

    /// <summary>The Record journal entry; null until <c>IBillPostingService.RecordAsync</c> runs (PR 2).</summary>
    public JournalEntryId? JournalEntryId { get; init; }

    /// <summary>The Void reversal journal entry; non-null only when <see cref="Status"/> = <see cref="BillStatus.Voided"/>.</summary>
    public JournalEntryId? VoidedByEntryId { get; init; }

    /// <summary>Opaque user id of the approver, if any.</summary>
    public string? ApprovedByUserId { get; init; }

    /// <summary>Approval timestamp.</summary>
    public Instant? ApprovedAtUtc { get; init; }

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
    /// Construct a freshly-drafted bill. Subtotal / TaxTotal / Total /
    /// Balance are materialized from <paramref name="lines"/> (sum + zero
    /// tax — tax computation lands in PR 2's posting service). The bill
    /// starts in <see cref="BillStatus.Draft"/>.
    /// </summary>
    public static Bill Create(
        TenantId tenantId,
        ChartOfAccountsId chartId,
        string billNumber,
        PartyId vendorId,
        DateOnly billDate,
        DateOnly dueDate,
        IReadOnlyList<BillLine> lines,
        GLAccountId apAccountId,
        PartyId? createdBy = null,
        BillId? id = null,
        string? propertyId = null,
        DateOnly? receivedDate = null,
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
        return new Bill
        {
            Id = id ?? BillId.NewId(),
            TenantId = tenantId,
            ChartId = chartId,
            BillNumber = billNumber,
            VendorId = vendorId,
            PropertyId = propertyId,
            BillDate = billDate,
            DueDate = dueDate,
            ReceivedDate = receivedDate ?? billDate,
            Currency = currency,
            Lines = lines,
            Subtotal = subtotal,
            TaxTotal = taxTotal,
            Total = total,
            AmountPaid = 0m,
            Balance = total,
            Status = BillStatus.Draft,
            ApAccountId = apAccountId,
            Notes = notes,
            TermsId = termsId,
            ExternalRef = externalRef,
            CreatedAtUtc = now,
            CreatedBy = createdBy,
            UpdatedAtUtc = now,
            Version = 1,
        };
    }

    /// <summary>True when the bill is past <paramref name="asOf"/> with an open balance. Disputed bills are NOT overdue (they're on hold).</summary>
    public bool IsOverdueAsOf(DateOnly asOf) =>
        Status.IsOpen() && asOf > DueDate && Balance > 0m;
}
