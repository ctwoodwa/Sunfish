using RcModels = global::Sunfish.Blocks.RentCollection.Models;
using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.RentCollection.Migration;

/// <summary>
/// Non-breaking bridge from the rent-collection domain (period-keyed,
/// schedule-driven) to the canonical AR domain (customer-keyed,
/// chart-of-accounts-driven). Existing
/// <c>IRentCollectionService</c> continues to work unchanged;
/// consumers that want the canonical AR shape (the rent-roll UI, the
/// aging service, dashboards) call <see cref="ToCanonicalAr"/> on
/// demand.
///
/// <para>
/// <b>Full service-delegation flip is deferred</b> per the
/// Decide-as-Late-as-Possible principle. Replacing
/// <c>IRentCollectionService.GenerateInvoiceAsync</c> with a call to
/// <see cref="Sunfish.Blocks.FinancialAr.Services.IInvoicePostingService.IssueAsync"/>
/// requires a lease→tenant→<see cref="PartyId"/> lookup contract that
/// isn't yet defined cross-cluster. This projection adapter unblocks
/// the read path immediately; the write-path retrofit ships when the
/// lookup contract lands.
/// </para>
///
/// <para>
/// <b>What the rent invoice doesn't carry</b> (and the adapter
/// requires from the caller): the canonical customer
/// <see cref="PartyId"/>, the <see cref="ChartOfAccountsId"/>, the AR
/// control <see cref="GLAccountId"/>, the rent-income
/// <see cref="GLAccountId"/>, and a canonical invoice number. The
/// caller is expected to derive these from its own context (typically
/// from the lease + chart-config + numbering service).
/// </para>
/// </summary>
public static class ArInvoiceProjection
{
    /// <summary>
    /// Project a rent-collection invoice into the canonical AR shape.
    /// The mapping is total — every rent-invoice field has a defined
    /// target — and conservative — no fields are invented beyond what
    /// the caller supplies.
    /// </summary>
    /// <param name="rentInvoice">The source rent-collection invoice.</param>
    /// <param name="tenantId">Multi-tenant scope.</param>
    /// <param name="chartId">Chart-of-accounts this invoice posts against.</param>
    /// <param name="customerId">Party holding the customer/tenant role (caller derives from lease).</param>
    /// <param name="arAccountId">AR control account.</param>
    /// <param name="rentIncomeAccountId">Income account the line credits (typically "Rental Income").</param>
    /// <param name="invoiceNumber">
    /// Canonical-format invoice number. Caller mints via
    /// <see cref="Sunfish.Blocks.FinancialAr.Services.IInvoiceNumberingService"/>
    /// when transitioning the projection past Draft; empty string is
    /// acceptable when the projection is Draft-status.
    /// </param>
    /// <param name="propertyId">Optional cost-center / property handle for per-property roll-ups.</param>
    public static Invoice ToCanonicalAr(
        RcModels.Invoice rentInvoice,
        TenantId tenantId,
        ChartOfAccountsId chartId,
        PartyId customerId,
        GLAccountId arAccountId,
        GLAccountId rentIncomeAccountId,
        string invoiceNumber,
        string? propertyId = null)
    {
        if (rentInvoice is null) throw new ArgumentNullException(nameof(rentInvoice));

        var invoiceId = InvoiceId.NewId();
        var line = InvoiceLine.Create(
            invoiceId: invoiceId,
            lineNumber: 1,
            description: $"Rent — {rentInvoice.PeriodStart:yyyy-MM-dd} to {rentInvoice.PeriodEnd:yyyy-MM-dd}",
            quantity: 1m,
            unitPrice: rentInvoice.AmountDue,
            incomeAccountId: rentIncomeAccountId,
            propertyId: propertyId);

        var issueDate = DateOnly.FromDateTime(rentInvoice.GeneratedAtUtc.Value.UtcDateTime);

        var canonical = Invoice.Create(
            tenantId: tenantId,
            chartId: chartId,
            invoiceNumber: invoiceNumber,
            customerId: customerId,
            issueDate: issueDate,
            dueDate: rentInvoice.DueDate,
            lines: new[] { line },
            arAccountId: arAccountId,
            id: invoiceId,
            propertyId: propertyId,
            externalRef: $"rent-invoice:{rentInvoice.Id.Value}",
            createdAtUtc: rentInvoice.GeneratedAtUtc);

        // Map status + payment fields. The rent-collection model carries
        // `Overdue` as a stored status, which canonical AR derives — flatten
        // back to `Issued` so the canonical caller can re-derive via
        // `Invoice.IsOverdueAsOf(today)`.
        var canonicalStatus = MapStatus(rentInvoice.Status);
        var balance = rentInvoice.AmountDue - rentInvoice.AmountPaid;

        return canonical with
        {
            Status = canonicalStatus,
            AmountPaid = rentInvoice.AmountPaid,
            Balance = balance < 0m ? 0m : balance,
        };
    }

    /// <summary>
    /// Map rent-collection status codes to canonical AR. Documented above the
    /// switch so consumers can audit the policy without reading the body.
    ///
    /// <list type="bullet">
    /// <item><c>Draft → Draft</c></item>
    /// <item><c>Open → Issued</c> (canonical AR has no "Open" — Issued is the equivalent)</item>
    /// <item><c>PartiallyPaid → PartiallyPaid</c></item>
    /// <item><c>Paid → Paid</c></item>
    /// <item><c>Overdue → Issued</c> (canonical derives Overdue from DueDate at read-time, not as stored state)</item>
    /// <item><c>Cancelled → Voided</c> (both reverse the invoice; closest semantic match)</item>
    /// </list>
    /// </summary>
    public static InvoiceStatus MapStatus(RcModels.InvoiceStatus rentStatus) =>
        rentStatus switch
        {
            RcModels.InvoiceStatus.Draft         => InvoiceStatus.Draft,
            RcModels.InvoiceStatus.Open          => InvoiceStatus.Issued,
            RcModels.InvoiceStatus.PartiallyPaid => InvoiceStatus.PartiallyPaid,
            RcModels.InvoiceStatus.Paid          => InvoiceStatus.Paid,
            RcModels.InvoiceStatus.Overdue       => InvoiceStatus.Issued,
            RcModels.InvoiceStatus.Cancelled     => InvoiceStatus.Voided,
            _ => throw new ArgumentOutOfRangeException(nameof(rentStatus), rentStatus, "Unknown rent-collection InvoiceStatus."),
        };
}
