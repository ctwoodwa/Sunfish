# Stage 06 Hand-off — W#42 Foundation.Wayfinder Phase 1 substrate (ADR 0065)

**Workstream:** W#42 — Wayfinder System + Standing Order Contract
**ADR:** [`docs/adrs/0065-wayfinder-system-and-standing-order-contract.md`](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) (Accepted; PR #479 merged 2026-05-02)
**Council:** [`icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md`](../../icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md) (10 findings; 8 mechanical applied; F4/F5 pending CO disposition)
**Pipeline variant:** `sunfish-feature-change`
**Estimate:** ~18-25h sunfish-PM time / 6-7 PRs / 5 phases (per ADR 0065 council F8)
**Cohort posture:** pre-merge council canonical for substrate-tier; Stage 1.5 + WCAG/a11y subagent BEFORE any phase commit (ADR 0065 §Decision §7 mandates a11y subagent on every UI-bearing follow-on; Phases 3a/4 carry UI surface).

---

## Summary

Build the Wayfinder substrate: per-tenant Standing Order CRDT log + validator chain + audit-by-construction + Atlas materialized-view contract + dual-surface form/JSON toggle + WCAG 2.2 AA conformance. Replaces ad-hoc-config-per-block; consumed by ~ADR 0066 / ~0067 / ~0068 / W#43 (ADR 0009 amendment).

This Phase 1 ships the substrate package + interfaces + reference impls. Per-adapter UI rendering (Anchor MAUI / Bridge React / iOS) is deferred to follow-up workstreams.

---

## Acceptance criteria (per ADR 0065 §Implementation checklist)

### Phase 1 — Foundation.Wayfinder package + Standing Order types (~5h)

**Files created:**
- `packages/foundation-wayfinder/Sunfish.Foundation.Wayfinder.csproj` (Microsoft.NET.Sdk; netstandard2.1 or net11.0 — match cohort precedent at COB discretion)
- `packages/foundation-wayfinder/StandingOrder.cs` — `StandingOrder` record + `StandingOrderTriple` + `ApprovalChain` + `ApprovalStep`
- `packages/foundation-wayfinder/StandingOrderId.cs` — `StandingOrderId` + `AuditRecordId` `readonly record struct(Guid Value)`
- `packages/foundation-wayfinder/StandingOrderScope.cs` — 5-value enum (User/Tenant/Platform/Integration/Security)
- `packages/foundation-wayfinder/StandingOrderState.cs` — 6-value enum (Issued/Validated/Applied/Rescinded/Rejected/Conflicted)
- `packages/foundation-wayfinder/IStandingOrderRepository.cs`
- `packages/foundation-wayfinder/IStandingOrderIssuer.cs` — with required `IAuditTrail` parameter on `IssueAsync` + `RescindAsync`
- `packages/foundation-wayfinder/IStandingOrderValidator.cs` — with `Priority` property + `ValidateAsync` method
- `packages/foundation-wayfinder/StandingOrderValidatorPriority.cs` — 4-value enum (Schema=100/Policy=200/Authority=300/Conflict=400)
- `packages/foundation-wayfinder/StandingOrderValidationResult.cs` — `Accepted: bool` + `Issues: IReadOnlyList<StandingOrderValidationIssue>`
- `packages/foundation-wayfinder/StandingOrderValidationIssue.cs` — Severity + Path + Message + RemediationHint
- `packages/foundation-wayfinder/StandingOrderValidationSeverity.cs` — 4-value enum (Info/Warning/Error/Block)
- `packages/foundation-wayfinder/WayfinderServiceExtensions.cs` — `AddSunfishWayfinder()` + `AddStandingOrderValidator<T>()` (per cohort `AddSunfishX()` pattern)
- `packages/kernel-audit/AuditEventType.cs` — **add 5 new static readonly fields**: `StandingOrderIssued`, `StandingOrderAmended`, `StandingOrderRescinded`, `StandingOrderRejected`, `StandingOrderConflictResolved` (record-struct pattern, NOT enum values)

**ProjectReferences:** `Sunfish.Kernel.Audit` + `Sunfish.Kernel.Crdt` + `Sunfish.Foundation.MultiTenancy` + `Sunfish.Foundation.Identity` + `NodaTime`.

**Tests:** 12 unit tests in `packages/foundation-wayfinder/tests/`:
- 5 shape round-trip tests (StandingOrder / StandingOrderTriple / ApprovalChain / StandingOrderValidationResult / StandingOrderValidationIssue)
- 4 canonical-JSON round-trip tests via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (camelCase per ADR 0028 §A7.8)
- 3 enum-canonical-identifier round-trip tests (StandingOrderScope / StandingOrderState / StandingOrderValidatorPriority)

**Acceptance gate (binary):** all 12 tests green; `dotnet build` clean; new `AuditEventType` fields visible from `Sunfish.Kernel.Audit` consumers.

### Phase 2 — CRDT-backed repository + issuer (~4h)

**Files created:**
- `packages/foundation-wayfinder/CrdtStandingOrderRepository.cs` — implements `IStandingOrderRepository` over `ICrdtEngine.CreateDocument(documentId)` / `OpenDocument(documentId, snapshot)`; per-tenant document at `wayfinder/standing-orders/{tenantId}`
- `packages/foundation-wayfinder/DefaultStandingOrderIssuer.cs` — calls validator chain in `Priority` order; flips `State` to `Rejected` on `Block`-severity; emits `IAuditTrail.AppendAsync(new AuditRecord(..., AuditEventType.StandingOrderIssued, ...))` — per ADR 0065 §A0.3 the issuer constructs the AuditRecord, NOT a pseudo `(AuditEventType, payload, ct)` overload
- `packages/foundation-wayfinder/RescindAsync` impl on `DefaultStandingOrderIssuer` — emits `StandingOrderRescinded` audit event; does NOT redact the original issuance audit record (per ADR 0065 §Decision §4 rescission semantics — audit immutability per ADR 0049)

**Tests:** 8 property tests:
- 3 concurrent-issuance CRDT merge tests (disjoint paths / overlapping paths producing `Conflicted` / lexicographic tie-break)
- 2 audit-emission tests (one record per issuance; `StandingOrderConflictResolved` emitted once per pair)
- 2 rescission tests (rescind emits new audit record; original record preserved)
- 1 Block-severity validator integration test (chain stops at first Block; `State == Rejected`; rejection still emits audit)

**Acceptance gate:** all 8 tests green; CRDT merge property holds across 100 randomized concurrent-issuance scenarios.

### Phase 3a — Atlas projector + search basics (~4h)

**Files created:**
- `packages/foundation-wayfinder/IAtlasProjector.cs`
- `packages/foundation-wayfinder/AtlasView.cs` — `TenantId` + `ProjectedAt` (Instant) + `SettingsByPath: IReadOnlyDictionary<string, AtlasSettingSnapshot>`
- `packages/foundation-wayfinder/AtlasSettingSnapshot.cs` — Path + CurrentValue + LastIssuedBy + LastIssuedAt + Schema (`AtlasSchemaDescriptor`)
- `packages/foundation-wayfinder/AtlasSchemaDescriptor.cs` — JsonSchema (RFC draft 2020-12) + DisplayName + DescriptionMarkdown + Kind
- `packages/foundation-wayfinder/AtlasSettingKind.cs` — 6-value enum (String/Number/Boolean/Enum/JsonObject/Secret)
- `packages/foundation-wayfinder/AtlasSearchHit.cs` — Path + DisplayName + MatchSnippet + Score
- `packages/foundation-wayfinder/DefaultAtlasProjector.cs` — projects from per-tenant Standing Order log; `IAsyncEnumerable<AtlasSearchHit> SearchAsync` for streaming hit-by-hit display

**Tests:** 6 unit tests:
- 3 projection-correctness tests (single setting / nested settings / array-of-settings shape)
- 3 search tests (exact match / prefix match / substring match with score ordering)

**Acceptance gate:** all 6 tests green; search latency UNbenchmarked at this phase (perf tests in 3b).

### Phase 3b — Schema-registration analyzer + perf tests (~3h)

**Files created:**
- `packages/foundation-wayfinder-analyzers/Sunfish.Wayfinder.Analyzers.csproj` — NEW analyzer project (Microsoft.CodeAnalysis.CSharp); pack as `IsRoslynComponent`
- `packages/foundation-wayfinder-analyzers/SchemaRegistrationAnalyzer.cs` — Roslyn diagnostic `SUNFISH_WAYFINDER001` (severity Warning) on `IServiceCollection.AddSunfish*()` invocations whose containing project doesn't register an `AtlasSchemaDescriptor` for at least one settable path

**Tests:** 4 perf tests + 3 analyzer tests:
- Perf: search at 1K / 5K / 10K / 50K settings; cold-projection + warm-cache scenarios both measured; targets P95 ≤ 200ms cold / ≤ 100ms warm at 10K (per ADR 0065 council F9)
- Analyzer: positive (registers descriptor — no diagnostic) / negative (omits descriptor — Warning fires) / multi-call (two `AddSunfishX()` calls; one missing — Warning fires once)

**Acceptance gate:** all 7 tests green; perf targets met (or documented variance with halt-condition).

### Phase 4 — Cross-package wiring + apps/docs (~2h)

**Files created:**
- `apps/kitchen-sink/...` — wire `AddSunfishWayfinder()` + demo one form-view setting (e.g., `anchor.maui.theme`)
- `apps/docs/blocks/foundation-wayfinder.md` — block documentation page
- `apps/docs/wcag/wayfinder.md` — WCAG 2.2 AA + EN 301 549 v3.2.1 conformance baseline (initial; iterates per release)
- Cross-link from `_shared/product/architecture-principles.md` — make the "Wayfinder system" section a real link

**Acceptance gate:** kitchen-sink demo runs; docs pages render in apps/docs; cross-link resolves.

### Phase 5 — Ledger flip + close W#42 (~30min)

**Files modified:**
- `icm/_state/active-workstreams.md` — row 42: `design-in-flight` → `built` with PR list + new package list (`foundation-wayfinder`, `foundation-wayfinder-analyzers`) + new AuditEventType list
- `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_42_*.md` — memory entry for shipped scope

**Acceptance gate:** ledger row updated; memory entry written.

---

## Halt-conditions (PAUSE work and write `cob-question` beacon if any of these surface)

1. **CRDT engine document-key collision** — if any other workstream adopts a `wayfinder/...` document-id namespace, coordinate before Phase 2 commit.
2. **AuditEventType ordering** — if `Sunfish.Kernel.Audit.AuditEventType` is in flux at Phase 1 commit time (concurrent ADR amendment), pause and ask XO for the canonical insertion point.
3. **Per-tenant Loro document size** — if test projection at 50K settings (Phase 3b perf) shows memory or latency outside ADR 0065 §Decision §5 budget, halt on policy: do we reduce the search scope, raise the budget, or shard per-scope?
4. **Roslyn analyzer false-positive rate** — if SUNFISH_WAYFINDER001 fires on >10% of existing `AddSunfish*()` calls in `apps/kitchen-sink`, halt: the analyzer's heuristic for "registers descriptor" needs refinement.
5. **Form/JSON dual-surface implementation** — Phase 1 ships only the contract; the actual form↔JSON UI lives in Anchor MAUI / Bridge React adapters (separate workstreams). If Phase 4 kitchen-sink demo *requires* the form view to make sense, halt: kitchen-sink may need a stub form view that calls `IStandingOrderIssuer` directly.
6. **EN 301 549 v3.2.1 baseline** — Phase 4 conformance report is initial-baseline; full conformance is ongoing. Don't claim "conformant" — claim "baseline established."
7. **F4/F5 disposition pending** — ADR 0065 council F4 (split ADR 0009 amendment to W#43) and F5 (complex-schema a11y §7.1) await CO disposition. If CO disposes F5 with the proposed §7.1 addition, the Atlas form-view contract grows; if CO disposes F4 differently, the W#43 row may collapse back into W#42 scope. Confirm CO disposition before Phase 4 starts.

---

## Cohort patterns to follow (mirror W#34/W#35/W#36/W#37/W#39/W#40/W#41 substrate)

- **`AddSunfishX()` DI extension** with two overloads (audit-disabled + audit-enabled with TenantId requirement); both-or-neither per W#32.
- **`JsonStringEnumConverter`** on every enum so `CanonicalJson.Serialize` round-trips signature-stably (per ADR 0028 §A7.8).
- **Alphabetized audit-payload keys** in `WayfinderAuditPayloads` factory.
- **NSubstitute** for test doubles (industry default per Decision Discipline Rule 5).
- **`apps/docs/foundation/wayfinder/overview.md`** convention.
- **ConcurrentDictionary** for any in-process state (per cohort precedent).
- **Two-overload constructor** (audit-disabled / audit-enabled) on every service that emits audit.

---

## Cross-references

- **ADR:** [`docs/adrs/0065-wayfinder-system-and-standing-order-contract.md`](../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md)
- **Council:** [`icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md`](../../icm/07_review/output/adr-audits/0065-council-review-2026-05-01.md)
- **Intake:** [`icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md`](../../icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md)
- **W#34 discovery:** [`icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md`](../../icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md) §5.1 / §6.1 / §7
- **Composition substrate:** ADR 0028 (CRDT) + ADR 0049 (audit) + ADR 0009 (FeatureManagement, future W#43 consumer)
- **Sibling Stage 06 cohort precedent:** `foundation-mission-space-stage06-handoff.md` + `foundation-mission-space-requirements-stage06-handoff.md` + `foundation-versioning-stage06-handoff.md`

---

## Notes for COB

- This is a **substrate** hand-off; consumer wiring is separate workstreams.
- Pre-merge council canonical: dispatch Stage 1.5 council subagent (standard 4-perspective + WCAG/a11y for Phases 3a/4) BEFORE merging any phase. Cohort batting average is 19-of-19 — every substrate has needed amendments; pre-merge is dramatically cheaper than post-merge.
- §A0 self-audit inline in the council file. Three directions: negative-existence (claim doesn't yet exist) + positive-existence (verify the cited symbol exists in the cited namespace) + structural-citation correctness (verify field names + signatures). Cohort failure rate ~65% NOT caught by §A0 alone.
