using Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// Orchestrates the end-to-end leasing pipeline (Inquiry → Prospect →
/// Application → Decision → LeaseOffer) per ADR 0057. Phase 2 ships the
/// state machine + capability promotion contract; Phase 3 wires the
/// FCRA workflow + AdverseActionNotice issuance.
/// </summary>
public interface ILeasingPipelineService
{
    /// <summary>
    /// Promotes an <see cref="Inquiry"/> to a <see cref="Prospect"/>
    /// after the email-verification flow succeeds. Inquiry must be in
    /// <see cref="InquiryStatus.Submitted"/>; result is the persisted
    /// prospect + the macaroon-backed <c>ProspectCapability</c>.
    /// </summary>
    Task<Prospect> PromoteInquiryToProspectAsync(
        InquiryId inquiryId,
        string verifiedEmail,
        CancellationToken ct);

    /// <summary>
    /// Submits an application from a <see cref="Prospect"/>. Application
    /// is created in <see cref="ApplicationStatus.Submitted"/>.
    /// </summary>
    Task<Application> SubmitApplicationAsync(
        SubmitApplicationRequest request,
        CancellationToken ct);

    /// <summary>
    /// Transitions an <see cref="Application"/> from
    /// <see cref="ApplicationStatus.Submitted"/> →
    /// <see cref="ApplicationStatus.AwaitingBackgroundCheck"/> after
    /// payment + signature confirmation. Mints an
    /// <see cref="ApplicantCapability"/> on success.
    /// </summary>
    Task<ApplicantCapability> ConfirmApplicationAndPromoteAsync(
        ApplicationId applicationId,
        ActorId confirmedBy,
        CancellationToken ct);

    /// <summary>
    /// Records a background-check result + transitions the application
    /// from <see cref="ApplicationStatus.AwaitingBackgroundCheck"/> →
    /// <see cref="ApplicationStatus.AwaitingDecision"/>.
    /// </summary>
    Task<Application> RecordBackgroundCheckAsync(
        ApplicationId applicationId,
        BackgroundCheckResult result,
        CancellationToken ct);

    /// <summary>
    /// Records the operator's decision. Transitions the application
    /// from <see cref="ApplicationStatus.AwaitingDecision"/> →
    /// <see cref="ApplicationStatus.Accepted"/> or
    /// <see cref="ApplicationStatus.Declined"/>. Phase 3 hooks in
    /// <c>AdverseActionNotice</c> issuance for declines that cite a
    /// BG-check finding.
    /// </summary>
    Task<Application> RecordDecisionAsync(
        ApplicationId applicationId,
        ApplicationDecision decision,
        CancellationToken ct);

    /// <summary>
    /// Withdraws an application before the operator decides. Allowed
    /// from any non-terminal status.
    /// </summary>
    Task<Application> WithdrawApplicationAsync(
        ApplicationId applicationId,
        ActorId withdrawnBy,
        CancellationToken ct);

    /// <summary>Returns the application with the given id, or <see langword="null"/> if no such record exists.</summary>
    Task<Application?> GetApplicationAsync(ApplicationId id, CancellationToken ct);

    /// <summary>Returns the prospect with the given id, or <see langword="null"/> if no such record exists.</summary>
    Task<Prospect?> GetProspectAsync(ProspectId id, CancellationToken ct);

    /// <summary>Returns the inquiry with the given id, or <see langword="null"/> if no such record exists.</summary>
    Task<Inquiry?> GetInquiryAsync(InquiryId id, CancellationToken ct);
}

/// <summary>Submission shape for <see cref="ILeasingPipelineService.SubmitApplicationAsync"/>.</summary>
public sealed record SubmitApplicationRequest
{
    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The prospect submitting the application.</summary>
    public required ProspectId Prospect { get; init; }

    /// <summary>The listing being applied for.</summary>
    public required Sunfish.Blocks.PublicListings.Models.PublicListingId Listing { get; init; }

    /// <summary>Non-protected-class fields visible to decisioning.</summary>
    public required DecisioningFacts Facts { get; init; }

    /// <summary>Protected-class fields quarantined for HUD reporting.</summary>
    public required DemographicProfile Demographics { get; init; }

    /// <summary>Application fee per ADR 0051.</summary>
    public required Sunfish.Foundation.Integrations.Payments.Money ApplicationFee { get; init; }

    /// <summary>Application signature reference (ADR 0054).</summary>
    public required Sunfish.Foundation.Integrations.Signatures.SignatureEventRef Signature { get; init; }
}
