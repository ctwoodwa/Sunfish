namespace Sunfish.Blocks.FinancialTax.Models;

/// <summary>
/// Which IRS / state tax form a <see cref="TaxFormLineMap"/> targets.
/// Per <c>blocks-reports-schema-design.md</c> §3. Stable string codes
/// per CRDT-conventions §5 — member names are append-only and serialize
/// to the canonical kebab-case strings via
/// <see cref="TaxFormKindExtensions.ToCanonical"/>.
/// </summary>
public enum TaxFormKind
{
    /// <summary>Form 1040 Schedule E (rental real estate). Canonical: <c>"schedule-e"</c>.</summary>
    ScheduleE,

    /// <summary>Nonemployee compensation report. Canonical: <c>"1099-nec"</c>.</summary>
    Form1099Nec,

    /// <summary>Miscellaneous income report. Canonical: <c>"1099-misc"</c>.</summary>
    Form1099Misc,

    /// <summary>Self-employed Schedule C. Canonical: <c>"schedule-c"</c>.</summary>
    ScheduleC,

    /// <summary>Partnership K-1 distribution. Canonical: <c>"form-1065-k1"</c>.</summary>
    Form1065K1,

    /// <summary>State-specific rental-property forms. Canonical: <c>"state-rental"</c>.</summary>
    StateRental,
}

/// <summary>
/// Bridge between the C# CamelCase enum members and the kebab-case
/// strings the Loro storage layer + TypeScript surface use as the
/// canonical wire format.
/// </summary>
public static class TaxFormKindExtensions
{
    /// <summary>
    /// Canonical kebab-case string for serialization + cross-language
    /// interop. Never throws — adding a new <see cref="TaxFormKind"/>
    /// member requires adding a case here (compile-time enforced via
    /// the catch-all throw).
    /// </summary>
    public static string ToCanonical(this TaxFormKind kind) => kind switch
    {
        TaxFormKind.ScheduleE    => "schedule-e",
        TaxFormKind.Form1099Nec  => "1099-nec",
        TaxFormKind.Form1099Misc => "1099-misc",
        TaxFormKind.ScheduleC    => "schedule-c",
        TaxFormKind.Form1065K1   => "form-1065-k1",
        TaxFormKind.StateRental  => "state-rental",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind,
            "Unknown TaxFormKind — add a canonical-string mapping when extending the enum."),
    };
}
