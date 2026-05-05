---
id: 78
title: OOD Watch Rotation Primitive
status: Accepted
date: 2026-05-05
tier: foundation
pipeline_variant: sunfish-feature-change

concern:
  - configuration
  - audit
  - accessibility
  - security

enables:
  - ood-watch-rotation
  - quarterdeck-entry-point
  - eoow-analog

composes:
  - 65
  - 49
  - 46
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null
amendments: []
---

# ADR 0078 — OOD Watch Rotation Primitive

**Status:** Proposed
**Date:** 2026-05-05
**Authors:** XO research session
**Pipeline variant:** `sunfish-feature-change`
**Council posture:** standard adversarial + WCAG/a11y subagent (mandatory — OOD watch banner and
handover announcement are live-region-heavy per W#35 §5.1) + security-engineering subagent
(mandatory — OOD authority intersects security-critical Standing Order approval paths)

---

## Context

Sunfish has a role-assignment model (Department Head, Division Officer, etc.) but no concept of
*who is currently on watch*. In the naval analogy that drives the Sunfish Ship Architecture
(ADR 0065, W#35 discovery §6.7), the **Officer of the Deck (OOD)** is the qualified officer
currently standing watch — a temporal, rotatable designation that is orthogonal to their permanent
department-head assignment. Any qualified officer can be OOD for their shift; the designation
transfers via a formal handover.

For Sunfish, this maps directly onto **the currently-active admin**: in Phase 2 commercial scope,
a tenant may have multiple admins (e.g., spouses in joint property management, co-owners in a
business). At any moment, exactly one is the designated on-watch actor — the person who can
approve time-sensitive Standing Orders, whose name attaches to every Standing Order issued during
the watch, and whose presence is the liveness signal for the Quarterdeck UI. Without this
primitive, the Standing Order system (ADR 0065) has no answer to "who was responsible right now?"
distinct from "who has administrative authority by role."

The W#35 Ship Architecture discovery (§6.7 + §7.2) confirmed this is a genuine gap and filed the
OOD Watch Rotation intake as a first-class follow-on. The hard prerequisite — ADR 0065
(Wayfinder + Standing Order) — is Accepted and on `origin/main`.

A second analog — the **Engineering Officer of the Watch (EOOW)** — covers the Engine Room role.
In Sunfish terms, this is the currently-active infrastructure or integration operator for a tenant.
The EOOW role is structurally identical to OOD; this ADR introduces a shared primitive that
serves both.

---

## Decision drivers

1. **Standing Order provenance gap.** Every Standing Order in ADR 0065 is attributed to
   `ActorId IssuedBy`, but for multi-actor tenants there is no way to distinguish "issued by
   Alice during her authorized watch" from "issued by Alice at an arbitrary moment." The OOD
   designation closes this gap at the type level.

2. **Quarterdeck entry point is a hard consumer.** The `quarterdeck-entry-point-intake` filed
   after W#35 explicitly names OOD Watch Rotation as a hard prerequisite. Quarterdeck's
   UI is the live cockpit — the OOD-watch banner, the handover controls, and the watch-history
   timeline all require a stable `OodWatchId` / `IOodWatchRepository` surface before
   Quarterdeck can be authored.

3. **Multi-actor delegation correctness.** Phase 2 commercial scope (canonical test case: spouse
   50/50 + survivor recovery per ADR 0046) requires "which spouse is on watch?" to be
   unambiguous. Without an OOD primitive, the audit trail has no way to answer a dispute
   "who approved this tenant configuration change at 02:30?"

4. **Standing Order approval window.** ADR 0065 §ApprovalChain specifies multi-actor approval
   steps. The OOD is the default approver for Standing Orders that arrive during their watch;
   this ADR specifies how the watch state feeds into the approval routing.

5. **Audit-by-construction.** Watch start, handover, and TTL-expiry are all high-priority audit
   events. Correlating them to the same `OodWatchId` enables compliance queries of the form
   "list all Standing Orders issued during watch #X and by whom."

6. **WCAG 2.2 AA conformance.** Per W#35 §5.1 and the W#35 hardening pass: the OOD-watch
   banner is a live region (`aria-live="polite"`, downgraded to `"assertive"` only on handover
   announcements). Handover completion must announce the relieving actor's name without
   requiring focus change. This is a contract, not a goal.

7. **Failsafe TTL.** Watches must not silently remain active indefinitely. A configurable
   `MaxWatchDuration` (default 24 h) triggers `OodWatchExpired`, demoting the watch to
   `Expired` state and emitting a high-priority audit event. UI surfaces must treat `Expired`
   state as equivalent to "no active watch" — blocking Standing Order issuance until the watch
   is formally re-started.

---

## Considered options

### Option A — OOD as a role flag on `ActorId`

Add an `IsOnWatch: bool` property to the existing principal/actor surface. Simple; no new type.

**Pro:** minimal new surface.
**Con:** conflates current watch state with identity; a boolean does not capture watch
  history, handover chain, `OodWatchId`, or TTL. The audit trail loses the ability to correlate
  "all Standing Orders during watch #X." Breaks when two actors are simultaneously on watch for
  different scopes (OOD for tenant A; not on watch for tenant B).
**Verdict:** rejected.

### Option B — OOD as a special StandingOrder scope

Extend `StandingOrderScope` (ADR 0065) with an `OodWatch` value; model watch start/handover
as Standing Orders on path `coordination/ood-watch/{role}`.

**Pro:** re-uses the Standing Order machinery; no new types.
**Con:** watch state is not a configuration change — it is an operational state. Conflating the
  two pollutes the Atlas projector (which materialized-views the configuration state) with
  ephemeral operational records. The `OodWatchId` concept — needed for correlation — still
  cannot be a `StandingOrderId` without circular dependency. CRDT merge semantics for
  concurrent watch-starts are undefined under the current ADR 0065 CRDT policy.
**Verdict:** rejected for the primary data model; RETAINED as the mechanism for emitting the
  watch-transfer notification into the Wayfinder Standing Order log (see §Watch-handover
  notification below).

### Option C — First-class `OodWatch` record in `foundation-wayfinder` **[RECOMMENDED]**

Introduce `OodWatch` as a first-class record in the `foundation-wayfinder` package (the same
package that owns `StandingOrder`). The watch state is stored in a dedicated
`IOodWatchRepository`; watch transitions are atomic; a failsafe TTL triggers `OodWatchExpired`
audit emission. A watch-transfer event is *also* emitted as a Standing Order notification into
the Wayfinder log (using the Option B path as a secondary notification side-effect, not the
primary store).

**Pro:** clean separation of concerns; `OodWatchId` is a stable correlation key across audit
  events and Standing Orders; multi-tenant isolation is natural (one watch per
  `(TenantId, OodRole)` pair); TTL is first-class; WCAG live-region contract is anchored to the
  watch-state transition events.
**Con:** new types; cross-package wiring between `foundation-wayfinder` watch state and the
  Standing Order approval path.
**Verdict:** recommended.

---

## Decision

**Adopt Option C** — `OodWatch` as a first-class record in `foundation-wayfinder`, with
`IOodWatchRepository` as the canonical state interface and `IOodWatchService` as the
issuance/handover service. Watch-transfer notifications flow through the Standing Order log as
a secondary side-effect.

### §1 Data model

Located in `Sunfish.Foundation.Wayfinder` (`packages/foundation-wayfinder/`):

```csharp
// Primary watch record
public sealed record OodWatch(
    OodWatchId Id,
    TenantId TenantId,
    ActorId OnWatchActor,
    OodRole Role,
    NodaTime.Instant StartedAt,
    NodaTime.Instant? RelievedAt,
    ActorId StartedBy,
    ActorId? RelievedBy,
    TimeSpan MaxWatchDuration,   // failsafe TTL; default 24h
    OodWatchState State
);

public readonly record struct OodWatchId(Guid Value);

public enum OodRole
{
    OfficerOfTheDeck,           // Quarterdeck + general tenant admin
    EngineeringOfficerOfTheWatch // Engine Room + infrastructure/integration operator
}

public enum OodWatchState
{
    Active,     // watch is in progress
    Relieved,   // formally handed over (has RelievedAt + RelievedBy)
    Expired     // MaxWatchDuration exceeded; treated as "no active watch" by UI
}
```

**Multi-tenant isolation:** the system enforces at most one `Active` watch per
`(TenantId, OodRole)` pair. Attempting to start a watch when one is already active for that
pair without a formal handover is rejected by `IOodWatchService.StartWatchAsync` with
`OodWatchConflictException`.

**OodWatchId linkage to StandingOrder:** ADR 0065's `StandingOrder` record is extended with a
new optional field:

```csharp
// Extension to StandingOrder (additive; null for orders issued before ADR 0078 build)
OodWatchId? IssuedDuringWatchId = null
```

This is an **additive extension** to ADR 0065's data model — existing `StandingOrder`
instances with `null` are valid. `IOodWatchService` populates this field automatically when an
active watch exists at the moment of Standing Order issuance.

**Binary-compatibility halt-condition:** `StandingOrder` is a `sealed record` with a positional
primary constructor. Adding `IssuedDuringWatchId` to the constructor is a **breaking binary
change** to any published binary. This field MUST be added inside the W#42 Stage 06 build
(Phase 1 PR), BEFORE W#42 ships any NuGet binary. If W#42 has already published a binary by
the time W#49 Stage 06 begins, an ADR 0065 amendment is required before proceeding.

### §2 Repository and service contracts

```csharp
public interface IOodWatchRepository
{
    ValueTask<OodWatch?> GetCurrentWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);

    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId startedBy, CancellationToken ct = default);

    ValueTask<OodWatch> RelieveWatchAsync(
        OodWatchId watchId, ActorId relievedBy, CancellationToken ct = default);

    ValueTask<OodWatch> ExpireWatchAsync(
        OodWatchId watchId, CancellationToken ct = default);

    IAsyncEnumerable<OodWatch> GetWatchHistoryAsync(
        TenantId tenantId, OodRole role,
        NodaTime.Instant from, NodaTime.Instant to,
        CancellationToken ct = default);

    // Returns all Active watches whose StartedAt + MaxWatchDuration <= cutoff.
    // Used by IOodWatchExpiryService to drive the TTL sweep across all tenants.
    IAsyncEnumerable<OodWatch> GetExpiredCandidatesAsync(
        NodaTime.Instant cutoff, CancellationToken ct = default);
}

public interface IOodWatchService
{
    /// <summary>
    /// Start a new watch. OodWatchId is server-generated; callers MUST NOT supply it.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// Thrown when an Active watch already exists for the given (TenantId, OodRole) pair.
    /// </exception>
    ValueTask<OodWatch> StartWatchAsync(
        TenantId tenantId, ActorId onWatchActor, OodRole role,
        TimeSpan? maxDuration, ActorId requestedBy, CancellationToken ct = default);

    /// <summary>
    /// Formal handover: relieves the current watch and starts a new one atomically.
    /// </summary>
    /// <exception cref="OodWatchConflictException">
    /// Thrown if currentWatchId is not in Active state at the time of the call.
    /// </exception>
    ValueTask<(OodWatch Relieved, OodWatch Started)> HandoverWatchAsync(
        OodWatchId currentWatchId, ActorId incomingActor,
        ActorId requestedBy, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Returns the Active watch for the given (tenant, role) pair, or null if none.
    /// Returns null for Relieved or Expired watches — callers MUST NOT infer "no watch
    /// ever existed" from a null return.
    /// </summary>
    ValueTask<OodWatch?> GetActiveWatchAsync(
        TenantId tenantId, OodRole role, CancellationToken ct = default);
}
```

**Persistence uniqueness contract:** the repository implementation MUST enforce a
database-level uniqueness constraint on `(TenantId, OodRole)` filtered to `State = Active`
(e.g., a partial unique index). Optimistic concurrency on `OodWatch` rows is the required
secondary defense. Concurrent `StartWatchAsync` calls on the same pair MUST fail-one-atomically
at the persistence layer — in-memory locking alone is insufficient.

### §3 Watch-handover notification via Standing Order log

When `HandoverWatchAsync` completes, `IOodWatchService` emits a notification Standing Order
on path `coordination/ood-watch/{role}/transfer` into the Wayfinder Standing Order log. This
Standing Order uses `StandingOrderScope.Tenant` (the handover is per-tenant; `Platform` scope
would imply a cross-tenant signal which is incorrect here) and has a single triple:

```csharp
new StandingOrderTriple(
    Path: $"coordination/ood-watch/{role.ToString().ToLowerInvariant()}/transfer",
    OldValue: JsonValue.Create(currentWatchId.Value.ToString()),
    NewValue: JsonValue.Create(startedWatchId.Value.ToString()))
```

This is a **notification only** — the authoritative watch state is in `IOodWatchRepository`,
not in the Atlas projector. The Standing Order gives the Wayfinder log a durable audit trail
of handovers and enables live-region UI updates via the same subscription path as all other
Standing Order notifications.

### §4 Audit event types

This ADR introduces **3 new `AuditEventType` static-readonly constants** on
`Sunfish.Kernel.Audit.AuditEventType` (a `readonly record struct(string Value)` per ADR 0049;
same pattern as `StandingOrderIssued` / `KeyRecoveryInitiated` etc.):

```csharp
// added to Sunfish.Kernel.Audit.AuditEventType
public static readonly AuditEventType OodWatchStarted  = new("OodWatchStarted");
public static readonly AuditEventType OodWatchRelieved = new("OodWatchRelieved");
public static readonly AuditEventType OodWatchExpired  = new("OodWatchExpired");
```

All three carry `OodWatchId`, `TenantId`, `OodRole`, and the relevant `ActorId` fields in
their `JsonNode Payload`. `OodWatchExpired` additionally carries `MaxWatchDuration` and
`ExpiredAt` in payload. Audit emission follows ADR 0049 contract:
`IAuditTrail.AppendAsync(record, ct)`.

`OodWatchStarted` and `OodWatchExpired` are emitted with `"severity": "High"` in the
`JsonNode Payload`; `OodWatchRelieved` with `"severity": "Normal"`. These are literal string
values in the payload JSON — there is no sentinel enum (ADR 0049 does not define one). The
severity field is advisory for UI/alert filtering and monitoring dashboards.

### §5 TTL failsafe

`IOodWatchService` implementations must provide a background expiry sweep. Recommended pattern:
a hosted service (`IOodWatchExpiryService`) polls at a configurable interval (default 5 min),
calls `IOodWatchRepository.GetExpiredCandidatesAsync(now)` (all-tenants all-roles query — §2),
and calls `ExpireWatchAsync` for each returned watch. This is an implementation concern, not a
contract; the ADR specifies the observable behavior: `OodWatchExpired` MUST be emitted before
or at the moment the watch state is set to `Expired`.

Unit tests for the expiry service MUST disable or mock the timer to avoid non-determinism.
The `IHostedService.StartAsync` / `StopAsync` lifecycle is the recommended seam.

### §6 Standing Order approval path composition

`IOodWatchService.GetActiveWatchAsync(tenantId, OodRole.OfficerOfTheDeck)` is the query a
Standing Order approval router calls to determine whether the current approver is the on-watch
OOD. **This ADR does not specify the approval routing policy** — that belongs to ~ADR 0068
(Tenant Security Policy). This ADR specifies the query surface that the approval router will
use.

### §7 WCAG 2.2 AA conformance contract

Per W#35 §5.1 and the W#34 WCAG/a11y hardening pass:

- The OOD watch banner MUST be rendered as an `aria-live="polite"` region.
- Watch **handover** completion MUST announce
  `"Watch relieved. {Role.DisplayName} is now {IncomingActor.DisplayName}."` as an
  `aria-live="polite"` announcement (handover is a planned, non-urgent transition — `assertive`
  would interrupt ongoing SR utterance unnecessarily), triggered once per handover event,
  without requiring focus change.
- Watch **expiry** MUST announce
  `"Watch expired. No {Role.DisplayName} is currently on watch."` as an
  `aria-live="assertive"` announcement (expiry is an operational state degradation; subsequent
  Standing Order issuance is blocked — user must be interrupted).
- `{Role.DisplayName}` resolves to a per-role localized string resource ("Officer of the Deck"
  for `OodRole.OfficerOfTheDeck`; "Engineering Officer of the Watch" for `OodRole.EngineeringOfficerOfTheWatch`).
  Hard-coding "Officer of the Deck" in the announcement string is forbidden.
- The handover control MUST be operable by keyboard and pointer; no gesture-only path.
- Error prevention: confirming a formal handover requires a verification step
  (WCAG 2.2 SC 3.3.7 Error Prevention — Legal, Financial).

#### §7.1 Handover confirmation dialog a11y contract

The handover confirmation dialog MUST conform to:

- `role="dialog"` (not `alertdialog` — handover is an intentional, non-emergency action)
- `aria-labelledby` references a heading element naming the incoming actor (e.g., "Hand watch
  to {IncomingActor.DisplayName}")
- `aria-describedby` references the watch-summary text (current role, expected watch duration)
- Focus moves to the primary confirmation button when the dialog opens
- Esc key and the Cancel button both close the dialog without committing; focus returns to
  the "Begin Handover" trigger
- The confirmation button text MUST be action-specific ("Confirm Handover"), not generic ("OK")

These conformance requirements are authored here as a contract for downstream UI implementors
(Quarterdeck, Engine Room adapters). WCAG subagent dispatch is mandatory for any Stage 06
build that ships the watch-banner UI.

### §8 Package location

Types introduced by this ADR are located in **`packages/foundation-wayfinder/`**, the same
package that owns `StandingOrder`. No new package is created. The new surface is a cohesive
extension of the Wayfinder domain.

### §A0 Cited-symbol audit

#### §A0.1 Pre-acceptance negatives (symbols NOT introduced by this ADR)

- `Sunfish.Foundation.Wayfinder.StandingOrder` — `packages/foundation-wayfinder/` (ADR 0065
  Accepted; introduced by W#42 build). **NB:** W#42 is `ready-to-build`, not yet `built`. If
  the COB build of W#42 has not yet landed when W#78 Stage 06 begins, the implementer must
  treat `StandingOrder` as a forthcoming type and stub-reference it.
- `Sunfish.Foundation.Assets.Common.TenantId` — `packages/foundation/Assets/Common/TenantId.cs`
  (verified by ADR 0065-A1 §A0.2 council correction; origin/main).
- `Sunfish.Foundation.Assets.Common.ActorId` — `packages/foundation/Assets/Common/ActorId.cs`
  (verified by ADR 0065-A1 §A0.2 council correction; origin/main).
- `Sunfish.Kernel.Audit.AuditEventType` — `readonly record struct(string Value)` in
  `packages/kernel-audit/` (verified; ADR 0049 substrate; PR #... merged).
- `Sunfish.Kernel.Audit.IAuditTrail.AppendAsync(AuditRecord record, CancellationToken ct)`
  — verified signature in `packages/kernel-audit/` per ADR 0049 §Implementation checklist
  (lines 119-123).
- `IOperationSigner` (referenced in §Trust impact) — verified at
  `packages/foundation/Crypto/IOperationSigner.cs`; `SignAsync<T>(T payload, DateTimeOffset issuedAt, Guid nonce, ...)` exists with native nonce and timestamp binding.
- `NodaTime.Instant` — verified external dependency already in use across foundation packages.

#### §A0.2 Symbols introduced by this ADR (not yet on origin/main)

The following symbols do NOT exist yet and are created by the W#49 Stage 06 build:
`OodWatch`, `OodWatchId`, `OodRole`, `OodWatchState`, `IOodWatchRepository`,
`IOodWatchService`, `OodWatchConflictException`, `OodWatchExpiryService`,
`AuditEventType.OodWatchStarted`, `AuditEventType.OodWatchRelieved`,
`AuditEventType.OodWatchExpired`.

The `StandingOrder.IssuedDuringWatchId` field addition is also introduced by this ADR's Stage 06
build, as an additive extension to ADR 0065's `StandingOrder` record (null-safe; backwards
compatible).

#### §A0.3 Structural citation correctness

- `StandingOrderTriple(string Path, JsonNode? OldValue, JsonNode? NewValue)` — verified against
  ADR 0065 Decision §1 (exact constructor shape). Confirmed `JsonNode?` (nullable) for both
  value fields.
- `StandingOrderScope { User, Tenant, Platform, Integration, Security }` — verified enum values
  in ADR 0065. The notification Standing Order in §3 uses `StandingOrderScope.Tenant` (the
  handover is per-tenant; council amended from initial `Platform` draft).
- `AuditEventType` as `readonly record struct(string Value)` — verified in ADR 0049 and
  confirmed by ADR 0065 §Implementation checklist (same static-field constant pattern as
  `StandingOrderIssued`).

---

## Consequences

### Positive

- `OodWatchId` becomes the durable cross-entity correlation key for Standing Orders, audit
  events, and Quarterdeck UI state — answering "who was on watch when this happened?" from
  any query path.
- Multi-actor tenants (spouse-recovery, co-owner, vendor co-coordinator) have a clean
  "currently active admin" signal without modifying `ActorId` or role-assignment surfaces.
- The TTL failsafe prevents watches from becoming stale without operator notice; `OodWatchExpired`
  is a clear alert signal for monitoring dashboards (Engine Room in future).
- Quarterdeck can be authored as a straightforward consumer of `IOodWatchService` without
  needing to invent its own watch-state model.

### Negative

- `StandingOrder` gains a nullable `OodWatchId?` field. All existing Standing Order construction
  call-sites must pass `null` explicitly or use the default. Additive; no breaking change on
  the binary surface, but any code doing exhaustive positional construction will need updating.
- The `IOodWatchExpiryService` background sweep adds a hosted service to the DI container.
  This is a minor startup cost; tests must mock or disable the sweep to avoid timer-induced
  non-determinism.
- If W#42 (foundation-wayfinder build) has not yet landed when the W#49 Stage 06 build begins,
  the implementer must stub-reference `StandingOrder` types and compile the OOD types
  independently, then integrate when W#42 is available.

### Trust impact / Security & privacy

OOD designation represents an elevated operational authority: the on-watch actor can approve
Standing Orders within the approval-routing rules that ~ADR 0068 will define. An attacker who
can forge or replay an OOD-start event could escalate authority for the duration of the watch.

Mitigations (required by implementation):
1. **`StartWatchAsync` and `HandoverWatchAsync` MUST require an attesting signature** from the
   requesting actor before mutating watch state. The signature uses
   `IOperationSigner.SignAsync<T>` (verified at `packages/foundation/Crypto/IOperationSigner.cs`)
   which natively binds `issuedAt: DateTimeOffset` and `nonce: Guid` into the canonical-JSON
   form per ADR 0046. The signed payload contains `(TenantId, OodRole, OodWatchId)`.
   `OodWatch.StartedAt` is set server-side to the envelope's `issuedAt`; the verifier MUST
   reject any envelope whose `issuedAt` is outside ±5 min of server clock to bound clock-skew
   replay. Implementations MUST NOT roll their own canonical message form outside of
   `IOperationSigner`.
2. **`OodWatchId` MUST be server-generated** (a fresh `Guid.NewGuid()` on the server path, not
   caller-supplied) as documented in `IOodWatchService.StartWatchAsync`'s XML doc (§2).
3. Watch state transitions MUST be serialized per `(TenantId, OodRole)` at the **database
   level** via a partial unique index on `State = Active`, as stated in §2's persistence
   uniqueness contract. In-memory locking alone is insufficient.
4. Security-engineering subagent review is **mandatory** before PR merge (per this ADR's council
   posture declaration).

---

## Compatibility plan

**`foundation-wayfinder`** (extended; same package as ADR 0065 types):
- New types: `OodWatch`, `OodWatchId`, `OodRole`, `OodWatchState`, `IOodWatchRepository`,
  `IOodWatchService`, `OodWatchConflictException`
- New field on `StandingOrder`: `OodWatchId? IssuedDuringWatchId = null` (additive; nullable)
- New hosted service: `OodWatchExpiryService` (internal; registered via DI extension update)

**`kernel-audit`** (extended; same package as ADR 0049 types):
- 3 new `AuditEventType` static-readonly constants

**All other packages:** no changes required. The `StandingOrder` extension is backwards
compatible. Existing callers of `IStandingOrderIssuer.IssueAsync` do not need to change unless
they explicitly construct `StandingOrder` records directly (implementation-internal only).

**W#42 sequencing note:** W#49 Stage 06 build MUST wait until W#42 Phase 1 has landed (so
`StandingOrder` type exists in `foundation-wayfinder`). If COB is building both in parallel,
W#49 Phase 1 (OOD types) can proceed first; the `StandingOrderTriple` notification wiring in
W#49 Phase 2 must wait for W#42 Phase 1.

---

## Implementation checklist

### Phase 1 — OOD substrate types + audit constants (~2–3h; 1 PR; `sunfish-feature-change`)

- [ ] Add `OodWatchId`, `OodRole`, `OodWatchState`, `OodWatch` records to
  `packages/foundation-wayfinder/`
- [ ] Add `OodWatchConflictException` (inherits `InvalidOperationException`; carries
  `ExistingWatchId: OodWatchId` and `TenantId` properties)
- [ ] Add `IOodWatchRepository` interface to `packages/foundation-wayfinder/`
- [ ] Add `IOodWatchService` interface to `packages/foundation-wayfinder/`
- [ ] Add 3 new `AuditEventType` constants to `packages/kernel-audit/` source:
  `OodWatchStarted`, `OodWatchRelieved`, `OodWatchExpired`
- [ ] Add `OodWatchId? IssuedDuringWatchId = null` field to `StandingOrder` record in
  `packages/foundation-wayfinder/` (additive; positional-constructor extension)
  **Halt-condition (binary compat):** this MUST be added inside the W#42 Stage 06 Phase 1 PR,
  BEFORE W#42 ships any NuGet binary. If W#42 has already published a binary, stop and file an
  ADR 0065 amendment before proceeding.
  **Halt-condition (W#42 not yet built):** if W#42 Phase 1 has not yet landed, `StandingOrder`
  does not exist yet — defer this checkbox to W#42 Phase 1 and coordinate with XO
- [ ] Update DI extension `AddSunfishWayfinder()` to register `IOodWatchService` (impl stub
  in Phase 1; full impl in Phase 2)
- [ ] Verify all new types compile clean with `dotnet build packages/foundation-wayfinder/`
- [ ] Pre-merge council (mandatory per ADR 0069 D1; security-engineering subagent required
  for `OodWatch` + signing requirements)

### Phase 2 — `IOodWatchService` implementation + audit emission (~3–4h; 1 PR)

- [ ] Implement `DefaultOodWatchService : IOodWatchService` in `packages/foundation-wayfinder/`
- [ ] `StartWatchAsync`: validates signature; checks for existing active watch (throws
  `OodWatchConflictException` if one exists); persists via `IOodWatchRepository`; emits
  `OodWatchStarted` audit event
- [ ] `HandoverWatchAsync`: atomically relieves current + starts new watch; emits
  `OodWatchRelieved` (old) + `OodWatchStarted` (new) audit events; emits watch-transfer
  notification Standing Order via `IStandingOrderIssuer` (path §3)
  **Halt-condition:** `IStandingOrderIssuer` only available after W#42 Phase 2 — implement the
  audit emission; stub out the Standing Order notification until W#42 Phase 2 lands
- [ ] `GetActiveWatchAsync`: returns `Active` watch or null
- [ ] `OodWatchExpiryService` (hosted): polls `IOodWatchRepository.GetWatchHistoryAsync` for
  `Active` watches past TTL; calls `ExpireWatchAsync`; emits `OodWatchExpired` audit event
- [ ] Unit tests (8 minimum):
  - [ ] StartWatch_Succeeds_EmitsAuditEvent
  - [ ] StartWatch_ConflictThrows_WhenActiveExists
  - [ ] HandoverWatch_AtomicRelieveAndStart
  - [ ] HandoverWatch_EmitsBothAuditEvents
  - [ ] GetActiveWatch_ReturnsNull_WhenNoneActive
  - [ ] GetActiveWatch_ReturnsExpired_AsNull_ToCallers (confirm UI contract)
  - [ ] ExpiryService_SetsExpiredState_EmitsAuditEvent
  - [ ] StandingOrder_IssuedDuringWatchId_PopulatedWhenActive
- [ ] Pre-merge council (mandatory; security-engineering subagent re-review for signing
  implementation)

### Phase 3 — Wiring + apps/docs (~1–2h; 1 PR)

- [ ] `apps/docs/foundation/wayfinder/ood-watch.md` — usage guide for Quarterdeck implementors
- [ ] XML docs on all public types and interface members in `foundation-wayfinder/`
- [ ] Changelog entry (user-facing description)
- [ ] Update `icm/_state/active-workstreams.md` W#49 row to `built`
- [ ] Standard review (no council required for docs-only PR)

---

## Open questions

1. **EOOW scope.** The intake calls out `OodRole.EngineeringOfficerOfTheWatch` as the Engine
   Room analog. In Phase 1, the same `OodWatch` type serves both roles (differentiated by the
   `OodRole` field). The Engine Room UI is a separate workstream (~ADR engine-room-observability-intake);
   this ADR does not scope Engine Room UI. The implementer need not wait for that ADR.

2. **`IOodWatchRepository` persistence.** This ADR specifies the repository interface but not
   the persistence substrate. The recommended approach: store watch records in the same EFCore
   `DbContext` as the foundation-wayfinder package's other state, with a partial unique index
   on `(TenantId, OodRole)` filtered to `State = Active` (required by §2 persistence
   uniqueness contract). This is a Stage 06 decision for COB; no ADR amendment required unless
   a non-EFCore substrate is chosen.

3. **`OodWatchConflictException` base class.** §1 proposes `InvalidOperationException` as the
   parent. If `Sunfish.Foundation` already defines a `SunfishDomainException` base, prefer
   that for consistent catch-block behavior across domain boundaries. Verify before authoring
   Phase 1. If no domain base exists, `InvalidOperationException` is acceptable.

---

## Revisit triggers

- **~ADR 0068 (Tenant Security Policy) is authored** — review approval-routing composition in §6;
  this ADR may need an amendment to align the `IOodWatchService.GetActiveWatchAsync` contract
  with whatever approval-gate interface ADR 0068 defines.
- **First regulated-SMB tenant onboards under Bridge** — the WCAG 2.2 AA and EN 301 549
  procurement requirements in §7 become customer-contractual; re-audit conformance.
- **ADR 0004 algorithm-agility refactor** — `AuditRecord.AttestingSignatures` format may change;
  watch-start audit records are long-retained; re-evaluate whether watch-event audit records
  need a `v1` envelope tag at that time.
- **Phase 2 commercial scope ships** — once spouse / co-owner multi-actor delegation is live,
  run a threat-model review of OOD designation as an authority-escalation surface; add
  penetration test scenarios for watch-start forgery.

---

## Pre-acceptance audit

- [x] **AHA pass.** Options A (role flag) and B (StandingOrder scope) considered and rejected.
  Option B retained as a secondary side-channel (notification Standing Order) rather than the
  primary store.
- [x] **FAILED conditions.** This decision should be reversed if: (a) ADR 0068 Tenant Security
  Policy defines an incompatible approval-gate interface that cannot consume
  `IOodWatchService.GetActiveWatchAsync`, or (b) the signing API required in §Trust impact
  does not exist in `packages/foundation/Crypto/` and cannot be introduced without a major
  ADR 0046 amendment that is out of scope.
- [x] **Rollback strategy.** The `StandingOrder.IssuedDuringWatchId` field addition is
  backwards-compatible (nullable; default null). All other types are new and can be removed by
  reverting the W#49 Stage 06 build PRs. The 3 `AuditEventType` constants are additive and
  their removal does not break callers.
- [x] **Confidence level.** HIGH. ADR 0065 types are Accepted and on main; ADR 0049 audit
  substrate is built; the OOD pattern has a clear naval analog that maps cleanly to the
  Sunfish multi-actor tenant problem.
- [x] **Cited-symbol verification.** See §A0. All cited existing symbols verified against
  origin/main. New symbols explicitly marked "introduced by this ADR" in §A0.2.
- [x] **Anti-pattern scan.** AP-1 (unvalidated assumptions): §A0 covers; AP-3 (vague success
  criteria): implementation checklist is observable; AP-21 (cited-symbol drift): §A0 covers.
  No critical anti-patterns apply.
- [x] **Revisit triggers.** Named in §Revisit triggers above.
- [x] **Cold Start Test.** A fresh contributor can execute all three phases from the checklist
  with the halt-conditions as their guidance for sequencing relative to W#42.

---

## References

### Predecessor and sister ADRs

- [ADR 0065](./0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder + Standing
  Order Contract; defines `StandingOrder`, `StandingOrderId`, `StandingOrderTriple`,
  `ActorId`, `TenantId` shapes this ADR extends
- [ADR 0049](./0049-audit-trail-substrate.md) — Audit Trail Substrate; defines `IAuditTrail`,
  `AuditRecord`, `AuditEventType` this ADR emits into
- [ADR 0046](./0046-key-loss-recovery-scheme-phase-1.md) — key-loss recovery and identity
  primitives; informs §Trust impact signing requirements

### Roadmap and specifications

- W#35 Ship Architecture discovery §6.7 + §7.2 + §8.1 —
  `icm/01_discovery/output/2026-05-01_ship-architecture.md`
- OOD Watch Rotation intake —
  `icm/00_intake/output/2026-05-01_ood-watch-rotation-intake.md`
- Quarterdeck Entry Point intake (hard consumer) —
  `icm/00_intake/output/2026-05-01_quarterdeck-entry-point-intake.md`
- WCAG 2.2 AA (2018) — Success Criteria 3.3.7 Error Prevention (Legal, Financial) + live-region
  pattern

### Existing code / substrates

- `packages/foundation-wayfinder/` — host package for new types (W#42 build; `ready-to-build`)
- `packages/foundation/Assets/Common/TenantId.cs` — TenantId value type
- `packages/foundation/Assets/Common/ActorId.cs` — ActorId value type
- `packages/kernel-audit/` — IAuditTrail + AuditEventType + AuditRecord
