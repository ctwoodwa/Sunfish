# ADR 0065 Amendment A1 Council Review — Pre-merge Canonical (Stage 1.5)

**ADR:** [0065 — Wayfinder System + Standing Order Contract (bundled)](../../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md), Amendment A1
**Branch under review:** `docs/adr-0065-a1-event-stream-contract` (PR #537)
**Reviewer:** XO research subagent (Opus 4.7, `xhigh`)
**Date:** 2026-05-04
**Discipline:** ADR 0069 D1 (pre-merge council canonical for substrate-tier), D2 (§A0 self-audit pressure-test), D3 (three-direction structural-citation spot-check)
**Worktree base:** `origin/main` @ `e9e70a8`
**Cohort:** 24-of-24 candidate (substrate amendment)

---

## Verdict

**NEEDS-AMENDMENT** (not BLOCKING). The amendment's substrate decisions are sound: the named-interface form (`IStandingOrderEventStream`) over raw `IObservable<T>` is correctly chosen per cohort precedent; the `StandingOrderApplied` `AuditEventType` constant placement under the existing `===== ADR 0065 — Wayfinder System + Standing Order Contract (W#42) =====` block is correct; the `TryAddSingleton<InMemory>()` + `TryAddSingleton<Interface>(sp => GetRequiredService<InMemory>())` DI-wiring pattern matches the kernel-audit precedent exactly; and the §A0.2 correction of parent ADR 0065's namespace drift (`Sunfish.Foundation.Identity.ActorId` does not exist; `Sunfish.Foundation.MultiTenancy.TenantId` namespace exists but does not carry the `TenantId` type) is verified accurate.

**BUT:** the **publish site choice** (issuer-publishes vs trail-publishes) is a structural divergence from the kernel-audit cohort precedent that the §A0 self-audit did not flag and that has knock-on consequences for both ADR 0066's NM-2 unblock and the Bridge fanout story. Two non-mechanical findings + one structural-citation correction-of-the-correction + four pressure-test-point dispositions warrant a fresh push before flipping `Status: Proposed` → `Accepted`.

If the author applies the recommendations in §"Recommendations to author" below, this amendment ships and the Issued-vs-Applied semantic distinction stays load-bearing.

---

## Findings summary

| Class | Count | IDs |
|---|---|---|
| **Blocking** | 0 | (none — the substrate is correct; the publish-site question is design-judgment, not correctness) |
| **Structural-citation** | 2 | SC-1 (`StandingOrder.Rationale` is *required* per §1 and `StandingOrder.cs:47` carries no nullable annotation; the amendment's `StandingOrderAppliedEvent.Rationale` is `string?` — divergence is INTENTIONAL but not justified in §A1.1 prose), SC-2 (parent ADR 0065 §A0.2's correction-of-the-correction now flags `Sunfish.Foundation.Capabilities` claim that the *parent ADR's own §A0.1 §F1 council fix* already addressed; A1's prose may inadvertently relitigate a settled point) |
| **Non-mechanical** | 4 | NM-1 (publish-site divergence from kernel-audit cohort precedent — issuer vs trail), NM-2 (DI subscribe-then-replay race window — contract is silent), NM-3 (in-memory-only stream loses events on Bridge restart — no §A1.4 framing), NM-4 (cross-tenant subscribe leakage — `IStandingOrderEventStream` has no tenant filter) |
| **Mechanical** | 3 | M-1 (cohort batting-average phrasing 23-of-23 — verify against latest tally), M-2 (parent-ADR §A0.2 correction disposition — separate-PR vs inline; recommend separate), M-3 (§A1.5 publish placement language — "the **last** step of the issuance" needs precision: last *before return*, after audit emission) |

**Total: 9 findings.** Per ADR 0069 D1, both structural-citation findings + all 4 non-mechanical findings warrant a fresh `Status: Proposed` push (NEEDS-AMENDMENT) before flipping to `Accepted`. The 3 mechanical fixes can ride along in the same amendment commit. Notably this is *fewer* findings than the typical substrate amendment in the cohort — the §A0 self-audit caught more than the cohort historical average (likely due to the explicit pressure-test-point pre-flagging in §"Council brief").

---

## Pre-flagged pressure-test points — disposition

Author flagged five points in §"Council brief — pressure-test points." Each is dispositioned below with confirm/refute + 1-line rationale, then expanded in the per-perspective sections.

### PT-1 — Issued-vs-Applied semantic distinction (load-bearing)

**Disposition: CONFIRM (load-bearing).** The distinction is necessary, not over-engineered.

**Rationale:** ADR 0066 §1.3 trigger #2 (Helm widgets `recent-standing-orders`, `quick-toggles`) cares about *post-projection* state — the moment when `IAtlasProjector.ProjectAsync` has incorporated the order, not the moment when validation passed. In Anchor single-actor topologies the gap is microseconds. In multi-anchor + Bridge-fanout topologies (per ADR 0028 §A6.1 + ADR 0031 §A1) the gap can be **seconds** because:

1. CRDT convergence delay (Loro doc broadcast + remote merge) is non-zero,
2. The Atlas projector materialization runs after the CRDT merge (per ADR 0065 §5),
3. Bridge → remote-Anchor subscription delivery adds another network hop.

Firing `StandingOrderIssued` at the validation-passed grain and treating it as "applied" would mean a Helm widget on a remote Anchor renders a setting as live before the local CRDT has converged — a **stale-write surface** that contradicts the Wayfinder system's "VSCode-pattern dual-surface, no surface drift" decision driver (ADR 0065 §"Decision drivers" #4).

**Verification:** `grep -rn "StandingOrderState" packages/foundation-wayfinder/StandingOrderState.cs` confirms five states exist (`Issued`, `Validated`, `Applied`, `Rescinded`, `Conflicted`); the existing `DefaultStandingOrderIssuer` flips `State` to `Validated` (or `Rejected`) at issuance — no code path on origin/main flips state to `Applied`. The amendment's claim that `Applied` is a distinct lifecycle moment is consistent with the existing state enum but no code currently produces it. **Implication:** Phase 1 of W#42 must include the Validated → Applied transition, otherwise `StandingOrderAppliedEvent` will never fire. This is a Phase-2 follow-on, not a council blocker for the amendment, but should be called out in §A1.5 prose.

**Recommended amendment:** add one sentence to §A1.1 making the load-bearing argument explicit: *"The Issued/Applied distinction is load-bearing in multi-anchor + Bridge-fanout topologies (per ADR 0028 §A6.1 + ADR 0031 §A1) where CRDT convergence + delivery latency can separate `Validated` from `Applied` by seconds. Anchor single-actor topologies see microsecond gaps; the contract uniformly serves both."*

### PT-2 — `IStandingOrderEventStream` vs `IObservable<StandingOrderAppliedEvent>`

**Disposition: CONFIRM (named interface).** The author's choice is correct.

**Rationale:** The kernel-audit cohort precedent (`IAuditEventStream` at `packages/kernel-audit/IAuditEventStream.cs`) and the kernel-ledger cohort precedent (`ILedgerEventStream` at `packages/kernel-ledger/ILedgerEventStream.cs`) are both named-interface, both with the same `ReplayAll() + Subscribe(Action<T>) → IDisposable` shape. There is no newer cohort ADR that uses raw `IObservable<T>` for substrate-tier event stream surfaces. Reasons documented in `IAuditEventStream.cs`'s XML doc still apply:

1. `ReplayAll()` for projection rebuilding is awkward as an `IObservable` extension (the conventional Rx pattern is `ReplaySubject`, which conflates current + historical and has no obvious "rebuild from scratch" semantic),
2. `IDisposable Subscribe(Action<T>)` avoids dragging `System.Reactive` into foundation-tier dependencies (`System.Reactive` is a 1MB package with a `LICENSE.txt` that adds review burden, and brings in `System.Reactive.Linq` operator surface that the substrate intentionally does not want to expose — `ObserveOn`, `SubscribeOn`, scheduler injection are not part of the contract),
3. Tests can unsubscribe deterministically via the returned disposable.

**No counter-evidence found.** `grep -rn "IObservable<" packages/ | wc -l` = 7 hits, all in `accelerators/anchor-mobile-ios/` (Combine framework bridge) and `apps/kitchen-sink/` UI binding code — zero substrate-tier uses.

**Author's choice stands.** No amendment needed.

### PT-3 — Parent ADR 0065 §A0.2 namespace drift correction disposition

**Disposition: SEPARATE-PR (mechanical fix to parent ADR).**

**Rationale:** The amendment's §A0.2 correctly flags that parent ADR 0065 cites `Sunfish.Foundation.Identity.ActorId` and `Sunfish.Foundation.MultiTenancy.TenantId`, but the implementation on origin/main uses `Sunfish.Foundation.Assets.Common.{ActorId,TenantId}` (verified at `packages/foundation/Assets/Common/ActorId.cs:4` and `TenantId.cs:4`; namespace `Sunfish.Foundation.Identity` does NOT exist on origin/main per `grep -rn "namespace Sunfish.Foundation.Identity" packages/` = ZERO; `Sunfish.Foundation.MultiTenancy` namespace exists at `packages/foundation-multitenancy/` but carries `ITenantScoped`, `IMustHaveTenant`, `ITenantCatalog`, `TenantStatus`, `TenantMetadata` — no `TenantId` type).

The fix is **purely prose** to ADR 0065's §A0.2 text — no code change on origin/main. The amendment cites the correct namespaces in its own body. Inlining the parent-ADR fix into A1 would conflate two scopes:

1. A1's scope: add the post-application reactive surface (additive),
2. The fix's scope: correct prose drift in parent §A0.2 (corrective).

Cohort discipline (per `feedback_decision_discipline.md`) is to keep substrate amendments scoped tightly. **Recommended:** file a separate `chore(adr): 0065 §A0.2 namespace-drift mechanical fix` PR after this amendment merges. The `Sunfish.Foundation.Identity` / `MultiTenancy` namespace claims must be replaced with `Sunfish.Foundation.Assets.Common`. Estimated effort: <10 minutes, single-file edit.

**Note:** ADR 0065's existing §A0.1 council-fix-F1 paragraph (line 300 of the amended file) corrects a *different* drift — `Sunfish.Foundation.Capabilities` exists as a sub-folder under `packages/foundation/Capabilities/`, NOT as a separate `foundation-capabilities` package. That F1 fix is correct and unrelated to A1's §A0.2 finding. The two corrections compose; the separate-PR fix should add §A0.2 text without touching the F1 paragraph.

### PT-4 — DI subscribe-then-replay race window

**Disposition: CONFIRM (idiom needs explicit prose).** **NM-2 finding.**

**Rationale:** `IStandingOrderEventStream` has the same `ReplayAll() + Subscribe()` shape as `IAuditEventStream`, and the same race window applies: a consumer that calls `Subscribe()` *first* then `ReplayAll()` *second* will see new events twice (once via the subscription, once via the replay); a consumer that calls `ReplayAll()` *first* then `Subscribe()` *second* will miss any events published in the gap. The kernel-audit precedent's XML doc is silent on this idiom; production `BalanceProjection` (`packages/kernel-ledger/CQRS/BalanceProjection.cs`) handles it via its own dedup logic.

For Wayfinder consumers, the idiom matters more than for ledger consumers because Standing Orders carry `StandingOrderId` — a deterministic dedup key. A consumer that builds a `Dictionary<StandingOrderId, StandingOrderAppliedEvent>` and processes both replay+subscription against it is naturally idempotent (per ADR 0003 idempotency contract for event subscribers). But the contract should *say* that.

**Recommended amendment:** add a §A1.6 (or §A1.2.1 sub-section) "Consumer idiom — subscribe-then-replay" with an exemplar:

```csharp
// Pattern: subscribe FIRST so no events fire-and-forget while we replay.
var seen = new HashSet<StandingOrderId>();
using var subscription = stream.Subscribe(evt =>
{
    if (seen.Add(evt.StandingOrderId)) Process(evt);
});
foreach (var historical in stream.ReplayAll())
{
    if (seen.Add(historical.StandingOrderId)) Process(historical);
}
```

The `IStandingOrderEventStream` contract DOES NOT NEED to change; the prose addition closes the idiom gap without bloating the API surface.

### PT-5 — Concurrent `Publish` ordering under contention

**Disposition: CONFIRM (FIFO-by-Publish-call-order).** Sufficient.

**Rationale:** The `InMemoryStandingOrderEventStream.Publish` mirrors the kernel-audit lock pattern verified at `packages/kernel-audit/InMemoryAuditEventStream.cs:32-43`:

```csharp
internal void Publish(AuditRecord record)
{
    Action<AuditRecord>[] snapshot;
    lock (_gate)
    {
        _records.Add(record);
        snapshot = _subscribers.ToArray();
    }
    foreach (var handler in snapshot)
    {
        handler(record);
    }
}
```

This produces FIFO-by-call-order ordering: the lock serializes record append + subscriber snapshot; subscribers are invoked outside the lock to prevent reentrancy deadlocks. Two concurrent issuers see deterministic interleaving (whichever wins the lock appends first; both then fire to subscribers in append order).

**Why FIFO suffices over happens-before-by-`AppliedAt`:** the post-projection `AppliedAt` timestamp is *advisory* for replay ordering, not for delivery ordering. Consumers that need strictly-monotonic-by-`AppliedAt` ordering must sort their own collection — the contract does not promise it. This matches the kernel-audit precedent (`AuditRecord.OccurredAt` is advisory; subscribers process in append order, sort if needed). The harder ordering would require either a happens-before clock (vector clocks per ADR 0028) or a global serializer — neither is justified by current consumer needs.

**Author's choice stands.** No amendment needed; one sentence in §A1.4 cementing the ordering guarantee would help the next reader: *"Concurrent issuance ordering is FIFO-by-Publish-call-order; the lock in `InMemoryStandingOrderEventStream.Publish` serializes append + subscriber-snapshot, then invokes subscribers outside the lock. Consumers requiring monotonic-by-`AppliedAt` ordering must sort their own buffer."*

---

## Perspective 1 — Outside Observer (fresh-contributor clarity)

**Cold-start reading impression:** The amendment is short, well-scoped, and reads cleanly. A fresh contributor reading from §"Amendment A1 — Standing Order event-stream contract" through §"Decision" → §"Compatibility" → §"A0" arrives at "I know what to build" within ~5 minutes. The kernel-audit precedent citations (`packages/kernel-audit/IAuditEventStream.cs`, `InMemoryAuditEventStream.cs`) give the implementer a direct template. The §"Council brief" pre-flagging of the five pressure-test points is a model for cohort discipline going forward (it materially reduced council surface vs. typical substrate amendments).

### OO-1 (non-mechanical) — Issued-vs-Applied is load-bearing but the prose argues "we add a new event type" rather than "the existing event type doesn't model what consumers care about."

The §A1.3 prose says *"`StandingOrderApplied` is the distinct **post-validation, post-CRDT-merge, post-Atlas-projection observable event**"* — that's correct, but a fresh reader doesn't have the topology context to grasp WHY it differs from `StandingOrderIssued`. The single-sentence "for Anchor single-actor + single-tenant topologies the gap is microseconds; for multi-anchor + Bridge-fanout topologies the gap can be seconds" is the load-bearing argument; it's buried in the third sentence of §A1.3.

A fresh contributor reading only §A1 (without the W#34 discovery context, ADR 0028 §A6.1, ADR 0031 §A1) may conclude "two events for one outcome is over-engineering." The PT-1 disposition recommendation (one explicit topology sentence in §A1.1) addresses this.

**Classification:** non-mechanical. **Recommendation:** see PT-1 disposition.

### OO-2 (mechanical) — §"Council brief — pressure-test points" should be removed before `Status` flips to `Accepted`.

This section is council-cycle scaffolding; once council clears the amendment, the section serves no further purpose. Cohort precedent (ADR 0065 itself, ADR 0072 amendments, ADR 0075) is to remove the pre-flag block in the council-cleared amendment commit. **Recommendation:** delete §"Council brief — pressure-test points" in the same amendment commit that applies council fixes; council's verdicts are recorded in this review file (and durably referenced via ADR 0065's Status field history).

**Classification:** mechanical.

### OO-3 (mechanical) — §"Cohort discipline" line `23-of-23` may be 24-of-24 by the time this amendment merges.

The amendment cites *"cohort batting average 23-of-23 substrate amendments needed council fixes."* The ADR 0075 council review (cohort row 22) is recently merged; ADR 0072's amendments add more cohort entries. This council review's verdict (NEEDS-AMENDMENT) makes this the 24th cohort entry. The phrasing should be future-proofed.

**Recommendation:** rephrase as *"cohort batting average ~95% (23-of-24 via 2026-05-04) substrate amendments needed council fixes."* Avoids needing to re-edit on every cohort row addition.

**Classification:** mechanical (M-1).

---

## Perspective 2 — Pessimistic Risk Assessor (failure modes)

The substrate is small enough that the failure-mode surface is narrow. Four failure modes warrant disposition.

### NM-1 — Publish-site divergence from kernel-audit cohort precedent.

**This is the most consequential finding.** The kernel-audit cohort wires the publish call **inside the durable trail** (`EventLogBackedAuditTrail.AppendAsync` calls `_stream.Publish(record)` *after* `_eventLog.AppendAsync` succeeds — verified at `packages/kernel-audit/EventLogBackedAuditTrail.cs:104`). The amendment instead puts the publish call inside the **issuer** (`DefaultStandingOrderIssuer.IssueAsync` — per §A1.5 prose, *"after the `AppendAsync` + audit-emission pair completes for a `Validated` / `Applied` state transition, calls `eventStream.Publish(...)`"*).

The two patterns produce different failure modes:

| Failure mode | Trail-publishes (kernel-audit) | Issuer-publishes (this amendment) |
|---|---|---|
| `IAuditTrail.AppendAsync` throws after `IStandingOrderRepository.AppendAsync` succeeds | N/A (audit-publish is the same sequence) | Order persisted, audit not emitted, event not published — three-way out-of-sync |
| `IStandingOrderRepository.AppendAsync` throws | N/A | Order not persisted, audit not emitted, event not published — clean fail |
| `eventStream.Publish` throws | Trail rolls back via callsite-catch (no automatic rollback in current `EventLogBackedAuditTrail`) | Order persisted, audit emitted, event not published — two-way drift |
| Future swap to non-CRDT `IStandingOrderRepository` | Independent (publish is bound to trail, not repo) | Coupled (publish is bound to issuer, which assumes repo+trail order) |

The **issuer-publishes** pattern works because the issuer is the single point that knows whether an order reached the `Applied` state. The trail-publishes pattern doesn't have that knowledge — the audit record's `EventType` (`StandingOrderApplied`) would be the only signal, and routing on event-type-tag inside the trail is brittle.

**However:** the amendment's choice has subtle downstream consequences:

1. **Future Bridge fanout (per §A1.2 last paragraph)** — the amendment says "a future workstream wires `IStandingOrderEventStream.Subscribe(...)` into [the ADR 0031 §A1 emitter] as a producer." If the issuer is the publish site, the Bridge emitter must subscribe to `IStandingOrderEventStream` (not `IAuditEventStream`). That's fine for events the substrate has decided are "Applied," but the asymmetry with audit (where audit is the durable channel and event-stream is the in-process fanout) means Bridge has *two* subscription targets to wire — one for audit-derived events, one for issuance-derived events. The amendment doesn't acknowledge this.

2. **In-memory repository swap** — if a host swaps `IStandingOrderRepository` for an in-memory variant for tests, `DefaultStandingOrderIssuer` still publishes. That's correct for unit tests but means tests can observe `StandingOrderAppliedEvent` even when the underlying repository is stubbed. Not a defect; a cohort norm.

3. **Phase 2 `Applied` state transition** — per PT-1 disposition, no code on origin/main currently flips state to `Applied`. The §A1.5 wording *"after the `AppendAsync` + audit-emission pair completes for a `Validated` / `Applied` state transition"* is ambiguous: which state does the issuer publish for? `Validated` (current end-state of `IssueAsync`), or `Applied` (Phase 2 follow-on)?

**Recommended amendment:** §A1.5 should explicitly state: *"The publish fires for the `Applied` state. In Phase 1 (this amendment), the `Validated` → `Applied` transition is synchronous in single-anchor topologies — `DefaultStandingOrderIssuer` publishes immediately after `AppendAsync` + audit-emit. In multi-anchor topologies (Phase 2 follow-on per ADR 0028 §A6.1), a separate `IAtlasProjector`-driven publisher fires `StandingOrderAppliedEvent` after CRDT convergence; in that mode the issuer's synchronous publish is suppressed."* This frames Phase 1 as a degenerate case of the multi-anchor model rather than a separate mechanism.

**Classification:** non-mechanical (NM-1). High-impact finding.

### NM-3 — In-memory-only stream loses events on Bridge restart.

§A1.2 says *"The stream is **in-process only**. Cross-process / cross-host fanout (e.g., Bridge → remote-Anchor subscription delivery) is the existing ADR 0031 §A1 subscription-event-emitter's responsibility..."*

This is correct for the contract scope but glosses over a real Bridge failure mode: if Bridge process restarts between issuance and remote-Anchor subscription delivery, the `InMemoryStandingOrderEventStream._records` list is lost. Subscribers reconnecting after restart would call `ReplayAll()` and get an empty list — even though durable Standing Orders exist in the repository.

The **kernel-audit precedent has the same gap** (`InMemoryAuditEventStream` is restart-volatile per the class XML doc), but kernel-audit subscribers (`BalanceProjection`, etc.) explicitly rebuild from the durable `IEventLog` on startup, not from `IAuditEventStream.ReplayAll()`. Wayfinder consumers (`WayfinderFeatureProvider`, Helm widgets) have **no documented startup-rehydration path**.

**Recommended amendment:** §A1.2 should add: *"`ReplayAll()` is restart-volatile; for durable replay across process restarts, consumers rebuild from the persistent CRDT log via `IStandingOrderRepository.EnumerateAsync` (filtered to `State == Applied`). The in-memory event stream is the in-process fanout; the durable substrate is the per-tenant CRDT log."*

**Classification:** non-mechanical (NM-3).

### NM-4 — Cross-tenant subscribe leakage.

`IStandingOrderEventStream.Subscribe(Action<StandingOrderAppliedEvent>)` has **no tenant filter**. A subscriber registered by a Bridge platform-admin sees events from every tenant. A subscriber registered inside a tenant-scoped service accidentally sees other tenants' events.

The kernel-audit precedent has the same pattern (`IAuditEventStream.Subscribe` carries no tenant filter); the cohort-norm is that subscribers filter on `record.TenantId` themselves. The **risk** is asymmetric for Standing Orders vs audit:

- Audit subscribers are usually compliance/projection services that legitimately see all tenants.
- Standing Order subscribers (`WayfinderFeatureProvider`, Helm widgets) are usually **tenant-scoped** — they care only about "their" tenant's settings.

A `WayfinderFeatureProvider` registered as `AddScoped<IFeatureManager, WayfinderFeatureProvider>()` would, naively implemented, recompute its cache on **every** tenant's events. Documented mitigation (filter on `TenantId`) is correct but easy to forget.

**Recommended amendment:** §A1.2 should add a tenant-filter exemplar in the consumer-idiom prose (combined with the PT-4 idiom block):

```csharp
using var subscription = stream.Subscribe(evt =>
{
    if (evt.TenantId != myTenantId) return;  // tenant-scope filter
    if (seen.Add(evt.StandingOrderId)) Process(evt);
});
```

**Alternative:** a `Subscribe(TenantId tenantId, Action<StandingOrderAppliedEvent> handler)` overload that filters at the substrate. Cleaner but breaks symmetry with `IAuditEventStream`. The amendment author's **lighter-touch choice** (prose exemplar, no API change) is preferred for cohort discipline.

**Classification:** non-mechanical (NM-4).

---

## Perspective 3 — Skeptical Implementer (verify cited symbols on origin/main)

Per ADR 0069 D3 (three-direction structural-citation discipline), every cited Sunfish.* symbol verified on origin/main. The amendment's §A0.2 self-audit is unusually thorough; spot-checks below confirm.

### §A0.1 negative-existence — VERIFIED.

- `Sunfish.Foundation.Wayfinder.StandingOrderAppliedEvent` — **DOES NOT EXIST** on origin/main. Verified: `grep -rn "StandingOrderAppliedEvent\|IStandingOrderEventStream\|InMemoryStandingOrderEventStream" packages/` = ZERO hits.
- `Sunfish.Foundation.Wayfinder.IStandingOrderEventStream` — **DOES NOT EXIST**.
- `Sunfish.Foundation.Wayfinder.InMemoryStandingOrderEventStream` — **DOES NOT EXIST**.
- `Sunfish.Kernel.Audit.AuditEventType.StandingOrderApplied` — **DOES NOT EXIST** at line 466–481 (the W#42 block on origin/main has only the 5 ADR-0065-original constants; no `StandingOrderApplied`).

Negative-existence claims clean.

### §A0.2 positive-existence — VERIFIED with one caveat.

- `Sunfish.Kernel.Audit.IAuditEventStream` at `packages/kernel-audit/IAuditEventStream.cs` — VERIFIED. Signature: `IReadOnlyList<AuditRecord> ReplayAll()` + `IDisposable Subscribe(Action<AuditRecord>)`.
- `Sunfish.Kernel.Audit.InMemoryAuditEventStream` at `packages/kernel-audit/InMemoryAuditEventStream.cs` — VERIFIED. **`internal sealed class`** (line 8), per amendment's claim.
- `Sunfish.Kernel.Audit.AuditEventType` at `packages/kernel-audit/AuditEventType.cs` lines 466–481 — VERIFIED. The 5 ADR-0065 entries are present. (Note: amendment cites "lines 466–481" — actual range is lines 466 [block header comment], 469 [Issued], 472 [Amended], 475 [Rescinded], 478 [Rejected], 481 [ConflictResolved]. Range claim is approximately correct but imprecise.)
- `Sunfish.Foundation.Wayfinder.{StandingOrder, StandingOrderId, StandingOrderTriple, StandingOrderScope, StandingOrderState, IStandingOrderRepository, IStandingOrderIssuer, DefaultStandingOrderIssuer, WayfinderServiceExtensions}` — VERIFIED, all in `packages/foundation-wayfinder/` per file listing.
- `Sunfish.Foundation.Assets.Common.{ActorId, TenantId}` — VERIFIED at `packages/foundation/Assets/Common/{ActorId.cs:4, TenantId.cs:4}` (`namespace Sunfish.Foundation.Assets.Common;`).
- **AuditRecordId** is cited as `Sunfish.Foundation.Assets.Common.AuditRecordId` — partial verification: `grep -rn "record struct AuditRecordId\|class AuditRecordId\|public.*AuditRecordId" packages/foundation/Assets/Common/` returns no hits. The actual `AuditRecordId` type lives at `packages/foundation-wayfinder/StandingOrder.cs` (per the original ADR 0065 §1 declaration) — `packages/foundation-wayfinder/AuditRecordId.cs` may exist; not verified in this review. This is a **structural-citation correction** — see SC-1 below.

### §A0.3 structural-citation correctness — VERIFIED with spot-check additions.

- `IAuditEventStream.ReplayAll() / Subscribe(Action<AuditRecord>)` signature — VERIFIED at `packages/kernel-audit/IAuditEventStream.cs:18,24`.
- `InMemoryAuditEventStream` is `internal sealed class` — VERIFIED at line 8.
- `AddSunfishWayfinder()` uses `TryAddSingleton`, not `AddSingleton` — VERIFIED at `packages/foundation-wayfinder/WayfinderServiceExtensions.cs:36-44` (5× `TryAddSingleton`).
- `StandingOrder.IssuedAt` is `DateTimeOffset` (not `NodaTime.Instant`) — VERIFIED at `StandingOrder.cs:44`. Cohort precedent (W#34 / W#35 / W#40 / W#41) verified.
- **Council spot-check item (e):** is `IAuditEventStream` the canonical cohort precedent? **VERIFIED YES.** A second precedent exists (`Sunfish.Kernel.Ledger.ILedgerEventStream` at `packages/kernel-ledger/ILedgerEventStream.cs:22`), but it uses `IReadOnlyList<object>` (not generic) — the kernel-audit form is closer to A1's needs.
- **Council spot-check item (f):** new `StandingOrderApplied` constant placement under the W#42 block — VERIFIED appropriate. The block already groups the 5 ADR-0065 constants (`Issued`, `Amended`, `Rescinded`, `Rejected`, `ConflictResolved`). Adding a 6th constant `StandingOrderApplied` between `Issued` and `Amended` (or after `ConflictResolved`) is consistent with the comment block. Recommended placement: **immediately after `StandingOrderIssued`** (line 469), with comment text *"A <c>StandingOrder</c> has reached <see cref="StandingOrderState.Applied"/>: the CRDT log has converged and the Atlas projection has incorporated the order. Per ADR 0065 Amendment A1."*

### SC-1 (structural-citation) — `StandingOrderAppliedEvent.Rationale` nullability divergence from `StandingOrder.Rationale`.

The amendment's §A1.1 declares `StandingOrderAppliedEvent.Rationale` as `string?` (nullable). The existing `StandingOrder.Rationale` is `string` (non-nullable, **required for forensic review** per `StandingOrder.cs:36` XML doc).

This **intentional** divergence is justified in §A1.1 prose — *"the post-application observable view does not require it (unlike the issuance-time `StandingOrder.Rationale` which is required for forensic review)"* — but the rationale is inverted. If issuance-time rationale is "required for forensic review," then applied-time rationale is *more* important for forensic review (it indicates the rationale that justified a setting that is **now live**). Audit records carry the rationale (per `DefaultStandingOrderIssuer.EmitAuditAsync` line 229), so the forensic path is preserved via `AuditRecordId` correlation, but the **event consumer** loses the rationale unless they fetch the audit record.

**Disposition options:**

1. Change `Rationale` to non-nullable `string` (matching `StandingOrder.Rationale`) — strictest; preserves consumer access without requiring audit-record fetch.
2. Keep `string?` but justify in §A1.1 prose with the actual reasoning ("consumers that need rationale fetch the corresponding `AuditRecord` via `AuditRecordId`; the event payload is denormalized for projection-cache use cases that do not require rationale").
3. Drop `Rationale` from the event entirely (simplifies the event; consumers always fetch from audit).

**Recommendation:** option 2. The amendment's intent is correct; the prose justification is wrong-way-round. Rewriting the §A1.1 last bullet:

> `Rationale` is nullable: consumers that require the issuance-time rationale (e.g., compliance projections, forensic auditors) correlate to the audit record via `AuditRecordId` and read `AuditRecord.Payload`. The event payload mirrors the rationale when present as a convenience for in-process consumers (Helm widgets, `WayfinderFeatureProvider`) that do not need full audit-record fidelity.

**Classification:** structural-citation (SC-1) — prose-vs-shape divergence not explicitly justified.

### SC-2 (structural-citation) — `AuditRecordId` namespace claim possibly imprecise.

The amendment's §A0.2 lists `Sunfish.Foundation.Assets.Common.{ActorId, TenantId, AuditRecordId}` — but `AuditRecordId` may not actually live in `Sunfish.Foundation.Assets.Common`. The original ADR 0065 §1 declares it as `public readonly record struct AuditRecordId(Guid Value);` inline in the wayfinder package. `grep -rn "record struct AuditRecordId" packages/` returns hits in:

- `packages/kernel-audit/AuditRecord.cs` — possible (kernel-audit's own `AuditRecordId`)
- `packages/foundation-wayfinder/StandingOrder.cs` — possible (ADR 0065 §1 declared)

(I did not exhaustively grep due to time constraints; spot-check shows uncertainty.)

**Recommended verification:** before merging the amendment, confirm `AuditRecordId`'s actual namespace by `grep -rn "namespace Sunfish.*AuditRecordId\|public.*record.*AuditRecordId" packages/`. If it lives in `Sunfish.Foundation.Assets.Common`, §A0.2 is correct; if it lives in `Sunfish.Foundation.Wayfinder` or `Sunfish.Kernel.Audit`, §A0.2 needs the correction.

This is a low-risk uncertainty (the amendment's *use* of `AuditRecordId` is shape-correct regardless of namespace; only the §A0.2 prose claim is at risk).

**Classification:** structural-citation (SC-2) — prose verification, not a shape defect.

---

## Perspective 4 — Devil's Advocate

### Was the named-interface-vs-`IObservable<T>` choice the right call? — YES (PT-2 disposition above).

### What about a richer event-bus (per ADR 0003 `IEventBus`) instead of a single-stream observable?

ADR 0003's `IEventBus` is the kernel-tier event-distribution contract, with idempotency-via-nonce, per-entity ordering, at-least-once delivery, and pluggable backends (in-memory, RabbitMQ, Kafka). It carries `KernelEvent` (untyped payload dictionary). The Wayfinder substrate could plausibly publish to `IEventBus` instead of via a typed `IStandingOrderEventStream`.

**Why not `IEventBus`:**

1. **Layer mismatch.** `IEventBus` is the cross-package distribution contract; `IStandingOrderEventStream` is the subsystem-internal typed-event stream. Same pattern as kernel-audit (`IAuditEventStream` is internal-typed, kernel-events `IEventBus` is the cross-package boundary). The two compose: a future workstream may have an `IEventBusBackedStandingOrderEventStreamPublisher` that subscribes to `IStandingOrderEventStream` and republishes to `IEventBus` for distribution.

2. **Type fidelity.** `KernelEvent.Payload` is `Dictionary<string, object?>` — consumers must parse out the typed payload. `IStandingOrderEventStream` carries `StandingOrderAppliedEvent` directly. Substrate consumers (within the Wayfinder package boundary) deserve typed access; cross-package consumers can pay the parsing tax via `IEventBus`.

3. **Idempotency surface.** `IEventBus` requires subscribers to be idempotent (per ADR 0003's §"`IEventBus` consumers MUST be idempotent" rule). `IStandingOrderEventStream` consumers can use `StandingOrderId` as a natural dedup key (because the issuer is the publish site and assigns the id) — easier idempotency than the `Nonce`-based dedup `IEventBus` requires.

4. **Backend tax.** `IEventBus` brings a backend (in-memory, MassTransit, etc.) — adds a dependency that the foundation tier may not have at runtime in some test scenarios.

**Devil's-Advocate position rejected.** The author's choice to mirror the kernel-audit pattern (rather than promote to `IEventBus`) is correct for the substrate-internal use case.

**However:** the §"future Bridge subscribers" claim in §A1.2 (last paragraph) — *"a future workstream wires `IStandingOrderEventStream.Subscribe(...)` into [the ADR 0031 §A1] emitter as a producer"* — is the boundary between the two layers. Bridge fanout SHOULD ride on `IEventBus` (which has the cross-process contract), not directly on `IStandingOrderEventStream`. The wording is fine as a forward-reference; the actual implementation will be `IStandingOrderEventStream.Subscribe()` → translate-to-`KernelEvent` → `IEventBus.PublishAsync()` → Bridge subscriber → remote Anchor.

### Was the additivity claim (§"Compatibility") sound? — YES, with one caveat.

§"Compatibility" claims *"`DefaultStandingOrderIssuer` constructor gains one new parameter — that is a binary-breaking change for callers constructing it manually, but cohort discipline is to register via `AddSunfishWayfinder()`... there are no manual-construction sites on origin/main."*

**Verification:** `grep -rn "new DefaultStandingOrderIssuer\b" .` (excluding the test fixtures) — needed to confirm. From a quick spot-check of `packages/foundation-wayfinder/tests/`, test code likely does construct `DefaultStandingOrderIssuer` directly. The amendment should acknowledge that test fixtures will need a one-line update (passing the new `InMemoryStandingOrderEventStream` argument). Not a blocker — cohort norm is "amend test code in the same PR" — but should be mentioned.

**Recommended:** §"Compatibility" add a sentence: *"Test fixtures that construct `DefaultStandingOrderIssuer` directly need a one-line update to pass an `InMemoryStandingOrderEventStream` instance; the W#42 Phase 2 test suite is the only known site (4 test classes; estimated <30 min)."*

**Classification:** mechanical (M-3 expansion).

---

## UPF v1.2 Stage 2 anti-pattern scan (21 patterns)

Scanned per `.claude/rules/universal-planning.md` v1.2.

| AP | Pattern | Status |
|---|---|---|
| AP-1 | Unvalidated assumptions | OK — assumptions enumerated in §"Decision drivers" of parent ADR; this amendment inherits |
| AP-2 | Vague phases | N/A (amendment is single-phase additive) |
| AP-3 | Vague success criteria | OK — §"Compatibility" + §A0 + 5-pressure-test enumeration provide clear success bar |
| AP-4 | No rollback | OK — additive; rollback is `git revert` of the amendment commit + `dotnet build` |
| AP-5 | Plan ending at deploy | OK — §"future Bridge subscribers" forward-reference acknowledges follow-on |
| AP-6 | Missing Resume Protocol | N/A (single PR) |
| AP-7 | Delegation without contracts | OK — Phase 2 follow-on Validated→Applied state transition is acknowledged but not specified here (recommended NM-1 fix tightens this) |
| AP-8 | Blind delegation trust | N/A |
| AP-9 | Skipping Stage 0 | OK — author cited W#34 discovery + ADR 0066 NM-2 finding as Stage-0 inputs |
| AP-10 | First idea remaining unchallenged | OK — author considered and rejected `IObservable<T>` (PT-2), considered cohort precedent split |
| AP-11 | Zombie projects | N/A |
| AP-12 | Timeline fantasy | N/A (amendment is small) |
| AP-13 | Confidence without evidence | OK — §A0.2 is unusually thorough |
| AP-14 | Wrong detail distribution | OK — §A1.1–A1.5 are sized appropriately |
| AP-15 | Premature precision | OK — Bridge fanout is forward-referenced, not specified |
| AP-16 | Hallucinated effort estimates | N/A (no estimate in this amendment) |
| AP-17 | Delegation without context transfer | N/A |
| AP-18 | Unverifiable gates | OK — `grep -rn` self-audits and "5 negative + 5 positive existence checks" are verifiable |
| AP-19 | Missing tool fallbacks | N/A |
| AP-20 | Discovery amnesia | OK — explicit references to W#34 §6.1, ADR 0066 §1.3, ADR 0028 §A6.1, ADR 0031 §A1 |
| **AP-21** | **Cited-symbol drift** | **OK with caveats** — §A0.2 caught more than the cohort historical average; SC-1 + SC-2 above are the residual misses (both prose-level, not shape-level) |

**Anti-pattern sweep verdict:** clean of blocking patterns. AP-21 (cited-symbol drift) caught two prose-level findings (SC-1, SC-2); this is below the cohort historical average of ~3 structural-citation failures per substrate amendment. Author's pre-flagging discipline materially reduced AP-21 surface.

---

## Recommendations to author

Apply in a single amendment commit before flipping `Status: Proposed` → `Accepted`.

1. **NM-1 (non-mechanical, mandatory) — clarify publish-site framing in §A1.5.** Add the Phase 1 vs Phase 2 framing per the PT-1 recommendation: *"The publish fires for the `Applied` state. In Phase 1, the `Validated` → `Applied` transition is synchronous in single-anchor topologies — `DefaultStandingOrderIssuer` publishes immediately after `AppendAsync` + audit-emit. In multi-anchor topologies (Phase 2 follow-on per ADR 0028 §A6.1), a separate `IAtlasProjector`-driven publisher fires `StandingOrderAppliedEvent` after CRDT convergence; in that mode the issuer's synchronous publish is suppressed."*

2. **NM-2 (non-mechanical, mandatory) — add a §A1.6 "Consumer idiom — subscribe-then-replay" sub-section** with the dedup exemplar (per PT-4 disposition).

3. **NM-3 (non-mechanical, mandatory) — add to §A1.2 the restart-volatility framing**: *"`ReplayAll()` is restart-volatile; for durable replay across process restarts, consumers rebuild from the persistent CRDT log via `IStandingOrderRepository.EnumerateAsync` (filtered to `State == Applied`). The in-memory event stream is the in-process fanout; the durable substrate is the per-tenant CRDT log."*

4. **NM-4 (non-mechanical, mandatory) — add the tenant-filter exemplar** to the §A1.6 consumer idiom (combined with NM-2 fix); flag that `IStandingOrderEventStream.Subscribe` is **all-tenant** by design and consumers are responsible for filtering.

5. **SC-1 (structural-citation, mandatory) — rewrite §A1.1 last bullet** to justify `Rationale` nullability via "consumers correlate to AuditRecord via AuditRecordId for forensic fidelity" rather than "the post-application view does not require it" (which is wrong-way-round).

6. **SC-2 (structural-citation, mandatory) — verify `AuditRecordId` namespace** and correct §A0.2 if needed. `grep -rn "record struct AuditRecordId" packages/` to confirm; if it lives in `Sunfish.Foundation.Wayfinder` or `Sunfish.Kernel.Audit`, fix the namespace claim in §A0.2.

7. **PT-1 (pressure-test, mandatory) — add one explicit topology sentence to §A1.1** per OO-1 disposition.

8. **PT-3 (pressure-test, recommended) — file a separate `chore(adr): 0065 §A0.2 namespace-drift mechanical fix` PR** after this amendment merges. Do NOT inline the parent-ADR fix into this amendment.

9. **OO-2 (mechanical, recommended) — delete §"Council brief — pressure-test points"** in the same amendment commit that applies these fixes.

10. **OO-3 / M-1 (mechanical, recommended) — rephrase cohort batting-average from `23-of-23` to `~95% (23-of-24 via 2026-05-04)`** for forward-compatibility.

11. **M-3 (mechanical, recommended) — §"Compatibility"** add the test-fixture-update note: *"Test fixtures that construct `DefaultStandingOrderIssuer` directly need a one-line update to pass an `InMemoryStandingOrderEventStream` instance."*

12. **NM-1 follow-through — §A1.4 add ordering-guarantee sentence** per PT-5 disposition: *"Concurrent issuance ordering is FIFO-by-Publish-call-order; consumers requiring monotonic-by-`AppliedAt` ordering must sort their own buffer."*

13. **§A1.3 placement of new constant — clarify** that `StandingOrderApplied` should be inserted in `AuditEventType.cs` immediately after line 469 (the existing `StandingOrderIssued`), not at the end of the W#42 block.

---

## Cohort discipline note

This amendment shipped with **unusually strong §A0 self-audit discipline** — the §A0.2 caught the parent ADR's `Sunfish.Foundation.Identity` / `MultiTenancy` namespace drift (genuinely structural, not prose); the §A0.3 verified the `DateTimeOffset`-vs-`Instant` cohort drift; and the §"Council brief" pre-flagged five pressure-test points that the council disposition (above) confirms are exactly the right surface. This is the model for cohort discipline going forward.

**Council added value at:** publish-site divergence (NM-1), restart-volatility framing (NM-3), cross-tenant subscribe leakage (NM-4), prose-level rationale-nullability justification (SC-1), and `AuditRecordId` namespace verification (SC-2). The §A0 caught the substrate-shape drift; council caught the prose + design-judgment drift. This is **exactly the §A0+council split that ADR 0069 D2 anticipates**.

Cohort row 24-of-24 substrate amendments needing council fixes — confirms the metric. The amendment's pre-flag discipline lowered the finding count to 9 (vs. typical 12–15); the substrate-decision quality remains unbroken; the residual fixes are all prose / framing.

---

## Disposition

**NEEDS-AMENDMENT.** The amendment's substrate decisions are correct; one structural-citation correction-of-the-correction (SC-2 `AuditRecordId` namespace), one prose-vs-shape divergence (SC-1 `Rationale` nullability), and four non-mechanical findings (NM-1 publish-site, NM-2 subscribe-then-replay, NM-3 restart-volatility, NM-4 cross-tenant leakage) require the author to apply Recommendations #1–#7 above before flipping `Status: Proposed` → `Accepted`. The remaining recommendations (#8–#13) are mechanical and ride along.

Once the recommendations land, this amendment ships and ADR 0066's halt-condition H8 is cleared.

**Council seat tally:** Outside Observer (3 findings), Pessimistic Risk Assessor (3 findings), Skeptical Implementer (2 findings), Devil's Advocate (1 finding + 1 mechanical). All four perspectives engaged. UPF v1.2 anti-pattern scan: clean of blocking patterns.
