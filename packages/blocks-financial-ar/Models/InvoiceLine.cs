using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// One line of an <see cref="Invoice"/>. Each line credits a single
/// income-side GL account; tax (when applicable) is computed on the line
/// via <c>ITaxCalculationService</c> from the tax cluster — the
/// <see cref="TaxCodeId"/> here is the opaque-string foreign key,
/// stored without taking a hard dep on
/// <c>Sunfish.Blocks.FinancialTax</c>.
///
/// <para>
/// <b>Amount derivation:</b> <see cref="Amount"/> is the rounded product
/// <c>Quantity × UnitPrice</c> using banker's rounding to two minor units.
/// The repository stores the materialized value rather than recomputing
/// on read so audit reports show the exact figure the customer saw.
/// </para>
/// </summary>
public sealed record InvoiceLine
{
    /// <summary>Stable identifier.</summary>
    public required InvoiceLineId Id { get; init; }

    /// <summary>The owning invoice.</summary>
    public required InvoiceId InvoiceId { get; init; }

    /// <summary>One-based position; preserves the order the customer sees.</summary>
    public required int LineNumber { get; init; }

    /// <summary>Free-text description rendered on the invoice.</summary>
    public required string Description { get; init; }

    /// <summary>Quantity (decimal — fractional hours / units are allowed).</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Unit price in invoice currency, major units.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Computed: <c>banker's-round(Quantity * UnitPrice, 2)</c>.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Credit account for this line — typically an Income / OperatingIncome subtype.</summary>
    public required GLAccountId IncomeAccountId { get; init; }

    /// <summary>Opaque FK into the tax cluster's <c>TaxCode</c>. Null = no tax.</summary>
    public string? TaxCodeId { get; init; }

    /// <summary>Computed tax on this line at posting time; zero until <c>IInvoicePostingService.IssueAsync</c> resolves it.</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Optional cost-center / property handle for per-property roll-ups.</summary>
    public string? PropertyId { get; init; }

    /// <summary>Optional Schedule E / form-line classification code.</summary>
    public string? ClassificationId { get; init; }

    /// <summary>Optional per-line notes (not rendered on the invoice by default).</summary>
    public string? Notes { get; init; }

    /// <summary>Construct an invoice line with the materialized amount computed for the caller.</summary>
    public static InvoiceLine Create(
        InvoiceId invoiceId,
        int lineNumber,
        string description,
        decimal quantity,
        decimal unitPrice,
        GLAccountId incomeAccountId,
        string? taxCodeId = null,
        string? propertyId = null,
        string? classificationId = null,
        string? notes = null,
        InvoiceLineId? id = null)
    {
        var amount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.ToEven);
        return new InvoiceLine
        {
            Id = id ?? InvoiceLineId.NewId(),
            InvoiceId = invoiceId,
            LineNumber = lineNumber,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
            IncomeAccountId = incomeAccountId,
            TaxCodeId = taxCodeId,
            PropertyId = propertyId,
            ClassificationId = classificationId,
            Notes = notes,
        };
    }
}
