namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Application data visible to <see cref="Services.IApplicationDecisioner"/>
/// — strictly non-protected-class fields. FHA-defense layout: this is the
/// ONLY shape that decisioning logic consumes. Protected-class data lives
/// in <see cref="DemographicProfile"/> and is structurally inaccessible to
/// any decisioning code path.
/// </summary>
/// <remarks>
/// Per ADR 0057: the structural quarantine is the defense, not policy +
/// audit. A reviewer can prove from the type signature alone that
/// decisioning code never reads protected-class data.
/// </remarks>
public sealed record DecisioningFacts
{
    /// <summary>Self-reported gross monthly income in the application's currency.</summary>
    public required decimal GrossMonthlyIncome { get; init; }

    /// <summary>Source of <see cref="GrossMonthlyIncome"/> (employer, self-employment, etc.) — non-protected.</summary>
    public required string IncomeSource { get; init; }

    /// <summary>Number of employed years at <see cref="IncomeSource"/>.</summary>
    public required int YearsAtIncomeSource { get; init; }

    /// <summary>Self-reported credit-score range (e.g., <c>720-780</c>); pulled from BG-check at decision time.</summary>
    public string? SelfReportedCreditRange { get; init; }

    /// <summary>Whether the applicant has a prior eviction on record (self-disclosed; verified by BG check).</summary>
    public required bool PriorEvictionDisclosed { get; init; }

    /// <summary>Number of references provided.</summary>
    public required int ReferenceCount { get; init; }

    /// <summary>Names of prior landlords for verification (not addresses; non-PII).</summary>
    public required IReadOnlyList<string> PriorLandlordNames { get; init; }

    /// <summary>Number of dependents to be housed in the unit (non-protected operational fact, not "familial status").</summary>
    public required int DependentCount { get; init; }

    /// <summary>Whether the applicant requested an accommodation (the request alone is not a protected-class indicator; the underlying basis is).</summary>
    public bool AccommodationRequested { get; init; }
}
