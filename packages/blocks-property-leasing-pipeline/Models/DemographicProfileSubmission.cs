namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Plaintext-shaped wire form for protected-class data — collected at
/// form submission, encrypted at the
/// <see cref="Services.ILeasingPipelineService.SubmitApplicationAsync"/>
/// boundary, and discarded. W#22 Phase 9 per the post-W#32 addendum.
/// </summary>
/// <remarks>
/// <para>
/// This record exists to keep plaintext from crossing the persistence
/// boundary. The Bridge inquiry / start-application surfaces accept
/// this record from form input; the leasing-pipeline service consumes
/// it, encrypts every field via
/// <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor"/>
/// (purpose label <c>encrypted-field-aes</c>; per-tenant DEK), and
/// emits a <see cref="DemographicProfile"/> for storage. After that
/// boundary, plaintext is not retained.
/// </para>
/// <para>
/// All fields are nullable — a Prospect may decline to disclose. Null
/// in the submission becomes null in the encrypted record (NOT an
/// encrypted-empty value). Decisioning code paths never see this
/// record nor the encrypted projection.
/// </para>
/// </remarks>
public sealed record DemographicProfileSubmission
{
    public string? RaceOrEthnicity { get; init; }
    public string? NationalOrigin { get; init; }
    public string? Religion { get; init; }
    public string? Sex { get; init; }
    public string? DisabilityStatus { get; init; }
    public string? FamilialStatus { get; init; }
    public string? MaritalStatus { get; init; }
    public string? IncomeSourceType { get; init; }
}
