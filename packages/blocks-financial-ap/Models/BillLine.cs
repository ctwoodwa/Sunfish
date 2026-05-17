using Sunfish.Blocks.FinancialLedger.Models;

namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// One line of a <see cref="Bill"/>. Each line debits a single GL
/// account — typically Expense or Asset for purchase-of-inventory.
/// Tax (when applicable) is computed on the line via the tax cluster's
/// calculation engine; the <see cref="TaxCodeId"/> here is the opaque
/// string FK, stored without taking a hard dep on
/// <c>Sunfish.Blocks.FinancialTax</c>.
///
/// <para>
/// <b>Amount derivation:</b> <see cref="Amount"/> is the rounded product
/// <c>Quantity * UnitPrice</c> using banker's rounding to two minor
/// units. Materialized on Create so audit reports show the exact figure
/// the vendor charged.
/// </para>
/// </summary>
public sealed record BillLine
{
    /// <summary>Stable identifier.</summary>
    public required BillLineId Id { get; init; }

    /// <summary>The owning bill.</summary>
    public required BillId BillId { get; init; }

    /// <summary>One-based position; preserves vendor-document order.</summary>
    public required int LineNumber { get; init; }

    /// <summary>Free-text description (rendered on bill review screens).</summary>
    public required string Description { get; init; }

    /// <summary>Quantity (decimal — fractional units allowed).</summary>
    public required decimal Quantity { get; init; }

    /// <summary>Unit price in bill currency.</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>Computed: <c>banker's-round(Quantity * UnitPrice, 2)</c>.</summary>
    public required decimal Amount { get; init; }

    /// <summary>The GL account this line debits — typically Expense or Asset subtype.</summary>
    public required GLAccountId DebitAccountId { get; init; }

    /// <summary>Opaque FK into the tax cluster's <c>TaxCode</c>. Null = no tax.</summary>
    public string? TaxCodeId { get; init; }

    /// <summary>Computed tax at posting time; zero until <c>IBillPostingService.RecordAsync</c> resolves it (PR 2).</summary>
    public decimal TaxAmount { get; init; }

    /// <summary>Optional cost-center / property handle for per-property roll-ups.</summary>
    public string? PropertyId { get; init; }

    /// <summary>Optional Schedule E / form-line classification code.</summary>
    public string? ClassificationId { get; init; }

    /// <summary>Optional per-line notes (not rendered on the bill by default).</summary>
    public string? Notes { get; init; }

    /// <summary>Construct a bill line with the materialized amount computed for the caller.</summary>
    public static BillLine Create(
        BillId billId,
        int lineNumber,
        string description,
        decimal quantity,
        decimal unitPrice,
        GLAccountId debitAccountId,
        string? taxCodeId = null,
        string? propertyId = null,
        string? classificationId = null,
        string? notes = null,
        BillLineId? id = null)
    {
        var amount = decimal.Round(quantity * unitPrice, 2, MidpointRounding.ToEven);
        return new BillLine
        {
            Id = id ?? BillLineId.NewId(),
            BillId = billId,
            LineNumber = lineNumber,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
            DebitAccountId = debitAccountId,
            TaxCodeId = taxCodeId,
            PropertyId = propertyId,
            ClassificationId = classificationId,
            Notes = notes,
        };
    }
}
