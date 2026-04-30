using System.Collections.Concurrent;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Services;

/// <summary>
/// In-memory <see cref="ILeasingPipelineService"/> +
/// <see cref="IPublicInquiryService"/>. Persists to
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>; not for production.
/// W#22 Phase 2: state machine (via
/// <see cref="TransitionTable{TState}"/> from blocks-maintenance) +
/// capability promotion shape. Phase 3 wires FCRA workflow + audit.
/// </summary>
public sealed class InMemoryLeasingPipelineService : ILeasingPipelineService, IPublicInquiryService
{
    private readonly ConcurrentDictionary<InquiryId, Inquiry> _inquiries = new();
    private readonly ConcurrentDictionary<ProspectId, Prospect> _prospects = new();
    private readonly ConcurrentDictionary<ApplicationId, Application> _applications = new();

    private readonly ICapabilityPromoter? _prospectPromoter;
    private readonly TimeProvider _time;

    private static readonly TransitionTable<InquiryStatus> InquiryTransitions = new(
    [
        (InquiryStatus.Submitted,          [InquiryStatus.PromotedToProspect, InquiryStatus.Withdrawn]),
        // Terminal: PromotedToProspect (record stays for audit), Withdrawn.
    ]);

    private static readonly TransitionTable<ApplicationStatus> ApplicationTransitions = new(
    [
        (ApplicationStatus.Submitted,                [ApplicationStatus.AwaitingBackgroundCheck, ApplicationStatus.Withdrawn]),
        (ApplicationStatus.AwaitingBackgroundCheck,  [ApplicationStatus.AwaitingDecision, ApplicationStatus.Withdrawn]),
        (ApplicationStatus.AwaitingDecision,         [ApplicationStatus.Accepted, ApplicationStatus.Declined, ApplicationStatus.Withdrawn]),
        // Terminal: Accepted, Declined, Withdrawn.
    ]);

    /// <summary>Creates the service with capability-promotion + audit hooks disabled.</summary>
    public InMemoryLeasingPipelineService() : this(prospectPromoter: null, time: null) { }

    /// <summary>Creates the service with optional capability-promotion wiring.</summary>
    public InMemoryLeasingPipelineService(ICapabilityPromoter? prospectPromoter, TimeProvider? time)
    {
        _prospectPromoter = prospectPromoter;
        _time = time ?? TimeProvider.System;
    }

    // ── IPublicInquiryService ─────────────────────────────────────────────

    /// <inheritdoc />
    public Task<Inquiry> SubmitInquiryAsync(
        PublicInquiryRequest request,
        AnonymousCapability capability,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capability);
        ct.ThrowIfCancellationRequested();

        var inquiry = new Inquiry
        {
            Id = new InquiryId(Guid.NewGuid()),
            Tenant = request.Tenant,
            Listing = request.Listing,
            ProspectName = request.ProspectName,
            ProspectEmail = request.ProspectEmail,
            ProspectPhone = request.ProspectPhone,
            MessageBody = request.MessageBody,
            ClientIp = request.ClientIp,
            UserAgent = request.UserAgent,
            SubmittedAt = _time.GetUtcNow(),
            Status = InquiryStatus.Submitted,
        };

        _inquiries[inquiry.Id] = inquiry;
        return Task.FromResult(inquiry);
    }

    // ── ILeasingPipelineService ───────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Prospect> PromoteInquiryToProspectAsync(
        InquiryId inquiryId,
        string verifiedEmail,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(verifiedEmail);
        ct.ThrowIfCancellationRequested();

        if (!_inquiries.TryGetValue(inquiryId, out var inquiry))
        {
            throw new InvalidOperationException($"Inquiry '{inquiryId.Value}' not found.");
        }

        InquiryTransitions.Guard(inquiry.Status, InquiryStatus.PromotedToProspect, $"Inquiry '{inquiryId.Value}'");

        if (_prospectPromoter is null)
        {
            throw new InvalidOperationException("Prospect promotion requires an ICapabilityPromoter; pass one via the constructor.");
        }

        var capability = await _prospectPromoter.PromoteToProspectAsync(verifiedEmail, inquiry.ClientIp, ct).ConfigureAwait(false);

        var prospect = new Prospect
        {
            Id = new ProspectId(Guid.NewGuid()),
            Tenant = inquiry.Tenant,
            OriginatingInquiry = inquiryId,
            VerifiedEmail = verifiedEmail,
            Capability = capability,
            PromotedAt = _time.GetUtcNow(),
        };
        _prospects[prospect.Id] = prospect;

        _inquiries[inquiryId] = inquiry with { Status = InquiryStatus.PromotedToProspect };

        return prospect;
    }

    /// <inheritdoc />
    public Task<Application> SubmitApplicationAsync(
        SubmitApplicationRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (!_prospects.ContainsKey(request.Prospect))
        {
            throw new InvalidOperationException($"Prospect '{request.Prospect.Value}' not found.");
        }

        var application = new Application
        {
            Id = new ApplicationId(Guid.NewGuid()),
            Tenant = request.Tenant,
            Prospect = request.Prospect,
            Listing = request.Listing,
            Facts = request.Facts,
            Demographics = request.Demographics,
            Status = ApplicationStatus.Submitted,
            ApplicationSignature = request.Signature,
            ApplicationFee = request.ApplicationFee,
            SubmittedAt = _time.GetUtcNow(),
        };

        _applications[application.Id] = application;
        return Task.FromResult(application);
    }

    /// <inheritdoc />
    public Task<ApplicantCapability> ConfirmApplicationAndPromoteAsync(
        ApplicationId applicationId,
        ActorId confirmedBy,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var application = TransitionApplication(applicationId, ApplicationStatus.AwaitingBackgroundCheck);

        var issuedAt = _time.GetUtcNow();
        var capability = new ApplicantCapability
        {
            Id = new ApplicantCapabilityId(Guid.NewGuid()),
            // Phase 2 ships the shape; the actual macaroon-mint wires in Phase 3 once the
            // applicant-tier root key is registered. This token is a deterministic placeholder.
            Token = $"applicant-pending:{application.Id.Value:D}",
            Application = application.Id,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt.AddDays(30),
        };

        return Task.FromResult(capability);
    }

    /// <inheritdoc />
    public Task<Application> RecordBackgroundCheckAsync(
        ApplicationId applicationId,
        BackgroundCheckResult result,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        ct.ThrowIfCancellationRequested();
        var application = TransitionApplication(applicationId, ApplicationStatus.AwaitingDecision);
        return Task.FromResult(application);
    }

    /// <inheritdoc />
    public Task<Application> RecordDecisionAsync(
        ApplicationId applicationId,
        ApplicationDecision decision,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ct.ThrowIfCancellationRequested();

        var newStatus = decision.Accepted ? ApplicationStatus.Accepted : ApplicationStatus.Declined;
        var application = TransitionApplication(applicationId, newStatus, decided: true, decidedBy: decision.DecidedBy);
        return Task.FromResult(application);
    }

    /// <inheritdoc />
    public Task<Application> WithdrawApplicationAsync(
        ApplicationId applicationId,
        ActorId withdrawnBy,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var application = TransitionApplication(applicationId, ApplicationStatus.Withdrawn, decided: true, decidedBy: withdrawnBy);
        return Task.FromResult(application);
    }

    /// <inheritdoc />
    public Task<Application?> GetApplicationAsync(ApplicationId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _applications.TryGetValue(id, out var app);
        return Task.FromResult<Application?>(app);
    }

    /// <inheritdoc />
    public Task<Prospect?> GetProspectAsync(ProspectId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _prospects.TryGetValue(id, out var p);
        return Task.FromResult<Prospect?>(p);
    }

    /// <inheritdoc />
    public Task<Inquiry?> GetInquiryAsync(InquiryId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _inquiries.TryGetValue(id, out var i);
        return Task.FromResult<Inquiry?>(i);
    }

    private Application TransitionApplication(
        ApplicationId id,
        ApplicationStatus newStatus,
        bool decided = false,
        ActorId? decidedBy = null)
    {
        if (!_applications.TryGetValue(id, out var application))
        {
            throw new InvalidOperationException($"Application '{id.Value}' not found.");
        }
        ApplicationTransitions.Guard(application.Status, newStatus, $"Application '{id.Value}'");

        var updated = decided
            ? application with { Status = newStatus, DecidedAt = _time.GetUtcNow(), DecidedBy = decidedBy }
            : application with { Status = newStatus };
        _applications[id] = updated;
        return updated;
    }
}
