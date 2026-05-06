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
/// <b>Cache (per ADR 0077 §2.5):</b> Per-tenant 60-second TTL cache of role
/// assignments. When <c>IStandingOrderEventStream</c> is provided
/// (registered via <c>AddSunfishHelm()</c> or equivalent), the cache for the
/// affected tenant is invalidated immediately on each
/// <c>StandingOrderAppliedEvent</c> (subscribe-before-load; halt-condition C
/// resolved by W#57). When the event stream is NOT provided (e.g., in
/// isolated unit tests), the TTL behaviour applies.
/// </para>
/// <para>
/// <b>Rate-limiting (per §2.4):</b> Per-<c>(ActorId, ShipLocation)</c> denial
/// counter with 1-minute sliding window. When the counter exceeds N=10 within
/// the window: emit <see cref="AuditEventType.PermissionDenialRateExceeded"/>
/// once and short-circuit subsequent calls within the window with
/// <see cref="DenialReason.SecurityPolicyBlocked"/>.
/// </para>
/// </remarks>
public sealed class DefaultPermissionResolver : IPermissionResolver, IDisposable
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
            // ADR 0083 §4 — W#55 Ship's Office cohort extension. Role-
            // minimum enforcement (Scribe / XO+) is a Phase 2 follow-up:
            // the Phase 1 resolver gates on location + deck only;
            // ITenantSecurityPolicy (W#37) wires per-action role minimums.
            KeyValuePair.Create(ShipAction.ViewShipsOffice, DeckDepth.TopDeck),
            KeyValuePair.Create(ShipAction.EditShipsOfficeDocument, DeckDepth.TopDeck),
            KeyValuePair.Create(ShipAction.PublishShipsOfficeDocument, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.ArchiveShipsOfficeDocument, DeckDepth.MainDeck),
            // ADR 0079 §4 — W#50 Engine Room cohort extension. Role-minimum
            // enforcement (department-head / EngineerOfficer) is a Phase 2
            // follow-up gated on ITenantSecurityPolicy. Damage Control
            // operations (Quarantine / Release / Compact) sit at MainDeck
            // (NOT BelowTheWaterline) per ADR 0079 §4 — they are reversible
            // operations on a tenant-scoped CRDT, not destructive system-
            // level deletes.
            KeyValuePair.Create(ShipAction.ViewEngineRoom, DeckDepth.TopDeck),
            KeyValuePair.Create(ShipAction.ViewDamageControl, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.QuarantineDocument, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.ReleaseQuarantine, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.CompactDocument, DeckDepth.MainDeck),
            // ADR 0082 §5 — W#54 Sick Bay cohort extension. Role-minimum
            // enforcement (IDC / Captain / XO per §5 table) is a Phase 2
            // follow-up gated on ITenantSecurityPolicy.
            KeyValuePair.Create(ShipAction.ViewSickBay, DeckDepth.TopDeck),
            KeyValuePair.Create(ShipAction.ViewPharmacy, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.ManageRecoveryContacts, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.TriggerKeyRotation, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.InitiateMedevac, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.AuthorizeMedevac, DeckDepth.MainDeck),
            KeyValuePair.Create(ShipAction.ViewFirstAid, DeckDepth.TopDeck),
        });

    /// <summary>
    /// Resource-scoped actions per §2.0 — passing <c>resource: null</c> for
    /// any of these short-circuits to <see cref="DenialReason.SecurityPolicyBlocked"/>
    /// at step 0(c).
    /// </summary>
    /// <remarks>
    /// W#50 P1 pre-merge security council 2026-05-06 (Major M1): the W#50
    /// Damage Control actions (<see cref="ShipAction.QuarantineDocument"/>
    /// / <see cref="ShipAction.ReleaseQuarantine"/> /
    /// <see cref="ShipAction.CompactDocument"/>) target a per-document
    /// identifier on the command service; they are resource-scoped, so
    /// they MUST be in this set. The §Trust-elevated default is
    /// "deny on null resource" — Phase 2 wiring may synthesize a
    /// <see cref="Resource"/> from <c>documentId</c>, but the substrate-
    /// level guard closes the null-resource bypass at the resolver tier.
    /// Read-only Engine Room views (<see cref="ShipAction.ViewEngineRoom"/>
    /// / <see cref="ShipAction.ViewDamageControl"/>) stay location-scoped
    /// (correct) and are NOT in this set.
    /// </remarks>
    private static readonly ImmutableHashSet<ShipAction> ResourceScopedActions =
        ImmutableHashSet.CreateRange(new[]
        {
            ShipAction.Approve,
            ShipAction.Quarantine,
            ShipAction.OverrideQuarantine,
            // ADR 0079 §4 — W#50 Damage Control resource-scoped actions.
            ShipAction.QuarantineDocument,
            ShipAction.ReleaseQuarantine,
            ShipAction.CompactDocument,
            // ADR 0082 §5 — W#54 Sick Bay resource-scoped actions.
            // ManageRecoveryContacts targets a specific contact record;
            // TriggerKeyRotation targets a specific field-purpose. Both
            // are resource-scoped; null-resource short-circuits to
            // SecurityPolicyBlocked at step 0(c).
            ShipAction.ManageRecoveryContacts,
            ShipAction.TriggerKeyRotation,
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
    private readonly Dictionary<TenantId, Task<IReadOnlyList<ShipRoleAssignment>>> _inflightLoads = new();
    private readonly Dictionary<TenantId, int> _tenantInvalidationEpoch = new();
    private readonly IDisposable? _eventStreamSubscription;
    private bool _disposed;
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
    /// <param name="eventStream">
    /// Optional <see cref="IStandingOrderEventStream"/> for subscribe-before-load
    /// cache invalidation per W#46 halt-condition C (resolved by W#57).
    /// When provided, every <see cref="StandingOrderAppliedEvent"/> invalidates
    /// the affected tenant's cache (or all tenants for Platform-scoped events);
    /// when null, the 60s TTL behaviour applies as the sole staleness bound.
    /// </param>
    public DefaultPermissionResolver(
        IShipRoleAssignmentSource assignmentSource,
        IAuditTrail auditTrail,
        IOperationSigner signer,
        ILogger<DefaultPermissionResolver> logger,
        ICapabilityGraph? capabilityGraph = null,
        IShipActionMissionEnvelopeGate? envelopeGate = null,
        TimeProvider? timeProvider = null,
        int? denialRateLimit = null,
        IStandingOrderEventStream? eventStream = null)
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
        _eventStreamSubscription = eventStream?.Subscribe(OnStandingOrderApplied);
    }

    private void OnStandingOrderApplied(StandingOrderAppliedEvent e)
    {
        // W#46 halt-C: invalidate cache on Standing-Order applied events.
        // Scope-aware invalidation per shared-design-system-permres-cache-
        // invalidation-addendum:
        //  - Platform → all tenants (spans the local node)
        //  - Integration → no-op (integration-config doesn't touch role
        //    assignments or capability graph)
        //  - User / Tenant / Security → invalidate the event's tenant only
        //
        // Per W#46 P1b council M1: bump the per-tenant invalidation
        // epoch so an in-flight load that started BEFORE the event
        // arrives does not write its (potentially pre-applied) snapshot
        // back into the cache. ExecuteLoadAsync compares its captured
        // epoch against the current epoch under _gate and discards the
        // result on mismatch.
        lock (_gate)
        {
            switch (e.Scope)
            {
                case StandingOrderScope.Platform:
                    _cache.Clear();
                    _inflightLoads.Clear();
                    BumpAllEpochs();
                    break;
                case StandingOrderScope.Integration:
                    break;
                default:
                    _cache.Remove(e.TenantId);
                    _inflightLoads.Remove(e.TenantId);
                    BumpEpoch(e.TenantId);
                    break;
            }
        }
    }

    private void BumpEpoch(TenantId tenant)
    {
        _tenantInvalidationEpoch.TryGetValue(tenant, out var current);
        _tenantInvalidationEpoch[tenant] = unchecked(current + 1);
    }

    private void BumpAllEpochs()
    {
        var keys = _tenantInvalidationEpoch.Keys.ToArray();
        foreach (var k in keys)
        {
            _tenantInvalidationEpoch[k] = unchecked(_tenantInvalidationEpoch[k] + 1);
        }
    }

    private int CurrentEpoch(TenantId tenant)
    {
        _tenantInvalidationEpoch.TryGetValue(tenant, out var epoch);
        return epoch;
    }

    /// <summary>
    /// Unsubscribes from the optional <see cref="IStandingOrderEventStream"/>.
    /// Idempotent — safe to call multiple times. DI containers
    /// (Microsoft.Extensions.DependencyInjection) call this on singleton
    /// dispose at application shutdown.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _eventStreamSubscription?.Dispose();
    }

    /// <inheritdoc />
    public IReadOnlyList<ShipAction> AuditLoudActions => AuditLoudActionsImpl;

    /// <inheritdoc />
    public async ValueTask<PermissionDecision> ResolveAsync(
        TenantId tenantId,
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
                "Resource-scoped action requires a resource reference.",
                new Remediation(
                    RemediationKind.None,
                    "This action requires identifying the specific record being targeted.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
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
                "This action requires the on-watch designation (OOD or EOOW).",
                new Remediation(
                    RemediationKind.AwaitWatch,
                    "Wait for the next watch rotation, or contact the current Officer of the Deck.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
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
                        // §Remediation contract: CallToActionLabel MUST be
                        // null when both EscalationLink + ContactActor are
                        // null. The gate's verdict carries an upgrade label
                        // but no link target in Phase 1; downstream
                        // renderers MUST derive labels from RemediationKind
                        // until Phase 2 wires upgrade URIs from the
                        // edition layer (W#46/§Trust amendment 2026-05-06).
                        CallToActionLabel: null),
                    now);
                await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
                return denied;
            }
        }

        // §2.1 step 3 — Deferral check
        if (location == ShipLocation.SupplyOffice)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.Phase2Deferred,
                "Supply Office is deferred to Phase 2 commercial work.",
                new Remediation(
                    RemediationKind.Phase2Deferred,
                    "No current access path — Supply Office ships with the Phase 2 commercial release.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }
        if (location is ShipLocation.Wardroom or ShipLocation.Brig)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.V2Deferred,
                $"{location} is deferred to v2 (commercial agreement required).",
                new Remediation(
                    RemediationKind.None,
                    "No current access path — v2 commercial agreement required.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 4 — Role match. The caller passes tenantId explicitly
        // (W#46 P1 pre-merge security council 2026-05-06: see remarks on
        // IPermissionResolver.ResolveAsync); the resolver loads assignments
        // ONLY within tenantId to prevent cross-tenant authority bleed.
        var matched = await FindAssignmentAsync(tenantId, subjectActor, now, ct).ConfigureAwait(false);
        if (matched is null)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.NoMatchingRole,
                "No assigned role grants this action.",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact your tenant administrator to request a role assignment.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §1.6 — SUPPO is structurally valid but operationally inert until
        // Phase 2.
        if (matched.Role == ShipRole.SUPPO)
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.Phase2Deferred,
                "SUPPO role is structurally assigned but operationally deferred to Phase 2.",
                new Remediation(
                    RemediationKind.Phase2Deferred,
                    "No current access path — SUPPO ships operationally with the Phase 2 commercial release.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
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
                "Your role does not grant access at this location.",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact the location's department head to request scoped access.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
            return denied;
        }

        // §2.1 step 5 — Deck restriction
        if (effectiveDeck == DeckDepth.BelowTheWaterline && !IsBelowTheWaterlineRole(matched.Role))
        {
            var denied = new PermissionDecision.Denied(
                DenialReason.DeckRestriction,
                "Destructive (below-the-waterline) actions require Captain or XO authority.",
                new Remediation(
                    RemediationKind.ContactAuthority,
                    "Contact the Captain or Executive Officer to request a destructive-action elevation.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
            await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
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
                    "The capability graph does not record a grant for this subject + resource + action.",
                    new Remediation(
                        RemediationKind.ContactAuthority,
                        "Contact the resource owner to request a capability grant.",
                        ContactActor: null,
                        EscalationLink: null,
                        CallToActionLabel: null),
                    now);
                await EmitDenialAsync(tenantId, subjectActor, location, denied, action, now, ct).ConfigureAwait(false);
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
                "Self-promotion forbidden.",
                new Remediation(
                    RemediationKind.SecurityPolicyAppeal,
                    "Promotion must be requested from a higher-authority actor.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
                now);
        }
        if (AuthorityRank[callerRole] >= AuthorityRank[targetRole])
        {
            return new PermissionDecision.Denied(
                DenialReason.SecurityPolicyBlocked,
                "Insufficient authority to promote to target role.",
                new Remediation(
                    RemediationKind.SecurityPolicyAppeal,
                    "Promotion must be performed by an actor of strictly higher authority.",
                    ContactActor: null,
                    EscalationLink: null,
                    CallToActionLabel: null),
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
        TenantId tenantId, ActorId actor, ShipLocation location,
        PermissionDecision.Denied denied, ShipAction action,
        DateTimeOffset occurredAt, CancellationToken ct)
    {
        bool emitRateLimitRecord = false;
        DateTimeOffset windowStartAt = occurredAt;
        int denialCount = 0;

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

        var denialPayload = new AuditPayload(new Dictionary<string, object?>
        {
            ["action"] = action.Name,
            ["actor"] = actor.Value,
            ["decidedAt"] = denied.DecidedAt.ToString("O"),
            ["location"] = location.ToString(),
            ["reason"] = denied.Reason.ToString(),
            ["remediationKind"] = denied.Remediation.Kind.ToString(),
            ["severity"] = "Normal",
            ["tenantId"] = tenantId.Value,
        });
        await EmitAsync(AuditEventType.PermissionDenied, tenantId, denialPayload, occurredAt, ct).ConfigureAwait(false);

        if (emitRateLimitRecord)
        {
            var rateLimitPayload = new AuditPayload(new Dictionary<string, object?>
            {
                ["actor"] = actor.Value,
                ["denialCount"] = denialCount,
                ["location"] = location.ToString(),
                ["severity"] = "High",
                ["tenantId"] = tenantId.Value,
                ["windowStartedAt"] = windowStartAt.ToString("O"),
            });
            await EmitAsync(AuditEventType.PermissionDenialRateExceeded, tenantId, rateLimitPayload, occurredAt, ct).ConfigureAwait(false);
        }
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

    private async ValueTask<ShipRoleAssignment?> FindAssignmentAsync(
        TenantId tenantId, ActorId actor, DateTimeOffset now, CancellationToken ct)
    {
        // Tenant binding is the caller's responsibility (security council
        // 2026-05-06). Look up assignments only within the asserted tenant.
        var assignments = await GetAssignmentsAsync(tenantId, now, ct).ConfigureAwait(false);
        return assignments.FirstOrDefault(a => a.Holder.Equals(actor));
    }

    private async ValueTask<IReadOnlyList<ShipRoleAssignment>> GetAssignmentsAsync(
        TenantId tenant, DateTimeOffset now, CancellationToken ct)
    {
        // Cache-stampede protection per the W#46 P1 pre-merge security
        // council 2026-05-06: concurrent expired-TTL callers all observed
        // the stale cache and all hit the upstream source. Now: the first
        // thread under the lock installs a single in-flight Task (via TCS
        // — the TCS is registered BEFORE the load starts so the finally
        // cleanup cannot race ahead of the install on a synchronously-
        // completing source mock); every subsequent thread awaits the
        // same Task. Result is exactly one upstream load per TTL window.
        Task<IReadOnlyList<ShipRoleAssignment>> task;
        int loadEpoch;
        lock (_gate)
        {
            if (_cache.TryGetValue(tenant, out var cached)
                && now - cached.LoadedAt < CacheTtl)
            {
                return cached.Assignments;
            }
            loadEpoch = CurrentEpoch(tenant);
            if (!_inflightLoads.TryGetValue(tenant, out task!))
            {
                var tcs = new TaskCompletionSource<IReadOnlyList<ShipRoleAssignment>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                task = tcs.Task;
                _inflightLoads[tenant] = task;
                _ = ExecuteLoadAsync(tenant, now, loadEpoch, tcs, ct);
            }
        }
        return await task.ConfigureAwait(false);
    }

    private async Task ExecuteLoadAsync(
        TenantId tenant, DateTimeOffset now, int loadEpoch,
        TaskCompletionSource<IReadOnlyList<ShipRoleAssignment>> tcs,
        CancellationToken ct)
    {
        try
        {
            var fresh = await _assignmentSource.LoadAssignmentsAsync(tenant, ct).ConfigureAwait(false);
            lock (_gate)
            {
                // W#46 P1b council M1 — generation-counter race fix:
                // an applied-event arriving while this load was in-flight
                // bumps the tenant's invalidation epoch. If the epoch
                // changed since this load began, the snapshot is
                // potentially pre-applied state — DO NOT write it into
                // the cache. The next call observes the missing cache
                // entry and re-loads. The Task still resolves to the
                // fresh value so awaiting callers in this batch are
                // not blocked, but subsequent callers see an empty
                // cache and trigger a fresh load.
                if (CurrentEpoch(tenant) == loadEpoch)
                {
                    _cache[tenant] = new CachedAssignments(fresh, now);
                }
            }
            tcs.SetResult(fresh);
        }
        catch (OperationCanceledException oce)
        {
            tcs.SetCanceled(oce.CancellationToken);
        }
        catch (Exception ex)
        {
            tcs.SetException(ex);
        }
        finally
        {
            lock (_gate)
            {
                _inflightLoads.Remove(tenant);
            }
        }
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
        // W#46 P1 pre-merge security council 2026-05-06: throw on an
        // unmapped action rather than silently fall through to Read — a
        // future ShipAction added without updating this mapping must
        // surface as a build-time failure path, not a silent
        // capability-downgrade.
        if (action.Equals(ShipAction.Read)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.Write)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.IssueStandingOrder)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.Approve)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.PromoteRole)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.StandWatch)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.TransferWatch)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.Quarantine)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.OverrideQuarantine)) return CapabilityAction.Write;
        // ADR 0083 §4 — W#55 Ship's Office cohort extension.
        if (action.Equals(ShipAction.ViewShipsOffice)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.EditShipsOfficeDocument)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.PublishShipsOfficeDocument)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.ArchiveShipsOfficeDocument)) return CapabilityAction.Write;
        // ADR 0079 §4 — W#50 Engine Room cohort extension. Damage Control
        // ops (Quarantine / Release / Compact) map to Write — they mutate
        // tenant-scoped CRDT state.
        if (action.Equals(ShipAction.ViewEngineRoom)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.ViewDamageControl)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.QuarantineDocument)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.ReleaseQuarantine)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.CompactDocument)) return CapabilityAction.Write;
        // ADR 0082 §5 — W#54 Sick Bay cohort extension.
        if (action.Equals(ShipAction.ViewSickBay)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.ViewPharmacy)) return CapabilityAction.Read;
        if (action.Equals(ShipAction.ManageRecoveryContacts)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.TriggerKeyRotation)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.InitiateMedevac)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.AuthorizeMedevac)) return CapabilityAction.Write;
        if (action.Equals(ShipAction.ViewFirstAid)) return CapabilityAction.Read;
        throw new InvalidOperationException(
            $"unmapped ShipAction '{action.Name}' — update DefaultPermissionResolver.MapToCapabilityAction");
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
