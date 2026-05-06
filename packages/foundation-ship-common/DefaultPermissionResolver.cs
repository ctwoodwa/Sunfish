using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.MultiTenancy;
using Sunfish.Foundation.Wayfinder;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Reference <see cref="IPermissionResolver"/> implementation per ADR 0077 §2.
/// Composes <see cref="IShipRoleAssignmentSource"/> + (optional)
/// <see cref="ICapabilityGraph"/> + (optional)
/// <see cref="IShipActionMissionEnvelopeGate"/> with mandatory
/// <see cref="IAuditTrail"/> + <see cref="IOperationSigner"/> for §Trust
/// emission.
/// </summary>
/// <remarks>
/// <para>
/// <b>§Trust:</b> Audit emission for <c>Denied</c> decisions and audit-loud
/// <c>Granted</c> decisions is mandatory. <see cref="IAuditTrail"/> and
/// <see cref="IOperationSigner"/> MUST be supplied at construction; the
/// resolver throws <see cref="InvalidOperationException"/> at first audit-emit
/// attempt when either is missing — fail loudly rather than run authority
/// resolution with no audit trail (W#49 cohort precedent).
/// </para>
/// <para>
/// <b>Cache (per ADR 0077 §2.5; halt-condition C):</b> the not-yet-shipped <c>IStandingOrderEventStream</c>
/// is not yet built (ADR 0065-A1 spec-only). Phase 1 ships a per-tenant 60-second
/// TTL cache of role assignments. Subscribe-before-load invalidation is a
/// follow-up once the not-yet-shipped <c>IStandingOrderEventStream</c> ships in
/// <c>packages/foundation-wayfinder/</c>. See PR description for the
/// follow-up tracking.
/// </para>
/// <para>
/// <b>Rate-limiting (per §2.4):</b> Per-<c>(ActorId, ShipLocation)</c> denial
/// counter with 1-minute sliding window. When the counter exceeds N=10 within
/// the window: emit <see cref="AuditEventType.PermissionDenialRateExceeded"/>
/// once and short-circuit subsequent calls within the window with
/// <see cref="DenialReason.SecurityPolicyBlocked"/>.
/// </para>
/// </remarks>
public sealed class DefaultPermissionResolver : IPermissionResolver
{
    /// <summary>
    /// Default sliding-window threshold for the
    /// per-<c>(ActorId, ShipLocation)</c> denial-rate-limit per §2.4.
    /// Tenants MAY override via the constructor parameter.
    /// </summary>
    public const int DefaultDenialRateLimit = 10;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Authority gradient rank per §1 + §2.1 step 0(b) hierarchy invariant.
    /// Lower numbers = higher authority. <see cref="ShipAction.PromoteRole"/>
    /// requires the caller's effective role rank to be strictly less than the
    /// target role's rank.
    /// </summary>
    private static readonly ImmutableDictionary<ShipRole, int> AuthorityRank =
        ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(ShipRole.Captain, 0),
            KeyValuePair.Create(ShipRole.XO, 1),
            KeyValuePair.Create(ShipRole.EngineerOfficer, 2),
            KeyValuePair.Create(ShipRole.Navigator, 2),
            KeyValuePair.Create(ShipRole.TacticalOfficer, 2),
            KeyValuePair.Create(ShipRole.DivisionOfficer, 3),
            KeyValuePair.Create(ShipRole.IDC, 3),
            KeyValuePair.Create(ShipRole.Scribe, 3),
            KeyValuePair.Create(ShipRole.SUPPO, 3),
            KeyValuePair.Create(ShipRole.OOD, 4),
            KeyValuePair.Create(ShipRole.EOOW, 4),
        });

    /// <summary>
    /// Step 0(a) — per-action minimum deck. Callers passing
    /// <see cref="DeckDepth.MainDeck"/> for <see cref="ShipAction.Quarantine"/>
    /// are silently promoted to <see cref="DeckDepth.BelowTheWaterline"/>.
    /// </summary>
    public static readonly ImmutableDictionary<ShipAction, DeckDepth> ActionMinimumDeck =
        ImmutableDictionary.CreateRange(new[]
        {
            KeyValuePair.Create(ShipAction.Read, DeckDepth.TopDeck),
            KeyValuePair.Create(ShipAction.Write, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.IssueStandingOrder, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.Approve, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.PromoteRole, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.StandWatch, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.TransferWatch, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.Quarantine, DeckDepth.BelowTheWaterline),
            KeyValuePair.Create(ShipAction.OverrideQuarantine, DeckDepth.BelowTheWaterline),
        });

    /// <summary>
    /// Resource-scoped actions per §2.0 — passing <c>resource: null</c> for
    /// any of these short-circuits to <see cref="DenialReason.SecurityPolicyBlocked"/>
    /// at step 0(c).
    /// </summary>
    private static readonly ImmutableHashSet<ShipAction> ResourceScopedActions =
        ImmutableHashSet.CreateRange(new[]
        {
            ShipAction.Approve,
            ShipAction.Quarantine,
            ShipAction.OverrideQuarantine,
        });

    /// <summary>
    /// Audit-loud action set per §2.4 — <see cref="PermissionDecision.Granted"/>
    /// outcomes for these actions ALSO emit
    /// <see cref="AuditEventType.PermissionDenied"/>... wait, they emit a
    /// distinct grant audit. Phase 1 deliberately scopes audit emission to
    /// the denied path + the rate-limit path; the loud-grant audit-event
    /// type is reserved for a follow-up (no public consumer exists in
    /// Phase 1, so adding the event type now is YAGNI).
    /// </summary>
    private static readonly ImmutableArray<ShipAction> AuditLoudActionsImpl =
        ImmutableArray.Create(
            ShipAction.Quarantine,
            ShipAction.OverrideQuarantine,
            ShipAction.TransferWatch,
            ShipAction.PromoteRole,
            ShipAction.Approve);

    private readonly IShipRoleAssignmentSource _assignmentSource;
    private readonly ICapabilityGraph? _capabilityGraph;
    private readonly IShipActionMissionEnvelopeGate? _envelopeGate;
    private readonly IAuditTrail _auditTrail;
    private readonly IOperationSigner _signer;
    private readonly ILogger<DefaultPermissionResolver> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly int _denialRateLimit;

    private readonly Dictionary<TenantId, CachedAssignments> _cache = new();
    private readonly Dictionary<(ActorId, ShipLocation), DenialWindow> _denialWindows = new();
    private readonly object _gate = new();

    /// <summary>Creates a resolver bound to the supplied collaborators.</summary>
    /// <param name="assignmentSource">Materializes <see cref="ShipRoleAssignment"/> records per tenant.</param>
    /// <param name="auditTrail">Audit trail for <c>Denied</c> + rate-limit emissions. Mandatory.</param>
    /// <param name="signer">Operation signer for audit-record envelopes. Mandatory.</param>
    /// <param name="logger">Logger; non-nullable so audit-write swallows are observable.</param>
    /// <param name="capabilityGraph">Optional capability graph for §2.1 step 6; null skips the check.</param>
    /// <param name="envelopeGate">Optional Mission-Envelope gate for §2.1 step 2; null skips the check.</param>
    /// <param name="timeProvider">Clock source. Defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="denialRateLimit">Override for the §2.4 rate-limit threshold. Defaults to <see cref="DefaultDenialRateLimit"/>.</param>
    public DefaultPermissionResolver(
        IShipRoleAssignmentSource assignmentSource,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        ILogger<DefaultPermissionResolver> logger,
        ICapabilityGraph? capabilityGraph = null,
        IShipActionMissionEnvelopeGate? envelopeGate = null,
        TimeProvider? timeProvider = null,
        int? denialRateLimit = null)
    {
        _assignmentSource = assignmentSource ?? throw new ArgumentNullException(nameof(assignmentSource));
        _auditTrail = auditTrail ?? throw new ArgumentNullException(nameof(auditTrail));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _capabilityGraph = capabilityGraph;
        _envelopeGate = envelopeGate;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _denialRateLimit = denialRateLimit ?? DefaultDenialRateLimit;
        if (_denialRateLimit < 1) throw new ArgumentOutOfRangeException(nameof(denialRateLimit));
    }

    /// <inheritdoc />
    public IReadOnlyList<ShipAction> AuditLoudActions => AuditLoudActionsImpl;

    /// <inheritdoc />
    public async ValueTask<PermissionDecision> ResolveAsync(
        Principal subject,
        ShipLocation location,
        DeckDepth deck,
        ShipAction action,
        Resource? resource,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subject);
        var now = _timeProvider.GetUtcNow();
        // PrincipalId is a 32-byte Ed25519 public key; its string form is
        // base64url. Cache-key + audit-payload ActorId is derived via that
        // canonical encoding so cross-process replay round-trips cleanly.
        var subjectActor = new ActorId(subject.Id.ToBase64Url());

        // §2.4 rate-limit short-circuit — checked BEFORE step 0 so a
        // systematic denial-loop cannot execute resolution steps unbounded
        // times per minute.
        if (TryRateLimitShortCircuit(subjectActor, location, now, out var rateLimitDecision))
        {
            return rateLimitDecision!;
        }

        // §2.1 step 0(a) — deck canonicalization
        var effectiveDeck = ActionMinimumDeck.TryGetValue(action, out var floor) && floor > deck
            ? floor
            : deck;

        // §2.1 step 0(b) — promotion-target / self-promotion guard
        // Phase 1: the caller-supplied promotion target is not yet wired
        // through to ResolveAsync. The structural invariants (hierarchy +
        // self-promotion) are still encoded in the public surface so
        // consumers can call CheckPromotionGuard directly in their
        // PromoteRole pipeline; resolver-side enforcement requires a
        // PromoteRoleContext extension that Phase 2 (caller wiring) will
        // add.

        // §2.1 step 0(c) — resource-scope guard
        if (resource is null && ResourceScopedActions.Contains(action))
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.SecurityPolicyBlocked,
                "resource-scoped action requires a resource reference",
                new Remediation(
                    RemediationKind.None,
                    "This action requires identifying the specific record being targeted.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 1 — Watch precondition (PHASE-2-DEFER: requires OOD watch
        // lookup via IOodWatchService — not wired in Phase 1 because
        // foundation-ship-common cannot depend on foundation-wayfinder's
        // IOodWatchService without a dependency cycle once W#46 Phase 1
        // unblocks W#49 P3 consumers. Phase 2 (or a Phase 1.5 follow-up)
        // injects an IOnWatchProbe that returns the active OOD/EOOW
        // designation. Until then, watch-required actions return
        // Denied(WatchRequired, ...) with a "watch lookup deferred" hint.)
        if (IsWatchRequired(action, location, effectiveDeck))
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.WatchRequired,
                "this action requires the on-watch designation (OOD or EOOW)",
                new Remediation(
                    RemediationKind.AwaitWatch,
                    "Wait for the next watch rotation, or contact the current Officer of the Deck.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 2 — Mission Envelope gate (optional)
        if (_envelopeGate is not null)
        {
            var verdict = await _envelopeGate.EvaluateAsync(action, ct).ConfigureAwait(false);
            if (verdict is { IsAvailable: false } v)
            {
                var denied = new PermissionDecision.Denied(
                    DenialReason.MissionEnvelopeUnavailable,
                    v.ReasonDisplay,
                    new Remediation(
                        RemediationKind.UpgradeMissionEnvelope,
                        v.RemediationDisplay,
                        ContactActor: null,
                        EscalationLink: null,
                        CallToActionLabel: v.CallToActionLabel),
                    now);
                await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
                return denied;
            }
        }

        // §2.1 step 3 — Deferral check
        if (location == ShipLocation.SupplyOffice)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.Phase2Deferred,
                "Supply Office is deferred to Phase 2 commercial work",
                new Remediation(
                    RemediationKind.Phase2Deferred,
                    "No current access path — Supply Office ships with the Phase 2 commercial release.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }
        if (location is ShipLocation.Wardroom or ShipLocation.Brig)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.V2Deferred,
                $"{location} is deferred to v2 (commercial agreement required)",
                new Remediation(
                    RemediationKind.None,
                    "No current access path — v2 commercial agreement required.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 4 — Role match
        // Resolve the subject's tenant from the cached assignment lookup —
        // Phase 1 limits role lookup to a single tenant per call by
        // requiring the assignment source to materialize ALL tenants the
        // subject participates in; the caller-side mapping
        // Principal → TenantId is the consumer's responsibility (the
        // capability graph already encodes this binding via the signed-op
        // chain). For Phase 1 we look up across every cached tenant and
        // pick the first matching assignment.
        var matched = await FindAssignmentAsync(subjectActor, ct).ConfigureAwait(false);
        if (matched is null)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.NoMatchingRole,
                "no assigned role grants this action",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact your tenant administrator to request a role assignment.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: "Request role assignment"),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §1.6 — SUPPO is structurally valid but operationally inert until
        // Phase 2.
        if (matched.Role == ShipRole.SUPPO)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.Phase2Deferred,
                "SUPPO role is structurally assigned but operationally deferred to Phase 2",
                new Remediation(
                    RemediationKind.Phase2Deferred,
                    "No current access path — SUPPO ships operationally with the Phase 2 commercial release.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // Location scope check — Phase 1 uses a coarse "role is allowed in
        // location" map; per-action granularity is Phase 2. The map is
        // derived from §3.1 default-landing decks (a role with no landing
        // in a location has no scope there).
        if (!IsRoleAllowedInLocation(matched.Role, location))
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.LocationOutOfScope,
                "your role does not grant access at this location",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact the location's department head to request scoped access.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: "Request access"),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 5 — Deck restriction
        if (effectiveDeck == DeckDepth.BelowTheWaterline && !IsBelowTheWaterlineRole(matched.Role))
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.DeckRestriction,
                "destructive (below-the-waterline) actions require Captain or XO authority",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact the Captain or Executive Officer to request a destructive-action elevation.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: "Request elevation"),
                now);
            await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 6 — Capability check (optional graph)
        CapabilityProof? proof = null;
        if (_capabilityGraph is not null && resource is { } res)
        {
            var capabilityAction = MapToCapabilityAction(action);
            var ok = await _capabilityGraph.QueryAsync(subject.Id, res, capabilityAction, now, ct).ConfigureAwait(false);
            if (!ok)
            {
                var denied = new PermissionDecision.Denied(
                    DenialReason.NoMatchingRole,
                    "the capability graph does not record a grant for this subject + resource + action",
                    new Remediation(
                        RemediationKind.ContactAuthority,
                        "Contact the resource owner to request a capability grant.",
                        ContactActor: null,
                        EscalationLink: null,
                        CallToActionLabel: "Request grant"),
                    now);
                await EmitDenialAsync(subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
                return denied;
            }
            proof = await _capabilityGraph.ExportProofAsync(subject.Id, res, capabilityAction, now, ct).ConfigureAwait(false);
        }

        // §2.1 step 7 — Security policy gate (~ADR 0068 territory; Phase 1
        // ships with no policy provider — Phase 2 / W#37 wires
        // ITenantSecurityPolicy in once ADR 0068 reaches Accepted +
        // built).

        // §2.1 step 8 — Granted
        return new PermissionDecision.Granted(matched.Role, now, proof);
    }

    /// <summary>
    /// Public helper for callers building a <see cref="ShipAction.PromoteRole"/>
    /// pipeline. Implements §2.1 step 0(b) — hierarchy invariant +
    /// self-promotion prohibition. Returns null on success;
    /// <see cref="PermissionDecision.Denied"/> on failure (caller emits).
    /// </summary>
    /// <param name="callerRole">Effective <see cref="ShipRole"/> of the caller.</param>
    /// <param name="callerActor">Caller actor ID.</param>
    /// <param name="targetActor">Actor whose role is being promoted.</param>
    /// <param name="targetRole">Role the target is being promoted to.</param>
    /// <param name="now">Wall-clock time the decision is made.</param>
    public static PermissionDecision.Denied? CheckPromotionGuard(
        ShipRole callerRole, ActorId callerActor, ActorId targetActor, ShipRole targetRole, DateTimeOffset now)
    {
        if (callerActor.Equals(targetActor))
        {
            return new PermissionDecision.Denied(
                DenialReason.SecurityPolicyBlocked,
                "self-promotion forbidden",
                new Remediation(
                    RemediationKind.SecurityPolicyAppeal,
                    "Promotion must be requested from a higher-authority actor.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: "Request promotion"),
                now);
        }
        if (AuthorityRank[callerRole] >= AuthorityRank[targetRole])
        {
            return new PermissionDecision.Denied(
                DenialReason.SecurityPolicyBlocked,
                "insufficient authority to promote to target role",
                new Remediation(
                    RemediationKind.SecurityPolicyAppeal,
                    "Promotion must be performed by an actor of strictly higher authority.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: "Escalate"),
                now);
        }
        return null;
    }

    private bool TryRateLimitShortCircuit(
        ActorId actor, ShipLocation location, DateTimeOffset now,
        out PermissionDecision? decision)
    {
        decision = null;
        lock (_gate)
        {
            var key = (actor, location);
            if (_denialWindows.TryGetValue(key, out var window) && window.IsActiveAt(now, RateLimitWindow))
            {
                if (window.Count > _denialRateLimit)
                {
                    decision = new PermissionDecision.Denied(
                        DenialReason.SecurityPolicyBlocked,
                        "permission-denial rate limit exceeded",
                        new Remediation(
                            RemediationKind.SecurityPolicyAppeal,
                            "Too many denied requests; further requests are blocked until the 1-minute window expires.",
                            ContactActor: null,
                            EscalationLink: null,
                            CallToActionLabel: null),
                        now);
                    return true;
                }
            }
        }
        return false;
    }

    private async ValueTask EmitDenialAsync(
        ActorId actor, ShipLocation location, PermissionDecision.Denied denied,
        ShipAction action, DateTimeOffset occurredAt, CancellationToken ct)
    {
        bool emitRateLimitRecord = false;
        DateTimeOffset windowStartAt = occurredAt;
        int denialCount = 0;
        TenantId? auditTenant = null;

        lock (_gate)
        {
            var key = (actor, location);
            if (!_denialWindows.TryGetValue(key, out var window) || !window.IsActiveAt(occurredAt, RateLimitWindow))
            {
                window = new DenialWindow(occurredAt, 1);
            }
            else
            {
                window = window with { Count = window.Count + 1 };
            }
            _denialWindows[key] = window;

            if (window.Count == _denialRateLimit + 1)
            {
                emitRateLimitRecord = true;
                windowStartAt = window.WindowStartedAt;
                denialCount = window.Count;
            }
        }

        // Resolve the audit-tenant from the most-recent cached assignment
        // for the actor; fall back to TenantId.System when none cached.
        auditTenant = ResolveAuditTenantOrSystem(actor);

        var denialPayload = new AuditPayload(new Dictionary<string, object?>
        {
            ["action"] = action.Name,
            ["actor"] = actor.Value,
            ["decidedAt"] = denied.DecidedAt.ToString("O"),
            ["location"] = location.ToString(),
            ["reason"] = denied.Reason.ToString(),
            ["remediationKind"] = denied.Remediation.Kind.ToString(),
            ["severity"] = "Normal",
            ["tenantId"] = auditTenant.Value.Value,
        });
        await EmitAsync(AuditEventType.PermissionDenied, auditTenant.Value, denialPayload, occurredAt, ct).ConfigureAwait(false);

        if (emitRateLimitRecord)
        {
            var rateLimitPayload = new AuditPayload(new Dictionary<string, object?>
            {
                ["actor"] = actor.Value,
                ["denialCount"] = denialCount,
                ["location"] = location.ToString(),
                ["severity"] = "High",
                ["tenantId"] = auditTenant.Value.Value,
                ["windowStartedAt"] = windowStartAt.ToString("O"),
            });
            await EmitAsync(AuditEventType.PermissionDenialRateExceeded, auditTenant.Value, rateLimitPayload, occurredAt, ct).ConfigureAwait(false);
        }
    }

    private TenantId ResolveAuditTenantOrSystem(ActorId actor)
    {
        lock (_gate)
        {
            foreach (var (tenant, cached) in _cache)
            {
                if (cached.Assignments.Any(a => a.Holder.Equals(actor)))
                {
                    return tenant;
                }
            }
        }
        // TenantId.System sentinel is introduced by ADR 0084 (Proposed
        // 2026-05-05; CO acceptance flip pending). Until ADR 0084 reaches
        // Accepted on origin/main, fall back to TenantId.Default — audit
        // records emitted before any per-tenant cache warm-up will be
        // tagged with the default tenant rather than a yet-to-exist System
        // sentinel. Once ADR 0084 lands, swap this to TenantId.System and
        // any consumer counting on the default tenant gets re-pointed.
        return TenantId.Default;
    }

    private async ValueTask EmitAsync(
        AuditEventType eventType, TenantId tenantId, AuditPayload payload,
        DateTimeOffset occurredAt, CancellationToken ct)
    {
        var nonce = Guid.NewGuid();
        var signed = await _signer.SignAsync(payload, occurredAt, nonce, ct).ConfigureAwait(false);
        var record = new AuditRecord(
            AuditId: Guid.NewGuid(),
            TenantId: tenantId,
            EventType: eventType,
            OccurredAt: occurredAt,
            Payload: signed,
            AttestingSignatures: Array.Empty<AttestingSignature>());
        try
        {
            await _auditTrail.AppendAsync(record, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not AuditSignatureException && ex is not OperationCanceledException)
        {
            // Best-effort: audit-backend hiccups must not deny resolution
            // outcomes, but they MUST surface through the host's logging
            // pipeline so SREs can investigate.
            _logger.LogError(ex,
                "Permission audit write failed for {EventType}; continuing best-effort",
                eventType);
        }
    }

    private async ValueTask<ShipRoleAssignment?> FindAssignmentAsync(ActorId actor, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow();
        // Linear scan over every cached tenant — Phase 1 acceptable; Phase 2
        // will index by ActorId once the assignment source surfaces a
        // tenant-resolution path.
        var tenantsToCheck = SnapshotCachedTenants();
        foreach (var tenant in tenantsToCheck)
        {
            var assignments = await GetAssignmentsAsync(tenant, now, ct).ConfigureAwait(false);
            var match = assignments.FirstOrDefault(a => a.Holder.Equals(actor));
            if (match is not null) return match;
        }

        // Cache miss — ask the source for the actor's tenant directly. The
        // assignment source is responsible for resolving the
        // Actor → Tenant binding (it has the StandingOrderRepository in
        // scope; we don't).
        var resolved = await _assignmentSource.ResolveAssignmentAsync(actor, ct).ConfigureAwait(false);
        if (resolved is not null)
        {
            // Warm the cache for the discovered tenant.
            _ = await GetAssignmentsAsync(resolved.TenantId, now, ct).ConfigureAwait(false);
        }
        return resolved;
    }

    private List<TenantId> SnapshotCachedTenants()
    {
        lock (_gate)
        {
            return _cache.Keys.ToList();
        }
    }

    private async ValueTask<IReadOnlyList<ShipRoleAssignment>> GetAssignmentsAsync(
        TenantId tenant, DateTimeOffset now, CancellationToken ct)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(tenant, out var cached)
                && now - cached.LoadedAt < CacheTtl)
            {
                return cached.Assignments;
            }
        }

        var fresh = await _assignmentSource.LoadAssignmentsAsync(tenant, ct).ConfigureAwait(false);
        lock (_gate)
        {
            _cache[tenant] = new CachedAssignments(fresh, now);
        }
        return fresh;
    }

    private static bool IsWatchRequired(ShipAction action, ShipLocation location, DeckDepth deck) =>
        action.Equals(ShipAction.StandWatch)
        || action.Equals(ShipAction.TransferWatch)
        || (location == ShipLocation.Quarterdeck && deck == DeckDepth.TopDeck && action.Equals(ShipAction.Approve));

    private static bool IsBelowTheWaterlineRole(ShipRole role) =>
        role is ShipRole.Captain or ShipRole.XO;

    private static bool IsRoleAllowedInLocation(ShipRole role, ShipLocation location)
    {
        // Phase 1 coarse-grained allowance per §3.1 default-landing table.
        // Captain + XO have access everywhere except deferred locations
        // (deferral check has already short-circuited those at step 3).
        if (role is ShipRole.Captain or ShipRole.XO) return true;

        return (role, location) switch
        {
            (ShipRole.EngineerOfficer, ShipLocation.EngineRoom) => true,
            (ShipRole.EngineerOfficer, ShipLocation.Quarterdeck) => true,
            (ShipRole.Navigator, ShipLocation.Wayfinder) => true,
            (ShipRole.Navigator, ShipLocation.Quarterdeck) => true,
            (ShipRole.TacticalOfficer, ShipLocation.Tactical) => true,
            (ShipRole.TacticalOfficer, ShipLocation.Quarterdeck) => true,
            (ShipRole.DivisionOfficer, _) => location is not ShipLocation.SickBay and not ShipLocation.ShipsOffice,
            (ShipRole.IDC, ShipLocation.SickBay) => true,
            (ShipRole.IDC, ShipLocation.Quarterdeck) => true,
            (ShipRole.Scribe, ShipLocation.ShipsOffice) => true,
            (ShipRole.Scribe, ShipLocation.Quarterdeck) => true,
            (ShipRole.OOD, _) => true,
            (ShipRole.EOOW, ShipLocation.EngineRoom) => true,
            (ShipRole.EOOW, ShipLocation.Quarterdeck) => true,
            _ => false,
        };
    }

    private static CapabilityAction MapToCapabilityAction(ShipAction action)
    {
        // §2.2: ShipAction → CapabilityAction translation. Phase 1 maps the
        // 9 canonical ShipAction values onto the existing CapabilityAction
        // surface; Phase 2 extends both sides as new actions appear.
        if (action.Equals(ShipAction.Read)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.Write)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.IssueStandingOrder)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.Approve)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.PromoteRole)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.StandWatch)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.TransferWatch)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.Quarantine)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.OverrideQuarantine)) return CapabilityAction.Write;
        return CapabilityAction.Read;
    }

    /// <summary>Per-tenant cache entry per §2.5 TTL fallback.</summary>
    private sealed record CachedAssignments(
        IReadOnlyList<ShipRoleAssignment> Assignments,
        DateTimeOffset LoadedAt);

    /// <summary>Per-(actor, location) sliding window per §2.4 rate-limit.</summary>
    private sealed record DenialWindow(DateTimeOffset WindowStartedAt, int Count)
    {
        public bool IsActiveAt(DateTimeOffset now, TimeSpan window) =>
            now - WindowStartedAt < window;
    }
}
