using System.Text;
using Sunfish.Blocks.TaxReporting.Models;

namespace Sunfish.Blocks.TaxReporting.Rendering;

/// <summary>
/// Default implementation of <see cref="ITaxReportTextRenderer"/>.
/// Produces a simple plain-text layout with tab-aligned columns.
/// </summary>
public sealed class TaxReportTextRenderer : ITaxReportTextRenderer
{
    /// <inheritdoc />
    public string Render(TaxReport report)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"TAX REPORT");
        sb.AppendLine($"ID:     {report.Id}");
        sb.AppendLine($"Year:   {report.Year}");
        sb.AppendLine($"Kind:   {report.Kind}");
        sb.AppendLine($"Status: {report.Status}");
        if (report.PropertyId is { } pid)
            sb.AppendLine($"Property: {pid}");
        sb.AppendLine($"Generated: {report.GeneratedAtUtc}");
        if (report.SignatureValue is not null)
            sb.AppendLine($"SHA-256: {report.SignatureValue}");
        sb.AppendLine(new string('-', 72));

        switch (report.Body)
        {
            case ScheduleEBody body:
                RenderScheduleE(sb, body);
                break;

            case Form1099NecBody body:
                RenderForm1099Nec(sb, body);
                break;

            case StatePersonalPropertyBody body:
                RenderStatePersonalProperty(sb, body);
                break;

            default:
                sb.AppendLine($"[Unrecognized body kind: {report.Body.Kind}]");
                break;
        }

        return sb.ToString();
    }

    // -----------------------------------------------------------------------
    // Per-body renderers
    // -----------------------------------------------------------------------

    private static void RenderScheduleE(StringBuilder sb, ScheduleEBody body)
    {
        sb.AppendLine("SCHEDULE E — Supplemental Income and Loss");
        sb.AppendLine();

        const string header = "{0,-36} {1,12} {2,12} {3,12} {4,12} {5,12} {6,12} {7,12} {8,12}";
        const string row    = "{0,-36} {1,12:C} {2,12:C} {3,12:C} {4,12:C} {5,12:C} {6,12:C} {7,12:C} {8,12:C}";

        sb.AppendLine(string.Format(header,
            "Address",
            "Rents",
            "Mortgage",
            "Taxes",
            "Insurance",
            "Repairs",
            "Deprec.",
            "Other",
            "Net"));
        sb.AppendLine(new string('-', 120));

        foreach (var prop in body.Properties)
        {
            sb.AppendLine(string.Format(row,
                Truncate(prop.Address, 36),
                prop.RentsReceived,
                prop.MortgageInterest,
                prop.Taxes,
                prop.Insurance,
                prop.Repairs,
                prop.Depreciation,
                prop.OtherExpenses,
                prop.NetIncomeOrLoss));
        }

        sb.AppendLine(new string('=', 120));
        sb.AppendLine(string.Format(row,
            "TOTALS",
            body.TotalRents,
            body.Properties.Sum(p => p.MortgageInterest),
            body.Properties.Sum(p => p.Taxes),
            body.Properties.Sum(p => p.Insurance),
            body.Properties.Sum(p => p.Repairs),
            body.Properties.Sum(p => p.Depreciation),
            body.Properties.Sum(p => p.OtherExpenses),
            body.NetIncomeOrLoss));
    }

    private static void RenderForm1099Nec(StringBuilder sb, Form1099NecBody body)
    {
        sb.AppendLine("FORM 1099-NEC — Nonemployee Compensation");
        sb.AppendLine();

        if (body.Recipients.Count == 0)
        {
            sb.AppendLine("  (No recipients meet the $600 IRS reporting threshold.)");
            return;
        }

        for (int i = 0; i < body.Recipients.Count; i++)
        {
            var r = body.Recipients[i];
            sb.AppendLine($"  Recipient {i + 1}:");
            sb.AppendLine($"    Name:    {r.RecipientName}");
            sb.AppendLine($"    TIN:     {r.RecipientTaxId}");
            sb.AppendLine($"    Address: {r.RecipientAddress}");
            sb.AppendLine($"    Amount:  {r.TotalPaid:C}");
            if (r.AccountNumber is not null)
                sb.AppendLine($"    Account: {r.AccountNumber}");
            if (i < body.Recipients.Count - 1)
                sb.AppendLine();
        }
    }

    private static void RenderStatePersonalProperty(StringBuilder sb, StatePersonalPropertyBody body)
    {
        sb.AppendLine($"STATE PERSONAL PROPERTY — {body.StateCode}");
        sb.AppendLine();
        sb.AppendLine("NOTE: Per-state form templates are deferred. This is a schema-only listing.");
        sb.AppendLine();

        if (body.Items.Count == 0)
        {
            sb.AppendLine("  (No personal-property items.)");
            return;
        }

        const string header = "{0,-40} {1,6} {2,14} {3,14}";
        const string row    = "{0,-40} {1,6} {2,14:C} {3,14:C}";

        sb.AppendLine(string.Format(header, "Description", "Yr", "Original Cost", "Reported Value"));
        sb.AppendLine(new string('-', 78));

        foreach (var item in body.Items)
        {
            sb.AppendLine(string.Format(row,
                Truncate(item.Description, 40),
                item.AcquisitionYear,
                item.OriginalCost,
                item.ReportedValue));
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
}
