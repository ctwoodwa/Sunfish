using Sunfish.Foundation.Recovery;

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
/// quarantined. <b>W#22 Phase 9 / W#32 substrate:</b> every protected-
/// class field is now stored as an
/// <see cref="Sunfish.Foundation.Recovery.EncryptedField"/>; reading
/// requires an
/// <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor"/>
/// capability and emits a <c>FieldDecrypted</c> audit per W#32 / ADR 0046.
/// The "structurally inaccessible to decisioning" claim from ADR 0057
/// is enforced by the type system — decisioning code paths receive only
/// <see cref="DecisioningFacts"/> and have no access to a decrypt
/// capability.
/// </para>
/// <para>
/// The plaintext input shape lives in
/// <see cref="DemographicProfileSubmission"/>; a submission becomes a
/// <see cref="DemographicProfile"/> (encrypted) at the
/// <see cref="Services.ILeasingPipelineService.SubmitApplicationAsync"/>
/// boundary. Plaintext never persists.
/// </para>
/// <para>
/// The only legitimate read paths are: (1) HUD aggregated-stats export
/// (compliance-scoped capability), (2) court-ordered audit, (3) the
/// prospect themselves viewing their own data via a self-service /
/// SAR-scoped capability per FCRA §609 / GDPR / CCPA.
/// </para>
/// </remarks>
public sealed record DemographicProfile
{
    /// <summary>Race / ethnicity per HUD reporting categories. Optional; prospect may decline to state.</summary>
    public EncryptedField? RaceOrEthnicity { get; init; }

    /// <summary>National origin / country of birth. Optional.</summary>
    public EncryptedField? NationalOrigin { get; init; }

    /// <summary>Religion. Optional.</summary>
    public EncryptedField? Religion { get; init; }

    /// <summary>Sex / gender identity. Optional.</summary>
    public EncryptedField? Sex { get; init; }

    /// <summary>Disability status (if disclosed for HUD reporting; not for decisioning).</summary>
    public EncryptedField? DisabilityStatus { get; init; }

    /// <summary>Familial status (children in household; protected under FHA).</summary>
    public EncryptedField? FamilialStatus { get; init; }

    /// <summary>Marital status. Optional.</summary>
    public EncryptedField? MaritalStatus { get; init; }

    /// <summary>Source of income type (e.g., Section 8 voucher) — protected under some state/local laws.</summary>
    public EncryptedField? IncomeSourceType { get; init; }
}
