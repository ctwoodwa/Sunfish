using System.Collections.Concurrent;
using System.Collections.Immutable;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Blocks.PropertyLeasingPipeline.Audit;
using Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Blocks.PublicListings.Capabilities;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;
using Sunfish.Kernel.Audit;

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
    private readonly IInquiryValidator? _inquiryValidator;
    private readonly IAuditTrail? _auditTrail;
    private readonly IOperationSigner? _signer;
    private readonly Sunfish.Foundation.Integrations.Payments.IPaymentGateway? _paymentGateway;
    private readonly Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor? _fieldEncryptor;
    private readonly TenantId _auditTenant;
    private readonly TimeProvider _time;

    /// <summary>Per-application payment authorization handles returned by <c>IPaymentGateway.AuthorizeAsync</c>; used by Phase 8 to capture the fee on Accept.</summary>
    private readonly ConcurrentDictionary<ApplicationId, string> _paymentAuthHandles = new();

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

    /// <summary>Creates the service with capability-promotion + validation + audit hooks disabled.</summary>
    public InMemoryLeasingPipelineService() : this(prospectPromoter: null, inquiryValidator: null, time: null) { }

    /// <summary>Creates the service with optional capability-promotion wiring (validator disabled).</summary>
    public InMemoryLeasingPipelineService(ICapabilityPromoter? prospectPromoter, TimeProvider? time)
        : this(prospectPromoter, inquiryValidator: null, time) { }

    /// <summary>Creates the service with optional capability-promotion + inquiry-validation wiring (W#22 Phase 5).</summary>
    public InMemoryLeasingPipelineService(
        ICapabilityPromoter? prospectPromoter,
        IInquiryValidator? inquiryValidator,
        TimeProvider? time)
        : this(prospectPromoter, inquiryValidator, auditTrail: null, signer: null, tenantId: default, time)
    {
    }

    /// <summary>Creates the service with optional capability-promotion + validation + audit-emission wiring (W#22 Phase 6). When <paramref name="auditTrail"/> + <paramref name="signer"/> + <paramref name="tenantId"/> are all supplied, every lifecycle event emits an <see cref="AuditRecord"/> per the 12 leasing-pipeline AuditEventType constants in kernel-audit.</summary>
    public InMemoryLeasingPipelineService(
        ICapabilityPromoter? prospectPromoter,
        IInquiryValidator? inquiryValidator,
        IAuditTrail? auditTrail,
        IOperationSigner? signer,
        TenantId tenantId,
        TimeProvider? time)
        : this(prospectPromoter, inquiryValidator, auditTrail, signer, tenantId, paymentGateway: null, time)
    {
    }

    /// <summary>
    /// Creates the service with full cross-package wiring (W#22 Phase 7).
    /// When <paramref name="paymentGateway"/> is supplied,
    /// <see cref="SubmitApplicationAsync"/> authorizes the application
    /// fee at submission time + stores the auth handle for downstream
    /// capture on Accept. Mirrors the W#19 Phase 6 cross-package pattern.
    /// </summary>
    public InMemoryLeasingPipelineService(
        ICapabilityPromoter? prospectPromoter,
        IInquiryValidator? inquiryValidator,
        IAuditTrail? auditTrail,
        IOperationSigner? signer,
        TenantId tenantId,
        Sunfish.Foundation.Integrations.Payments.IPaymentGateway? paymentGateway,
        TimeProvider? time)
        : this(prospectPromoter, inquiryValidator, auditTrail, signer, tenantId, paymentGateway, fieldEncryptor: null, time) { }

    /// <summary>
    /// W#22 Phase 9 (post-W#32): adds <paramref name="fieldEncryptor"/> for
    /// per-field encryption of <see cref="DemographicProfileSubmission"/>
    /// at the <see cref="SubmitApplicationAsync"/> boundary. When null,
    /// the demographic submission is mapped to an all-null
    /// <see cref="DemographicProfile"/> (test/bootstrap fall-back); plaintext
    /// is never persisted in either case.
    /// </summary>
    public InMemoryLeasingPipelineService(
        ICapabilityPromoter? prospectPromoter,
        IInquiryValidator? inquiryValidator,
        IAuditTrail? auditTrail,
        IOperationSigner? signer,
        TenantId tenantId,
        Sunfish.Foundation.Integrations.Payments.IPaymentGateway? paymentGateway,
        Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor? fieldEncryptor,
        TimeProvider? time)
    {
        if ((auditTrail is null) ^ (signer is null))
        {
            throw new ArgumentException("auditTrail and signer must both be supplied together (or both null).");
        }
        if (auditTrail is not null && tenantId == default)
        {
            throw new ArgumentException("tenantId is required when audit emission is wired.", nameof(tenantId));
        }
        _prospectPromoter = prospectPromoter;
        _inquiryValidator = inquiryValidator;
        _auditTrail = auditTrail;
        _signer = signer;
        _paymentGateway = paymentGateway;
        _fieldEncryptor = fieldEncryptor;
        _auditTenant = tenantId;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Snapshot of payment authorization handles per application (set when <see cref="SubmitApplicationAsync"/> runs with an <c>IPaymentGateway</c> wired).</summary>
    public IReadOnlyDictionary<ApplicationId, string> PaymentAuthorizationHandles => _paymentAuthHandles;

    private async Task EmitAsync(AuditEventType eventType, AuditPayload payload, CancellationToken ct)
    {
        if (_auditTrail is null || _signer is null)
        {
            return;
        }
        var occurredAt = _time.GetUtcNow();
        var signed = await _signer.SignAsync(payload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: _auditTenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    // ── IPublicInquiryService ─────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<Inquiry> SubmitInquiryAsync(
        PublicInquiryRequest request,
        AnonymousCapability capability,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(capability);
        ct.ThrowIfCancellationRequested();

        if (_inquiryValidator is not null)
        {
            var verdict = await _inquiryValidator.ValidateAsync(request, ct).ConfigureAwait(false);
            if (!verdict.Passed)
            {
                await EmitAsync(
                    AuditEventType.InquiryRejected,
                    LeasingPipelineAuditPayloadFactory.InquiryRejected(request, verdict.FailedAt!.Value, verdict.Reason ?? "Inquiry rejected."),
                    ct).ConfigureAwait(false);
                throw new InquiryValidationException(verdict.FailedAt!.Value, verdict.Reason ?? "Inquiry rejected.");
            }
        }

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
        await EmitAsync(
            AuditEventType.InquiryAccepted,
            LeasingPipelineAuditPayloadFactory.InquiryAccepted(inquiry),
            ct).ConfigureAwait(false);
        return inquiry;
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

        await EmitAsync(
            AuditEventType.ProspectPromoted,
            LeasingPipelineAuditPayloadFactory.ProspectPromoted(prospect),
            ct).ConfigureAwait(false);
        return prospect;
    }

    /// <inheritdoc />
    public async Task<Application> SubmitApplicationAsync(
        SubmitApplicationRequest request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        if (!_prospects.ContainsKey(request.Prospect))
        {
            throw new InvalidOperationException($"Prospect '{request.Prospect.Value}' not found.");
        }

        // W#22 Phase 9: encrypt demographic submission at the boundary.
        // Plaintext does not flow past this point.
        var encryptedDemographics = await EncryptDemographicProfileAsync(request.Demographics, request.Tenant, ct).ConfigureAwait(false);

        var application = new Application
        {
            Id = new ApplicationId(Guid.NewGuid()),
            Tenant = request.Tenant,
            Prospect = request.Prospect,
            Listing = request.Listing,
            Facts = request.Facts,
            Demographics = encryptedDemographics,
            Status = ApplicationStatus.Submitted,
            ApplicationSignature = request.Signature,
            ApplicationFee = request.ApplicationFee,
            SubmittedAt = _time.GetUtcNow(),
        };

        _applications[application.Id] = application;

        if (_paymentGateway is not null)
        {
            // ADR 0051 + W#19 Phase 0 stub: authorize the application fee
            // at submission. Phase 8 (or a Phase 3.1 follow-up) captures
            // on operator Accept; refunds on Withdraw / Decline are also
            // possible but require separate operator action.
            var authResult = await _paymentGateway.AuthorizeAsync(
                new Sunfish.Foundation.Integrations.Payments.PaymentAuthorizationRequest(
                    Tenant: request.Tenant,
                    Amount: request.ApplicationFee,
                    CorrelationId: application.Id.Value.ToString("D")),
                ct).ConfigureAwait(false);
            _paymentAuthHandles[application.Id] = authResult.AuthorizationHandle;
        }

        await EmitAsync(
            AuditEventType.ApplicationSubmitted,
            LeasingPipelineAuditPayloadFactory.ApplicationSubmitted(application),
            ct).ConfigureAwait(false);
        return application;
    }

    /// <inheritdoc />
    public async Task<ApplicantCapability> ConfirmApplicationAndPromoteAsync(
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

        await EmitAsync(
            AuditEventType.ApplicantPromoted,
            LeasingPipelineAuditPayloadFactory.ApplicantPromoted(application, confirmedBy),
            ct).ConfigureAwait(false);
        return capability;
    }

    /// <inheritdoc />
    public async Task<Application> RecordBackgroundCheckAsync(
        ApplicationId applicationId,
        BackgroundCheckResult result,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(result);
        ct.ThrowIfCancellationRequested();
        var application = TransitionApplication(applicationId, ApplicationStatus.AwaitingDecision);
        await EmitAsync(
            AuditEventType.BackgroundCheckCompleted,
            LeasingPipelineAuditPayloadFactory.BackgroundCheckCompleted(applicationId, result),
            ct).ConfigureAwait(false);
        return application;
    }

    /// <inheritdoc />
    public async Task<Application> RecordDecisionAsync(
        ApplicationId applicationId,
        ApplicationDecision decision,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ct.ThrowIfCancellationRequested();

        var newStatus = decision.Accepted ? ApplicationStatus.Accepted : ApplicationStatus.Declined;
        var application = TransitionApplication(applicationId, newStatus, decided: true, decidedBy: decision.DecidedBy);
        var eventType = decision.Accepted ? AuditEventType.ApplicationAccepted : AuditEventType.ApplicationDeclined;
        await EmitAsync(
            eventType,
            LeasingPipelineAuditPayloadFactory.ApplicationDecision(application, newStatus, decision.DecidedBy, decision.Reason),
            ct).ConfigureAwait(false);
        return application;
    }

    /// <inheritdoc />
    public async Task<Application> WithdrawApplicationAsync(
        ApplicationId applicationId,
        ActorId withdrawnBy,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var application = TransitionApplication(applicationId, ApplicationStatus.Withdrawn, decided: true, decidedBy: withdrawnBy);
        await EmitAsync(
            AuditEventType.ApplicationWithdrawn,
            LeasingPipelineAuditPayloadFactory.ApplicationDecision(application, ApplicationStatus.Withdrawn, withdrawnBy, reason: null),
            ct).ConfigureAwait(false);
        return application;
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

    /// <summary>
    /// Encrypts every non-null field of <paramref name="submission"/> via
    /// <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldEncryptor"/>
    /// (purpose label <c>encrypted-field-aes</c>; per-tenant DEK). When
    /// the field encryptor is not wired, returns an all-null
    /// <see cref="DemographicProfile"/> — plaintext is dropped, NOT
    /// persisted. W#22 Phase 9 / W#32.
    /// </summary>
    private async Task<DemographicProfile> EncryptDemographicProfileAsync(
        DemographicProfileSubmission submission, TenantId tenant, CancellationToken ct)
    {
        if (_fieldEncryptor is null)
        {
            return new DemographicProfile();
        }

        async Task<Sunfish.Foundation.Recovery.EncryptedField?> EncryptField(string? plaintext)
        {
            if (string.IsNullOrEmpty(plaintext))
            {
                return null;
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            return await _fieldEncryptor.EncryptAsync(bytes, tenant, ct).ConfigureAwait(false);
        }

        return new DemographicProfile
        {
            RaceOrEthnicity = await EncryptField(submission.RaceOrEthnicity),
            NationalOrigin = await EncryptField(submission.NationalOrigin),
            Religion = await EncryptField(submission.Religion),
            Sex = await EncryptField(submission.Sex),
            DisabilityStatus = await EncryptField(submission.DisabilityStatus),
            FamilialStatus = await EncryptField(submission.FamilialStatus),
            MaritalStatus = await EncryptField(submission.MaritalStatus),
            IncomeSourceType = await EncryptField(submission.IncomeSourceType),
        };
    }

    /// <inheritdoc />
    public Task<Prospect?> GetProspectByEmailAsync(TenantId tenant, string email, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Task.FromResult<Prospect?>(null);
        }
        var normalized = email.Trim();
        var match = _prospects.Values.FirstOrDefault(p =>
            p.Tenant.Equals(tenant)
            && string.Equals(p.VerifiedEmail, normalized, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<Prospect?>(match);
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
