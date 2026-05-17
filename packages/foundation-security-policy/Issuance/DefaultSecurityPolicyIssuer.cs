using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.SecurityPolicy.Validation;
using Sunfish.Foundation.Ship.Common;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;
using Sunfish.Kernel.Audit.Payloads;

namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Phase 1 reference implementation of
/// <see cref="ISecurityPolicyIssuer"/>. Composes
/// <see cref="IStandingOrderIssuer"/> for the underlying Standing
/// Order state machine + <see cref="IAuditTrail"/> for the
/// <c>Sunfish.SecurityPolicy.*</c> audit cohort.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// <b>Authorization contract.</b> The issuer does NOT consult
/// <c>IUserContext</c>. The caller is the authority on whether the
/// supplied actor identities are authenticated; the issuer enforces
/// only the §3.1 ApprovalChain floor + §3.1.1 CapabilityProof
/// freshness + the proposer-cannot-self-approve invariant.
/// </para>
/// <para>
/// <b>Phase 1 durability gap.</b> In-flight approval proofs are
/// tracked in a process-local <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by <see cref="StandingOrderId"/>; a process restart loses
/// pending proposals (their underlying Standing Order is still in
/// the durable repository, but the per-approver proof-expiry
/// metadata is gone and a fresh approval round is required).
/// Persistent backing is a Phase 2 follow-on tracked by
/// <c>ITenantSecurityPolicyLoader</c> (PR 3b.4) + the SQLite
/// substrate work in W#37 Phase 2.
/// </para>
/// </remarks>
public sealed class DefaultSecurityPolicyIssuer : ISecurityPolicyIssuer
{
    private readonly IEnumerable<ISecurityPolicyValidator> _validators;
    private readonly IStandingOrderIssuer _standingOrderIssuer;
    private readonly ISecurityPolicyApprovalFloorProvider _approvalFloor;
    private readonly IActorPrincipalResolver _principalResolver;
    private readonly IShipRoleAssignmentSource _roleSource;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly TimeProvider _time;
    private readonly Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> _policyLoader;
    private readonly SecurityPolicyIssuerOptions _options;

    // Per-proposal in-flight approval state. KEY: StandingOrderId of the
    // Issued proposal. VALUE: ordered (chronological) list of approvals
    // recorded so far + the proof expiry the approver supplied. Phase 1
    // durability gap — see class-level remarks.
    // TODO(PR 3b.4): bound _inFlight via a sweep against
    // ApprovalProofMaxAge once ITenantSecurityPolicyLoader provides the
    // durable backing store. Until then a malicious or buggy caller can
    // flood ProposeAsync and never approve, growing the per-process map.
    // Council SE-2 (LOW) acknowledged + deferred.
    private readonly ConcurrentDictionary<StandingOrderId, InFlightProposal> _inFlight = new();

    /// <summary>Construct an issuer bound to its collaborators.</summary>
    public DefaultSecurityPolicyIssuer(
        IEnumerable<ISecurityPolicyValidator> validators,
        IStandingOrderIssuer standingOrderIssuer,
        ISecurityPolicyApprovalFloorProvider approvalFloor,
        IActorPrincipalResolver principalResolver,
        IShipRoleAssignmentSource roleSource,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        TimeProvider time,
        Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> policyLoader,
        IOptions<SecurityPolicyIssuerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(validators);
        ArgumentNullException.ThrowIfNull(standingOrderIssuer);
        ArgumentNullException.ThrowIfNull(approvalFloor);
        ArgumentNullException.ThrowIfNull(principalResolver);
        ArgumentNullException.ThrowIfNull(roleSource);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(policyLoader);
        ArgumentNullException.ThrowIfNull(options);

        _validators = validators;
        _standingOrderIssuer = standingOrderIssuer;
        _approvalFloor = approvalFloor;
        _principalResolver = principalResolver;
        _roleSource = roleSource;
        _auditTrail = auditTrail;
        _signer = signer;
        _time = time;
        _policyLoader = policyLoader;
        _options = options.Value ?? new SecurityPolicyIssuerOptions();
    }

    /// <inheritdoc />
    public async ValueTask<StandingOrderId> ProposeAsync(
        TenantId tenant,
        ActorId proposer,
        TenantSecurityPolicy proposed,
        string rationale,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentException.ThrowIfNullOrWhiteSpace(rationale);

        var occurredAt = _time.GetUtcNow();
        var current = await _policyLoader(tenant, ct).ConfigureAwait(false);
        var proposerRoles = await SnapshotRolesAsync(tenant, ct).ConfigureAwait(false);
        var proposerRole = proposerRoles.TryGetValue(proposer, out var r) ? r : ShipRole.DivisionOfficer;
        var context = new SecurityPolicyValidationContext(tenant, proposer, proposerRole);

        // Run every validator + every floor validator; aggregate findings
        // (no short-circuit) per ADR 0068 §2.1.
        var allFindings = new List<SecurityPolicyValidationFinding>();
        foreach (var v in _validators.OrderBy(v => (int)v.Priority))
        {
            var result = await v.ValidateAsync(proposed, current, context, ct).ConfigureAwait(false);
            allFindings.AddRange(result.Findings);
        }
        var errors = allFindings.Where(f => f.Severity == SecurityPolicyValidationSeverity.Error).ToList();

        if (errors.Count > 0)
        {
            // Issue a marker StandingOrder so the proposal has a stable id
            // for the SecurityPolicyRejected audit payload, then emit + throw.
            var rejectedId = await IssueUnderlyingStandingOrderAsync(
                tenant, proposer, proposed, current, rationale, ct).ConfigureAwait(false);
            await EmitSecurityPolicyAuditAsync(
                AuditEventType.SecurityPolicyRejected,
                tenant,
                new SecurityPolicyAuditPayloads.RejectedPayload(tenant, rejectedId, string.Join("; ", errors.Select(f => f.Code))),
                occurredAt,
                ct).ConfigureAwait(false);
            throw new SecurityPolicyRejectedException(allFindings);
        }

        var proposalId = await IssueUnderlyingStandingOrderAsync(
            tenant, proposer, proposed, current, rationale, ct).ConfigureAwait(false);

        // §3.2 Captain-vacancy detection: re-use the role snapshot taken
        // before validator dispatch. The floor-evaluation at ApproveAsync
        // time is authoritative; this is a forensic breadcrumb on the
        // proposal audit payload.
        var captainVacancyInvoked = !proposerRoles.Values.Any(role => role == ShipRole.Captain);

        _inFlight[proposalId] = new InFlightProposal(
            ProposedPolicy: proposed,
            CurrentAtProposal: current,
            Proposer: proposer,
            Approvals: ImmutableList<RecordedApproval>.Empty);

        await EmitSecurityPolicyAuditAsync(
            AuditEventType.SecurityPolicyProposed,
            tenant,
            new SecurityPolicyAuditPayloads.ProposedPayload(
                TenantId: tenant,
                Proposer: proposer,
                StandingOrderId: proposalId,
                PolicyDiffSummary: SummarizeDiff(current, proposed),
                CaptainVacancyExceptionInvoked: captainVacancyInvoked),
            occurredAt,
            ct).ConfigureAwait(false);

        return proposalId;
    }

    /// <inheritdoc />
    public async ValueTask<SecurityPolicyApprovalResult> ApproveAsync(
        TenantId tenant,
        ActorId approver,
        StandingOrderId proposal,
        CapabilityProof approverProof,
        string? comment,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(approverProof);

        var occurredAt = _time.GetUtcNow();

        // Principal resolution — fail-closed when null per IActorPrincipalResolver
        // <remarks> contract.
        var principal = await _principalResolver.ResolveAsync(tenant, approver, ct).ConfigureAwait(false);
        if (principal is null)
        {
            await EmitSecurityPolicyAuditAsync(
                AuditEventType.SecurityPolicyRejected,
                tenant,
                new SecurityPolicyAuditPayloads.RejectedPayload(tenant, proposal, "PRINCIPAL_NOT_RESOLVED"),
                occurredAt,
                ct).ConfigureAwait(false);
            throw new SecurityPolicyRejectedException(
                new[]
                {
                    SecurityPolicyValidationFinding.Error(
                        "PRINCIPAL_NOT_RESOLVED",
                        $"Approver '{approver.Value}' did not resolve to a Principal.",
                        "Verify the approver is enrolled in the tenant's capability graph."),
                });
        }

        // Proof verification — IsFresh + IsBoundTo + Approver matches.
        if (approverProof.Approver != approver
            || !approverProof.IsBoundTo(proposal)
            || !approverProof.IsFresh(occurredAt))
        {
            await EmitSecurityPolicyAuditAsync(
                AuditEventType.SecurityPolicyRejected,
                tenant,
                new SecurityPolicyAuditPayloads.RejectedPayload(tenant, proposal, "PROOF_INVALID"),
                occurredAt,
                ct).ConfigureAwait(false);
            throw new SecurityPolicyRejectedException(
                new[]
                {
                    SecurityPolicyValidationFinding.Error(
                        "PROOF_INVALID",
                        "The supplied capability proof is stale, mismatched-binding, or attributed to the wrong actor.",
                        "Mint a fresh capability proof bound to the target proposal id."),
                });
        }

        // Atomic compare-and-swap append on the in-flight proposal. Per
        // council .NET-architect A.1 (lost-update race): a naive
        // TryGetValue → modify → assign sequence is NOT atomic under
        // concurrent ApproveAsync calls — two approvers can both observe
        // the same baseline and the later write clobbers the earlier
        // approval. AddOrUpdate makes the read-modify-write a single
        // CAS over the per-proposal slot; the addValueFactory throws so
        // a vanished proposal (rescinded between this call and the CAS)
        // surfaces as PROPOSAL_NOT_FOUND rather than creating a phantom
        // entry.
        var newApproval = new RecordedApproval(approver, occurredAt, approverProof.ExpiresAt, comment);
        InFlightProposal updated;
        try
        {
            updated = _inFlight.AddOrUpdate(
                key: proposal,
                addValueFactory: _ => throw new KeyNotFoundException(),
                updateValueFactory: (_, existing) => existing with { Approvals = existing.Approvals.Add(newApproval) });
        }
        catch (KeyNotFoundException)
        {
            await EmitSecurityPolicyAuditAsync(
                AuditEventType.SecurityPolicyRejected,
                tenant,
                new SecurityPolicyAuditPayloads.RejectedPayload(tenant, proposal, "PROPOSAL_NOT_FOUND"),
                occurredAt,
                ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"No in-flight proposal with id {proposal.Value:N}; it may have been Applied, Rescinded, or originated in a prior process.");
        }

        // Emit ApprovalReceived AFTER the successful CAS append so the
        // audit log only records approvals that actually landed on a
        // live proposal.
        await EmitSecurityPolicyAuditAsync(
            AuditEventType.SecurityPolicyApprovalReceived,
            tenant,
            new SecurityPolicyAuditPayloads.ApprovalReceivedPayload(tenant, proposal, approver, occurredAt),
            occurredAt,
            ct).ConfigureAwait(false);

        var roles = await SnapshotRolesAsync(tenant, ct).ConfigureAwait(false);
        var chain = new ApprovalChain(updated.Approvals
            .Select(a => new ApprovalStep(a.Approver, a.ApprovedAt, a.Comment))
            .ToArray());
        var proofExpiries = updated.Approvals.ToDictionary(a => a.Approver, a => a.ProofExpiresAt);

        var verdict = _approvalFloor.Evaluate(
            proposal: proposal,
            proposer: updated.Proposer,
            chainSoFar: chain,
            approverRoles: roles,
            proofExpiriesByApprover: proofExpiries,
            now: occurredAt);

        if (!verdict.AllowApply)
        {
            return new SecurityPolicyApprovalResult(
                IsApprovalChainSatisfied: false,
                ApprovalsGranted: updated.Approvals.Count,
                ApprovalsRequired: DefaultSecurityPolicyApprovalFloorProvider.MinimumApproverCount);
        }

        // Floor satisfied — transition by issuing a NEW StandingOrder that
        // supersedes the proposal. Per ADR 0065 §4 the Issued proposal is NOT
        // mutated in place; the application is a fresh Standing Order whose
        // triples carry the proposed policy as the new value.
        //
        // Apply-once race-guard: a concurrent approver may already have
        // crossed the floor and removed the proposal from _inFlight while
        // this call was between the CAS append and here. TryRemove returns
        // false in that case — abort the Apply transition (audit + Standing
        // Order would be duplicated) but still report the chain as satisfied
        // because it factually IS (this approver's vote landed; another
        // approver's win is what actually drove the Apply).
        if (_inFlight.TryRemove(proposal, out _))
        {
            await IssueAppliedStandingOrderAsync(tenant, updated, chain, ct).ConfigureAwait(false);
            await EmitSecurityPolicyAuditAsync(
                AuditEventType.SecurityPolicyApplied,
                tenant,
                new SecurityPolicyAuditPayloads.AppliedPayload(tenant, proposal, occurredAt),
                occurredAt,
                ct).ConfigureAwait(false);
        }

        return new SecurityPolicyApprovalResult(
            IsApprovalChainSatisfied: true,
            ApprovalsGranted: updated.Approvals.Count,
            ApprovalsRequired: DefaultSecurityPolicyApprovalFloorProvider.MinimumApproverCount);
    }

    /// <inheritdoc />
    public async ValueTask RescindAsync(
        TenantId tenant,
        ActorId actor,
        StandingOrderId proposal,
        string reason,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!_inFlight.TryGetValue(proposal, out var inflight))
        {
            throw new InvalidOperationException(
                $"No in-flight proposal with id {proposal.Value:N}; rescission requires an Issued (not Applied / Rescinded) proposal.");
        }

        // Authorization: original proposer OR an actor holding Captain.
        if (actor != inflight.Proposer)
        {
            var roles = await SnapshotRolesAsync(tenant, ct).ConfigureAwait(false);
            if (!(roles.TryGetValue(actor, out var role) && role == ShipRole.Captain))
            {
                throw new System.UnauthorizedAccessException(
                    $"Actor '{actor.Value}' is neither the proposer nor a Captain; rescission denied.");
            }
        }

        var occurredAt = _time.GetUtcNow();

        await _standingOrderIssuer.RescindAsync(proposal, actor, reason, _auditTrail, ct).ConfigureAwait(false);

        await EmitSecurityPolicyAuditAsync(
            AuditEventType.SecurityPolicyRescinded,
            tenant,
            new SecurityPolicyAuditPayloads.RescindedPayload(tenant, proposal, actor, occurredAt),
            occurredAt,
            ct).ConfigureAwait(false);

        _inFlight.TryRemove(proposal, out _);
    }

    // ---- helpers ----

    private async ValueTask<StandingOrderId> IssueUnderlyingStandingOrderAsync(
        TenantId tenant,
        ActorId proposer,
        TenantSecurityPolicy proposed,
        TenantSecurityPolicy current,
        string rationale,
        CancellationToken ct)
    {
        var draft = new StandingOrderDraft(
            TenantId: tenant,
            Scope: StandingOrderScope.Security,
            Triples: new[]
            {
                new StandingOrderTriple(
                    Path: "tenant.security-policy",
                    OldValue: JsonNode.Parse("\"<elided-for-Phase-1>\""),
                    NewValue: JsonNode.Parse("\"<elided-for-Phase-1>\"")),
            },
            Rationale: rationale,
            ApprovalChain: null);
        var realized = await _standingOrderIssuer.IssueAsync(draft, proposer, _auditTrail, ct).ConfigureAwait(false);
        return realized.Id;
    }

    private async ValueTask IssueAppliedStandingOrderAsync(
        TenantId tenant,
        InFlightProposal inflight,
        ApprovalChain chain,
        CancellationToken ct)
    {
        var draft = new StandingOrderDraft(
            TenantId: tenant,
            Scope: StandingOrderScope.Security,
            Triples: new[]
            {
                new StandingOrderTriple(
                    Path: "tenant.security-policy",
                    OldValue: JsonNode.Parse("\"<elided-for-Phase-1>\""),
                    NewValue: JsonNode.Parse("\"<elided-for-Phase-1>\"")),
            },
            Rationale: "applied",
            ApprovalChain: chain);
        await _standingOrderIssuer.IssueAsync(draft, inflight.Proposer, _auditTrail, ct).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyDictionary<ActorId, ShipRole>> SnapshotRolesAsync(TenantId tenant, CancellationToken ct)
    {
        var assignments = await _roleSource.LoadAssignmentsAsync(tenant, ct).ConfigureAwait(false);
        return assignments.ToDictionary(a => a.Holder, a => a.Role);
    }

    private static string SummarizeDiff(TenantSecurityPolicy current, TenantSecurityPolicy proposed)
    {
        // Phase 1 placeholder. PR 3b.4 or follow-on will hand back a structured
        // diff via TenantSecurityPolicyDiffPrinter; for now the audit payload
        // carries a coarse hash of the proposed policy so forensic queries can
        // group related entries. NOT a security-sensitive payload field
        // (proof of policy content is the underlying Standing Order's triple
        // payload).
        return $"diff:{proposed.GetHashCode():X8}";
    }

    private async ValueTask EmitSecurityPolicyAuditAsync<T>(
        AuditEventType eventType,
        TenantId tenant,
        T payload,
        DateTimeOffset occurredAt,
        CancellationToken ct)
    {
        var auditId = Guid.NewGuid();
        var auditPayload = new AuditPayload(new Dictionary<string, object?>
        {
            ["security_policy_event"] = eventType.Value,
            ["payload"] = payload,
            ["audit_id"] = auditId.ToString("N"),
        });
        var signed = await _signer.SignAsync(auditPayload, occurredAt, Guid.NewGuid(), ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: auditId,
            TenantId: tenant,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: ImmutableArray<AttestingSignature>.Empty);
        await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
    }

    // ---- per-proposal in-flight state ----

    private sealed record InFlightProposal(
        TenantSecurityPolicy ProposedPolicy,
        TenantSecurityPolicy CurrentAtProposal,
        ActorId Proposer,
        ImmutableList<RecordedApproval> Approvals);

    private sealed record RecordedApproval(
        ActorId Approver,
        DateTimeOffset ApprovedAt,
        DateTimeOffset ProofExpiresAt,
        string? Comment);
}

/// <summary>
/// Thrown by <see cref="ISecurityPolicyIssuer"/> when a proposal is
/// rejected on validation grounds or its approval is rejected on
/// proof / principal grounds. The <see cref="Findings"/> property
/// carries the same finding cohort that was emitted into the
/// corresponding <c>SecurityPolicyRejected</c> audit payload.
/// </summary>
public sealed class SecurityPolicyRejectedException : Exception
{
    /// <summary>Read-only view of the rejection findings.</summary>
    public IReadOnlyList<SecurityPolicyValidationFinding> Findings { get; }

    /// <summary>Construct with the rejection findings.</summary>
    public SecurityPolicyRejectedException(IEnumerable<SecurityPolicyValidationFinding> findings)
        : base("Security-policy proposal or approval was rejected. See Findings for details.")
    {
        Findings = findings?.ToImmutableArray() ?? throw new ArgumentNullException(nameof(findings));
    }
}
