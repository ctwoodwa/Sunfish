using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TaxReporting.Models;

/// <summary>
/// Per-property income and expense data for IRS Schedule E (§199A-aligned).
/// All monetary fields are <see cref="decimal"/>; no floating-point types are used.
/// </summary>
/// <param name="PropertyId">Reference to the property entity.</param>
/// <param name="Address">Human-readable address string for display on the form.</param>
/// <param name="RentsReceived">Total rental receipts for the tax year.</param>
/// <param name="MortgageInterest">Deductible mortgage interest paid.</param>
/// <param name="Taxes">Real estate taxes paid.</param>
/// <param name="Insurance">Insurance premiums paid.</param>
/// <param name="Repairs">Repair and maintenance expenses.</param>
/// <param name="Depreciation">Depreciation deduction (from depreciation schedule).</param>
/// <param name="OtherExpenses">All other deductible expenses not listed above.</param>
public sealed record SchedulePropertyRow(
    EntityId PropertyId,
    string Address,
    decimal RentsReceived,
    decimal MortgageInterest,
    decimal Taxes,
    decimal Insurance,
    decimal Repairs,
    decimal Depreciation,
    decimal OtherExpenses)
{
    /// <summary>
    /// Total deductible expenses for this property row.
    /// </summary>
    public decimal TotalExpenses =>
        MortgageInterest + Taxes + Insurance + Repairs + Depreciation + OtherExpenses;

    /// <summary>
    /// Net income (positive) or loss (negative) for this property row.
    /// </summary>
    public decimal NetIncomeOrLoss => RentsReceived - TotalExpenses;
}
