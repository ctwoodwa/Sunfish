# Intake Note — Foundation Multi-Tenancy Type Surface Convention

**Status:** `design-in-flight` — Stage 00 intake widened; Stage 01 Discovery not yet started. **sunfish-PM: do not build against this intake's design until status flips to `ready-to-build` or a hand-off file appears in `icm/_state/handoffs/`.** Tier 1 retrofit on PR #190 has its own hand-off (`kernel-audit-tier1-retrofit.md`) that is independently `ready-to-build`.
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Architecture conversation 2026-04-28 — kernel-audit v0 surface design under ADR 0049; Phase 2 commercial-MVP multi-tenant view workflows; medical-provider worked example as stress test.
**Pipeline variant:** `sunfish-feature-change` (escalates to `sunfish-api-change` if `IMayHaveTenant` is removed or `TenantSelection` displaces existing `TenantId?` query parameters as a contract change).
**Blocks:** kernel-audit v0 scaffolding (ADR 0049 Implementation checklist) — held by user direction pending this decision.
**Scope history:** Originated as a TenantId sentinel-pattern intake (2026-04-28). Widened the same day to include `TenantSelection` after Phase 2 multi-tenant view workflows + a medical-provider worked example showed cross-tenant queries are common, not privileged-rare. Filename retained for traceability; title updated to reflect widened scope.

## Problem Statement

Sunfish's foundation multi-tenancy types have two shape problems that surfaced together while designing the kernel-audit v0 query surface (ADR 0049). The questions are orthogonal but share the same foundation multi-tenancy type surface — pinning them together at Stage 02 keeps that surface coherent.

### Problem 1 — Tenant identity has a split-personality pattern

Two parallel patterns exist for "this entity may not have a real tenant":

1. **Value-object sentinel pattern.** `TenantId.Default = new("default")` is treated as a sentinel meaning "system / no explicit tenant" by `Sunfish.Foundation.Assets.Audit.NullAuditContextProvider`. Used across 16+ sites; informally established but unformalized.
2. **Nullable / interface-marker pattern.** `IMayHaveTenant` interface defined in `foundation-multitenancy/ITenantScoped.cs` plus `TenantId?` nullable parameters/properties across 13 sites in foundation, foundation-integrations, foundation-featuremanagement, and foundation-localfirst.

These patterns do the same job through different mechanisms; choosing one for kernel-audit forced the upstream question of which is canonical.

### Problem 2 — No first-class shape for multi-tenant queries

Many real workflows span multiple tenants and are common, not privileged-rare:

- **Phase 2 commercial scope** (per `project_phase_2_commercial_scope` memory): consolidated P&L across 4 property LLCs + holding co (monthly); spouse co-owner dashboard (daily); bookkeeper view across all entities BK has access to (daily); tax-advisor consolidated annual export.
- **Medical-provider worked example** (stress test for the design): traveling provider rotates clinics weekly; today's 10 patient assignments change daily; provider's personal audit log spans tenants over time; "show me my work this week" needs to join records across the active and recently-active clinics.

These scenarios are not solved by sentinels. A `TenantId.Guest`-style sentinel cannot express *"records for tenants X, Y, Z"* or *"all tenants this principal has access to right now."* That is a SET shape, not a value-object identity.

The current "fix" — making query types take `TenantId?` and treating null as "any tenant" — conflates record-tenant identity with query-tenant scope, which have different authorization semantics. Multi-tenant queries need their own first-class type.

### Resolution direction

Combined Stage 02 decision pinning:

- `IMustHaveTenant` + sentinel `TenantId` for record identity (Problem 1)
- `TenantSelection` value object for query/view tenant scope (Problem 2)
- The two are orthogonal: a record always has one tenant; a query may span many. Authorization expansion (capability graph + macaroons) is the pivot point that maps `TenantSelection.AllAccessible` to a concrete tenant set per principal per moment.

## Scope Statement

### In scope

**Tenant identity (sentinel pattern):**

1. **TenantId reserved-sentinel namespace.** Reserve a syntactic prefix (e.g., `__*__`) for sentinel values; validate at `TenantId` construction that user-supplied values don't enter the reserved namespace.
2. **Sentinel set decision.** Whether to add `TenantId.Guest`, `TenantId.System`, others now — or wait until a concrete use case demands them.
3. **`IMayHaveTenant` deprecation stance.** Interface has zero downstream implementations today (verified 2026-04-28); deprecation cost is nil. Decide: `[Obsolete]` mark, full removal, or retain as documented escape hatch.
4. **`TenantId?` nullable migration plan.** 13 use sites in 5 packages need per-site classification: sentinel-fits / TenantSelection-fits / privileged-API-fits / retain-justified.
5. **`TenantId.Default` semantics clarification.** Currently overloaded — XML doc says "platform default"; `IAuditContextProvider` treats it as "system context." Decide: is `Default` the canonical system sentinel, or do we split it into `Default` (platform default) + `System` (kernel-internal)?

**Tenant query scope (`TenantSelection`):**

6. **`TenantSelection` value object shape.** Likely a sealed-hierarchy discriminated union: `Single(TenantId)`, `Multiple(IReadOnlyCollection<TenantId>)`, `AllAccessible`. Pin shape and home (likely `Foundation.MultiTenancy`). Convenience: `TenantSelection.Of(params TenantId[])` factory that resolves to `Single` or `Multiple` by count.
7. **`TenantSelection.AllAccessible` v0 semantics.** Three readings exist: "accessible *now*" (dashboard semantic — common case), "accessible *at record creation time*" (regulatory-investigation semantic), "accessible *ever*" (personal-history semantic — risky for least-privilege). Pin v0 = `Now`; document deferred siblings.
8. **Capability-graph time-awareness expectation.** `AllAccessible` expansion at query time must honor macaroon `expires_at` caveats and time-bound capability grants. Verify existing `IPolicyEvaluator` already honors this; document the cross-cutting expectation.
9. **Capability-graph entity-awareness expectation.** Per-entity capability scoping (medical-provider's "patient X but not patient Y" within an accessible tenant) is the four-namespace model from `foundation/DECENTRALIZATION.md`. Verify entity-level filtering composes with `TenantSelection` expansion and document the expectation.

**Cross-cutting:**

10. **CONVENTIONS-vs-ADR scope decision.** Stage 02 picks. CONVENTIONS-grade if pattern is established without breaking changes; ADR-grade if `IMayHaveTenant` is removed (api-change) OR `TenantSelection` displaces existing `TenantId?` query-parameter contracts in a way that callers must migrate.

### Out of scope

- **`EntitySelection` value object.** Ergonomic caller-side filtering of "show me only entities X, Y" within an accessible tenant. Pure-capability-graph filtering is sufficient for v0; an explicit caller filter is a usability nicety. Defer until a real use case demands it; flagged in OQ12 below for awareness.
- **`AllAccessible` historical / record-time variants.** Pin `Now` for v0; add `AsOfRecordTime` etc. when a regulatory-investigation use case lands.
- **Renaming `TenantId.Default`.** Risk of churn for 16+ existing callers; rename only if a Stage 02 finding forces it.
- **Foundation-audit vs kernel-audit relationship.** Discovered during scoping that `Sunfish.Foundation.Assets.Audit` already exists (with its own `IAuditLog` + `AuditQuery` + `IAuditContextProvider`) alongside the proposed `Sunfish.Kernel.Audit` from ADR 0049. The relationship between these two audit substrates is its own scope concern; flagged for follow-up intake.
- **`ActorId.System` parallel.** `ActorId` has the same sentinel shape question (`ActorId.System` is already a sentinel) and an analogous `ActorSelection` question for "who performed this." Intake stays focused on tenant; Stage 02 may decide whether the convention generalizes to actor.

## Affected Sunfish Areas

Impact markers approximate; Stage 01 Discovery refines.

| Area | Impact | Note |
|---|---|---|
| `packages/foundation/Assets/Common/TenantId.cs` | **affected** | Add reserved-namespace validation; potentially add new sentinels (`Guest`, `System`) |
| `packages/foundation-multitenancy/ITenantScoped.cs` | **affected** | Deprecate or remove `IMayHaveTenant`; soften XML docs to point at sentinel pattern |
| `packages/foundation-multitenancy/TenantSelection.cs` | **new** | New value-object hierarchy: `Single` / `Multiple` / `AllAccessible` |
| `packages/foundation-multitenancy/` (DI / extensions) | **possibly new** | Helper for materializing `TenantSelection.AllAccessible` against a principal's capability graph (`ITenantSelectionExpander`?) — Stage 02 decides |
| `packages/foundation/Capabilities/` (existing PolicyEvaluator) | **verified** | Confirm time-awareness + entity-scoping; document expectation; no behavior change expected |
| `packages/foundation/Assets/Audit/AuditQuery.cs` | **affected** | `TenantId? Tenant = null` migrates to `TenantSelection Tenants` (multi-tenant query) |
| `packages/foundation/Assets/Entities/EntityQuery.cs` | **affected** | Same migration pattern |
| `packages/foundation-integrations/ISyncCursorStore.cs` | **affected** | Nullable parameter — sentinel-fits (cursor not tied to a tenant) |
| `packages/foundation-integrations/SyncCursor.cs` | **affected** | Same |
| `packages/foundation-integrations/InMemorySyncCursorStore.cs` | **affected** | Two nullable parameters |
| `packages/foundation-integrations/WebhookEventEnvelope.cs` | **affected** | Nullable property — pre-routing webhook context, sentinel-fits |
| `packages/foundation-featuremanagement/FeatureEvaluationContext.cs` | **affected** | Nullable property — platform-wide feature flag, sentinel-fits |
| `packages/foundation-localfirst/DataExport.cs` | **affected** | Nullable property — likely TenantSelection-fits (multi-tenant export) |
| `packages/foundation-localfirst/DataImport.cs` | **affected** | Nullable property `TargetTenantId` — sentinel-fits (import into a sentinel target) or TenantSelection-fits (import into a chosen tenant set) |
| `packages/blocks-businesscases/tests/BundleEntitlementResolverTests.cs` | **affected** (test) | Test fixture parameter |
| `packages/foundation/tests/Assets/Audit/InMemoryAuditLogTests.cs` | **affected** (test) | Test fixture parameter |
| `packages/kernel-audit/` (per ADR 0049, not yet scaffolded) | **dependent** | v0 `AuditQuery` adopts `TenantSelection` if convention lands first; otherwise scaffolds against `IMustHaveTenant` and migrates later |
| `_shared/engineering/CONVENTIONS-multi-tenancy.md` *or* `packages/foundation-multitenancy/CONVENTIONS.md` | **new artifact** | Convention doc home — Stage 02 picks |
| `docs/adrs/<NNNN>-multi-tenancy-type-surface.md` | **possibly new** | If Stage 02 finds the change ADR-grade |

## Open Questions

### Tenant identity (sentinel) questions

1. **Reserved-namespace form.** `__guest__` vs `~guest~` vs `[guest]` vs URI-style `sentinel:guest` vs structurally-distinct (a separate `SentinelTenantId` type). Least likely to collide; easiest to enforce; most legible at call sites.
2. **Sentinel set at first landing.** Add `TenantId.Guest` + `TenantId.System` now (anticipating Phase 2 audit + recovery use cases), or wait for a concrete first use case (anti-pattern #15: premature precision)? Phase 2 scope (per `project_phase_2_commercial_scope`) does not currently demand a guest sentinel — all six tenants are real LLCs.
3. **`TenantId.Default` future role.** (a) keep as canonical "system / unscoped" sentinel and don't add `System`; (b) split — `Default` = platform default, `System` = kernel-internal; (c) deprecate `Default` (high churn risk for 16+ callers — likely rejected).
4. **`IMayHaveTenant` deprecation tier.** (a) `[Obsolete]` mark with sentinel-pattern guidance; (b) leave as documented escape hatch; (c) remove outright — zero downstream implementations make this technically safe.
5. **Per-site migration semantics for the 13 nullable sites.** Each needs "what does null mean here?" + proposed substitution:
   - **Audit/Entity queries (`AuditQuery`, `EntityQuery`)** — null = "any tenant"; **TenantSelection-fits**, not sentinel
   - **Sync cursors (4 sites)** — null = "global cursor not tied to a tenant"; **sentinel-fits** (Guest)
   - **Webhook envelope** — null = "incoming webhook not yet routed"; **sentinel-fits** (Guest, pre-routing)
   - **Feature evaluation context** — null = "platform-wide flag, not tenant-overridden"; **sentinel-fits** (Guest) OR justified retention
   - **DataExport/DataImport** — null = "all-tenant"; **TenantSelection-fits** (likely `AllAccessible` for export, `Multiple` for import)
   - Two test fixtures — follow whichever production migration decides
6. **CONVENTIONS doc location.** `_shared/engineering/CONVENTIONS-multi-tenancy.md` (cross-package) vs `packages/foundation-multitenancy/CONVENTIONS.md` (colocated). Discoverability vs cohesion.
7. **Validation strictness at TenantId construction.** (a) reject reserved-namespace user input by throwing `ArgumentException`; (b) silently normalize / strip; (c) accept-and-warn via logging. Throwing is strongest; silent normalization is the riskiest.

### Tenant query scope (`TenantSelection`) questions

8. **`TenantSelection` discriminated-union shape.** Sealed-hierarchy via private-ctor abstract record + nested sealed records is the C# canonical pattern; alternative is a struct + enum tag + payload union. Hierarchy is more idiomatic for Sunfish (already used elsewhere?). Pin pattern at Stage 02.
9. **`AllAccessible.Now` as the v0 default.** Pin v0 semantics to "accessible right now via the principal's currently-active capability graph." Document `AllAccessible.AsOfRecordTime` and `AllAccessible.Ever` as deferred sibling shapes to be added when regulatory-investigation or personal-history workflows demand them.
10. **`TenantSelection.AllAccessible` expansion locus.** Where does `AllAccessible → IReadOnlyCollection<TenantId>` happen? Options: (a) inside the query implementation (each `IAuditTrail.QueryAsync` impl handles its own expansion); (b) shared `ITenantSelectionExpander` injected DI service that wraps the capability graph; (c) caller pre-expands and passes `Multiple`. (b) centralizes auth logic; (c) is most explicit; (a) is most flexible but easiest to drift.
11. **Capability-graph contract verification.** Confirm `Foundation.Capabilities` + `Foundation.Macaroons` + `IPolicyEvaluator` already honor (i) `expires_at` time-bounding caveats and (ii) per-entity scoping caveats. If not, gap-analysis ADR before this convention can ship. Initial read of `DECENTRALIZATION.md` says yes; M1 verifies.

### Cross-cutting

12. **`EntitySelection` parallel — pre-empt or defer?** Within an accessible tenant, the medical-provider use case wants "patient X but not Y" filtering. Pure-capability-graph evaluation handles this without a caller-side type. Pre-empt with `EntitySelection` value object now, or wait for a concrete ergonomic complaint? Lean **defer** unless M1 finds an immediate consumer.
13. **`ActorId` parallel — pre-empt or defer?** `ActorId.System` already exists; an analogous `ActorSelection` question may want answering. Same decision: pre-empt or defer until a concrete use case shows a gap.
14. **Kernel-audit blocking dependency timing.** (a) land kernel-audit v0 first using `IMustHaveTenant` + `TenantSelection` (clean v0 if convention lands first; same shape if it doesn't); (b) hold kernel-audit until convention lands. Current default per user direction: hold. Reconsider after Stage 01.

## Discovery findings (preliminary, 2026-04-28)

Captured during intake to anchor Stage 01 work; treat as starting points, not conclusions.

### Code-state findings

- **`IMayHaveTenant` adoption: zero.** Searched all `packages/**/*.cs`; only the definition site at `foundation-multitenancy/ITenantScoped.cs:30` matches. Production blast radius for deprecation/removal is nil.
- **`IMustHaveTenant` adoption: 7 records** in `blocks-tenant-admin`, `blocks-businesscases`, `blocks-subscriptions`. The "must have tenant" branch is the de-facto convention already.
- **`TenantId.Default` adoption: 16+ sites** including 11 test fixtures and 5 production sites — most notably `IAuditContextProvider`'s default (`NullAuditContextProvider`) which uses it as the system-context sentinel. Sentinel pattern is informally established.
- **`TenantId?` nullable adoption: 13 sites** across 5 packages — query parameters (3), cursor types (4), context types (3), data-transfer types (2), tests (2). Per-site semantics vary as noted in OQ5.
- **`Sunfish.Foundation.Assets.Audit` already exists** with `IAuditLog` + `AuditQuery` + `IAuditContextProvider`. ADR 0049 proposes `Sunfish.Kernel.Audit`. Their relationship is a separate intake concern.

### Worked example — medical-provider use case stress test

Setup: traveling provider, 10 patients assigned today, clinic rotation weekly, patient records change daily.

Mapping to Sunfish primitives:

| Concept | Sunfish primitive |
|---|---|
| Clinic | `TenantId` (one per clinic) |
| Patient | `IMustHaveTenant` record scoped to current clinic-tenant |
| Provider | `PrincipalId` with macaroon-bearing capabilities |
| Today's 10 patients | Capability with caveats: `(action="patient:read", tenant=clinic-B, entity IN {p1..p10}, expires_at=tonight)` |
| Weekly clinic rotation | Time-bound capability: `(tenant=clinic-B, expires_at=Friday)` |

The proposed types hold up:

- *"Show me my work today"* → `AuditQuery(Tenants: AllAccessible, Principal: providerId)` — `AllAccessible.Now` expands to today's clinic via capability graph.
- *"Show me my work last Tuesday"* → same query + time filter — capability evaluated at record time (requires `AsOfRecordTime` v1 sibling, deferred).
- *"Show me audit for these 3 patients"* → either capability graph filters automatically (`AllAccessible` covers all 10 → caller filters returned set), or eventually an `EntitySelection` (deferred per OQ12).

What this validates:

- `TenantSelection.AllAccessible` is the right ergonomic answer for the dashboard / personal-view workflow class.
- Records stay `IMustHaveTenant` + single-`TenantId`; provider rotation does not change record identity.
- Sentinels stay orthogonal; no medical-specific sentinel needed.

What this stresses:

- Capability graph must expand `AllAccessible` against current-time capability grants (validates OQ8 / OQ11).
- Per-entity capability scoping must compose with `TenantSelection` expansion (OQ9 / OQ11).
- `AllAccessible` semantics needs explicit time-pinning at v0 (OQ9: `Now` as default, sibling shapes deferred).
- Scope creep candidate `EntitySelection` worth flagging; defer unless concrete consumer (OQ12).

## Proposed First 3 Milestones

### M1 — Stage 01 Discovery

- Per-site classification of all 13 `TenantId?` sites: sentinel-fits / TenantSelection-fits / privileged-API-fits / retain-justified, with proposed substitution per site (anchor in OQ5 starting points).
- Confirm `IMayHaveTenant` zero-adoption finding by clean-build search (not just grep).
- Per-site classification of all 16+ `TenantId.Default` sites (system context vs. platform default vs. test fixture vs. other).
- **Capability graph contract audit (OQ11):** verify `Foundation.Capabilities` + `Foundation.Macaroons` + `IPolicyEvaluator` already honor (i) `expires_at` caveats; (ii) per-entity scoping caveats. Output: clear yes/no with citations.
- Survey for `ActorId.System` and analogous patterns to inform Stage 02 generalization scope (OQ13).
- Output: `icm/01_discovery/output/multi-tenancy-type-surface-discovery-2026-04-DD.md`.

### M2 — Stage 02 Architecture (CONVENTIONS or ADR)

- Pin CONVENTIONS-grade vs ADR-grade based on M1 findings (OQ10).
- Pin reserved-namespace syntactic form (OQ1).
- Pin sentinel set with justification (OQ2).
- Pin `IMayHaveTenant` future (OQ4).
- Pin `TenantSelection` shape (OQ8) and `AllAccessible.Now` v0 semantics (OQ9).
- Pin `TenantSelection.AllAccessible` expansion locus (OQ10): which DI service / interface owns it.
- Pin `TenantId` constructor validation strictness (OQ7).
- Resolve OQ12 (`EntitySelection`) and OQ13 (`ActorId` parallel) — pre-empt or defer.
- Output: `_shared/engineering/CONVENTIONS-multi-tenancy.md` (or location decided in M2) + optional `docs/adrs/<NNNN>-multi-tenancy-type-surface.md` if ADR-grade.

### M3 — Stage 06 Build (mechanical migration)

- Apply convention to `TenantId.cs` (validation + any new sentinels).
- Add `TenantSelection.cs` and (if pinned in M2) `ITenantSelectionExpander` + default impl in `Foundation.MultiTenancy`.
- Apply per-site migration to the 13 nullable sites per M1 classification.
- Mark `IMayHaveTenant` per M2 decision (`[Obsolete]` / removed / retained-with-doc).
- Unblock kernel-audit v0 scaffolding (ADR 0049) per OQ14.
- Output: PR with mechanical migrations + convention doc.

## Kernel-audit retrofit plan

**Context:** A parallel session scaffolded `packages/kernel-audit/` (commit `02852d6`, branch `feat/kernel-audit-scaffold-adr-0049`, PR #190) on 2026-04-28 between this intake's first version and its widening to include `TenantSelection`. The scaffold was designed against the older single-tenant v0 surface. Auto-merge was disabled defensively when the lead session discovered it. The convention's M3 retrofits this in-flight scaffold rather than scaffolding from zero.

### Drift assessment vs. widened convention

After reading `packages/kernel-audit/EventLogBackedAuditTrail.cs`, the impl behavior is **smarter than the surface signatures suggest** — three drift points, but only one is a real bug.

| Drift | Severity | Retrofit |
|---|---|---|
| **`AuditQuery.TenantId` is single `TenantId`, not `TenantSelection Tenants`** | Feature gap (not bug) — multi-tenant queries (Phase 2 consolidated dashboards, medical-provider AllAccessible) can't be expressed | **Tier 2: blocked on M2.** Wait for `TenantSelection` shape pin in Stage 02 Architecture before retrofitting. AuditQuery's single-tenant constructor stays as a compat shim wrapping `new TenantSelection.Single(tenantId)`. |
| **`AttestingSignatures` is `IReadOnlyList<Signature>`, not `IReadOnlyList<AttestingSignature>` with `(PrincipalId, Signature)` pairs** | Bug for downstream verifiability — without the principal, a compliance reviewer reading historical records can't look up the attesting key to verify | **Tier 1: do now.** Add `AttestingSignature` record struct to `Sunfish.Kernel.Audit`; replace the field type. Tests using `Array.Empty<Signature>()` become `Array.Empty<AttestingSignature>()`. ~5 sites. |
| **`IAuditTrail.AppendAsync` docstring claims to "verify all signatures"** | Documentation drift, not implementation drift — the actual impl already does the right thing (verify the payload's `SignedOperation` envelope; trust multi-party `AttestingSignatures` per inline comment at `EventLogBackedAuditTrail.cs:71-79`) | **Tier 1: do now.** Update `IAuditTrail.AppendAsync` XML doc + `AuditRecord` XML doc to accurately describe the hybrid policy: substrate verifies the envelope; multi-party attestations are caller-verified upstream. The README already says this; the contract docstring just needs to match. |

### Tier 1 retrofits (do now, independent of convention M2)

These do not depend on the convention's `TenantSelection` decision — they can land as a follow-up commit on the existing `feat/kernel-audit-scaffold-adr-0049` branch (or a separate small PR that #190 rebases on top of):

1. **Add `AttestingSignature` record struct.** New file `packages/kernel-audit/AttestingSignature.cs` with `public readonly record struct AttestingSignature(PrincipalId PrincipalId, Signature Signature)`. Update `AuditRecord.cs` to use `IReadOnlyList<AttestingSignature>` for the field. Update test fixtures (5 sites in `AuditTrailTests.cs` use `Array.Empty<Signature>()`).
2. **Fix `IAuditTrail.AppendAsync` XML doc.** Soften the "verifies all signatures before persistence" claim to match the actual hybrid impl. Cite the inline comment in `EventLogBackedAuditTrail.cs:71-79` as the load-bearing description. Mirror in `AuditRecord` XML doc where it discusses the "Signing scope" remarks.
3. **Optional README addendum.** Note explicitly that `AttestingSignatures` are caller-verified, with a forward link to "v1 algorithm-agility envelope" per ADR 0004.

Estimated scope: ~80 LOC across 4 files. Should land as `refactor(kernel-audit): retrofit AttestingSignature shape + verification docstring` either on PR #190's branch (force-push) or as a follow-up PR that PR #190 rebases on top of. Use the worktree workaround per `feedback_use_worktree_when_gitbutler_blocks` if `but` rebase congestion blocks staging.

### Tier 2 retrofit (waits for convention M2)

Once M2 pins `TenantSelection` shape (sealed-hierarchy discriminated union: `Single` / `Multiple` / `AllAccessible`):

4. **Migrate `AuditQuery.TenantId` to `TenantSelection Tenants`.** Replace the field. Add a single-arg compat constructor `AuditQuery(TenantId)` that wraps `new TenantSelection.Single(tenantId)` to preserve callers. Adjust `EventLogBackedAuditTrail.QueryAsync` filter logic to expand `Multiple` and `AllAccessible` (the latter via `ITenantSelectionExpander` per OQ10).
5. **Audit-trail capability-graph integration.** When M2 also pins the `ITenantSelectionExpander` locus, `EventLogBackedAuditTrail` injects it (or moves the expansion to a higher layer). Tests gain a multi-tenant `QueryAsync(AllAccessible)` case + an entity-filtered case demonstrating the medical-provider workflow.

Estimated scope: ~150 LOC across 5–6 files. Lands as part of the convention's M3 build PR.

### Out of scope for retrofit

- The fundamental scaffold shape (parallel-to-kernel-ledger, layered over `IEventLog`, `IMustHaveTenant`, open-discriminator `AuditEventType`, `FormatVersion = 0`, `EventLogBackedAuditTrail` impl) — these all align with the widened convention. No retrofit needed.
- Foundation-audit / kernel-audit relationship — separate intake.

## Pipeline variant routing

**Filed as:** `sunfish-feature-change` — introducing a foundation convention is feature-shaped.

**Escalates to:** `sunfish-api-change` if M2 decides on (a) `IMayHaveTenant` removal, or (b) breaking-shape migration of `TenantId? Tenant` query parameters to `TenantSelection Tenants` in already-public foundation APIs (`Foundation.Assets.Audit.AuditQuery`, `Foundation.Assets.Entities.EntityQuery`). M2 finding determines this; intake leaves the door open without committing.

## Next stage

Advance to **01_discovery** for the inventory + capability-graph contract audit in M1. Stage 02 Architecture decisions (OQ1–OQ13) block on M1 findings; OQ14 (kernel-audit timing) is the gating prioritization decision and resolves at the end of Stage 02 once both convention shapes are pinned.
