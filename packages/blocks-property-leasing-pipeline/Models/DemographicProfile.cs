namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Protected-class data captured for HUD reporting + civil-rights
/// compliance — collected separately from the application form, NEVER
/// passed to <see cref="Services.IApplicationDecisioner"/>, and
/// structurally absent from
/// <see cref="Application"/>'s decisioning pathway.
/// </summary>
/// <remarks>
/// <para>
/// FHA-defense layout per ADR 0057: this record is intentionally
/// quarantined. Reading any field requires a separate capability
/// (<c>IFieldDecryptor</c> in Phase 3) and emits an audit record. The
/// only legitimate read paths are: (1) HUD reporting export, (2) a
/// court-ordered audit, (3) the prospect themselves viewing their own
/// data via a self-service capability.
/// </para>
/// <para>
/// Storage-side: each field is per-tenant-key encrypted (ADR 0046
/// <c>EncryptedField</c>). The plaintext shape is shown here for the
/// API surface; on-disk + on-wire it is sealed.
/// </para>
/// </remarks>
public sealed record DemographicProfile
{
    /// <summary>Race / ethnicity per HUD reporting categories. Optional; prospect may decline to state.</summary>
    public string? RaceOrEthnicity { get; init; }

    /// <summary>National origin / country of birth. Optional.</summary>
    public string? NationalOrigin { get; init; }

    /// <summary>Religion. Optional.</summary>
    public string? Religion { get; init; }

    /// <summary>Sex / gender identity. Optional.</summary>
    public string? Sex { get; init; }

    /// <summary>Disability status (if disclosed for HUD reporting; not for decisioning).</summary>
    public string? DisabilityStatus { get; init; }

    /// <summary>Familial status (children in household; protected under FHA).</summary>
    public string? FamilialStatus { get; init; }

    /// <summary>Marital status. Optional.</summary>
    public string? MaritalStatus { get; init; }

    /// <summary>Source of income type (e.g., Section 8 voucher) — protected under some state/local laws.</summary>
    public string? IncomeSourceType { get; init; }
}
