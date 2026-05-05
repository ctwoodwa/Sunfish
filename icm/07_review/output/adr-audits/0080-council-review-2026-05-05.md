# ADR 0080 Council Review — 2026-05-05

**ADR:** `docs/adrs/0080-quarterdeck-entry-point.md`
**Branch:** `docs/adr-0080-quarterdeck-entry-point`
**Date:** 2026-05-05
**Verdict:** NEEDS-AMENDMENT → all blocking amendments applied same session → **re-review queued**

---

## Final verdict (post-amendment)

All 10 Critical and 22 Major findings applied in the same authoring session. Council
dispatched across 4 perspectives; combined findings resolved in a single full-rewrite
pass. Pending re-review to confirm all Criticals resolved. §A0 is now fully closed (no
self-acknowledged gaps). Three new security contract sections (§5.1/§5.2/§5.3) added.
WCAG conformance contract significantly tightened.

---

## Findings

| ID | Perspective | Severity | Finding | Resolution |
|---|---|---|---|---|
| SI-1 | Skeptical Implementer | Critical | `IDepartmentKpiSource` referenced in Open Q2 but not defined in §1 or §2; `KpiCards` field non-nullable with no source contract | Applied: `IDepartmentKpiSource` interface added to §2; empty-list fallback; §2.3 aggregation rules item 7 covers `Task.WhenAll` pattern |
| SI-2 | Skeptical Implementer | Critical | `OodWatchSummary` bridge type has no conversion path from `OodWatch`; `OnWatchActorDisplayName` requires actor-directory lookup not cited anywhere | Applied: §1 `OodWatchSummary` doc note specifies `(OodRoleSummary)(int)oodWatch.Role` cast convention; §2.3 rule 8 specifies actor-display-name sourcing; Open Q1 renamed to cover actor-directory forward-ref |
| SI-3 | Skeptical Implementer | Critical | `GetSnapshotAsync` failure modes undefined (timeout, partial state, per-source errors) | Applied: §2.1 Failure modes table added with per-source sentinels; provider MUST NOT throw on per-source timeout |
| SI-4 | Skeptical Implementer | Critical | Timeout ownership ambiguous — spec says 1s/2s but `ValueTask` carries no deadline; who wraps the CTS? | Applied: §2 `IQuarterdeckDataProvider` XML doc + §1 `QuarterdeckOptions` clarify provider owns 800ms per-source CTS; block-side owns 2s outer timeout as defense-in-depth |
| SI-5 | Skeptical Implementer | Major | `SubscribeSnapshotAsync` lifecycle unspecified (cancellation, backpressure, partial state, fault) | Applied: §2.2 Subscription semantics added (Channel capacity-1 DropOldest; atomic snapshots; source fault → degraded snapshot + continue) |
| SI-6 | Skeptical Implementer | Major | `DepartmentLink` behavior when `Denied` unspecified — `DisplayName` populated or blank? `Status` real or Unknown? | Applied: §1 `DepartmentLink` record adds normative: DisplayName MUST still be populated (existence not secret); Status MUST be Unknown when Denied |
| SI-7 | Skeptical Implementer | Major | Open Q1 (`IStandingOrderRepository.GetByActorAsync`) a blocker with no fallback | Applied: §A0.1 verified `IStandingOrderRepository` has only `EnumerateAsync`; §2.3 rule 2 specifies client-side filter of `EnumerateAsync`, cap at 1,000 |
| SI-8 | Skeptical Implementer | Major | `AcknowledgeAlertAsync` referenced in §4 but no interface owns it, no signature, no error semantics | Applied: `IQuarterdeckCommandService` added to §2 with `AcknowledgeAlertAsync(TenantId, Principal, string, CancellationToken)` + idempotency + two-phase audit |
| SI-9 | Skeptical Implementer | Major | `HandoverWatchAsync` referenced in §3 with no citing interface or method signature | Applied: §3 now cites `IOodWatchService.HandoverWatchAsync(OodWatchId, ActorId)` exact signature (verified from ADR 0078); §A0.4 corrected |
| SI-10 | Skeptical Implementer | Major | 30s heartbeat hardcoded; no configurability | Applied: `QuarterdeckOptions` record added to §1 with `HeartbeatInterval`, `ProviderTimeout`, `PerSourceTimeout` + `Default` singleton |
| SI-11 | Skeptical Implementer | Major | `AlertId` format unconstrained; collisions across sources possible | Applied: §1 `QuarterdeckAlert` doc note specifies `{SourceName}:{source-local-id}` convention + `^[A-Za-z0-9_\-:]{1,128}$` validation rule |
| SI-12 | Skeptical Implementer | Major | `TransferWatch`/`StandWatch` used in §3/§5 without citation source | Applied: §5 table note + §A0.2 clarify these are ADR 0077 §2 existing catalog entries (W#46 forward-ref); not new constants from this ADR |
| SI-13 | Skeptical Implementer | Major | `Principal` parameter aggregation rules (permission pre-resolution, AffectsCurrentActor, IsCurrentActorOnWatch) not specified | Applied: §2.3 Aggregation rules added (7 rules covering all Principal-derived fields) |
| SI-14–SI-20 | Skeptical Implementer | Minor | String localization note; StatusDetail nullability; PendingAlerts ordering; CapturedAt clock; RecentOrders cardinality; result-count debounce owner | Applied: §1 record comments + §2.3 PendingAlerts ordering + §2.1 CapturedAt clock source + §1 RecentOrders ≤5 cap |
| PL-01 | Pedantic Lawyer | Major | §4 alert ticker behavior in descriptive prose with no RFC 2119 verbs | Applied: §4 rewritten with `MUST` for every normative obligation |
| PL-02 | Pedantic Lawyer | Major | KPI card `role="article"` or `role="listitem"` — two options without selection criterion | Applied: §6 picks `role="listitem"` within explicit `role="list"` (Safari/VoiceOver `list-style:none` gotcha noted) |
| PL-03 | Pedantic Lawyer | Critical | `ViewQuarterdeck` has two rows with different authorities — same ShipAction name, different permission — self-contradictory | Applied: split into `ViewQuarterdeck` (page, any ShipRole) and `ViewQuarterdeckAlerts` (ticker, DivisionOfficer+) in §5 |
| PL-04 | Pedantic Lawyer | Critical | `AcknowledgeAlertAsync` dangling reference — no owning interface | Applied: `IQuarterdeckCommandService` added (see SI-8) |
| PL-05 | Pedantic Lawyer | Major | `IShipRoleRegistry` cited in §1 `DepartmentLink` comment but not in §A0 | Applied: comment changed to "tenant-configured display label"; `IShipRoleRegistry` reference removed |
| PL-06 | Pedantic Lawyer | Major | `RecentOrders` cardinality — §1 unbounded vs checklist "last 5" | Applied: §1 `QuarterdeckSnapshot` doc note: "MUST contain ≤5 items, IssuedAt DESC" |
| PL-07 | Pedantic Lawyer | Critical | "KPI change" in `SubscribeSnapshotAsync` operationally undefined (governs network/CPU load) | Applied: §2 `SubscribeSnapshotAsync` XML doc defines triggers explicitly: (a) OodWatch state transition; (b) new RequiresAcknowledgement alert or ack removed; (c) DepartmentKpi.Status enum value change; MUST NOT emit on MetricValue string change alone |
| PL-08 | Pedantic Lawyer | Major | `OodRoleSummary` vs `OodRole` relationship undefined; divergence risk | Applied: §1 `OodRoleSummary` comment states: UI-tier projection; MUST stay numerically aligned; cast convention `(OodRoleSummary)(int)oodWatch.Role`; update both in same PR |
| PL-09 | Pedantic Lawyer | Major | `MissionEnvelopeSummary` / `MissionEnvelopeStatus` type vs ADR 0062 relationship undefined | Applied: §1 `MissionEnvelopeSummary` comment states projection rules from ADR 0062 `FeatureAvailabilityState`/`ProbeStatus` with explicit Nominal/Degraded/Unknown mapping |
| PL-10 | Pedantic Lawyer | Major | `StandingOrderSummary.AffectsCurrentActor` semantics undefined | Applied: §1 comment defines predicate (distribution list + role-distribution + issued-by); §2.3 rule 3 canonicalizes |
| PL-11 | Pedantic Lawyer | Critical | "disabled 2 seconds after open" — ambiguous English; one reading violates SC 3.3.4 | Applied: §3 + §6 rewritten: "MUST be `aria-disabled='true'` on open; MUST become enabled exactly 2 000 ms after dialog open event" |
| PL-12 | Pedantic Lawyer | Minor | SC 3.3.4 cited for focus-on-Cancel; correct SC is 2.4.3 | Applied: §3 changed to cite SC 2.4.3 (Focus Order) for focus-on-Cancel; SC 3.3.4 retained only for confirm-button timing |
| PL-13 | Pedantic Lawyer | Minor | `aria-pressed="false"` hardcoded; not clearly a dynamic attribute | Applied: §4 + §6 specify `aria-pressed`-only pattern with explicit toggle semantics |
| PL-14 | Pedantic Lawyer | Minor | `TransferWatch`/`StandWatch` in §5 table but not declared in constants block | Applied: §5 note clarifies these are ADR 0077 §2 existing catalog (W#46 forward-ref); §A0.2 updated |
| PL-15 | Pedantic Lawyer | Minor | "Department descent (Read)" in §5 table — "Read" not a ShipAction name | Applied: §5 table now says `ShipAction.Read` explicitly and cites ADR 0077 §2.1 |
| PL-17 | Pedantic Lawyer | Minor | `MaxWatchDuration: TimeSpan` mixed with NodaTime types | Applied: changed to `NodaTime.Duration` in §1 `OodWatchSummary` |
| PL-18 | Pedantic Lawyer | Minor | `IQuarterdeckAlertSource.GetAlertsAsync` no cap/sort/staleness contract | Applied: §2 XML doc: "Returns at most 50 items, sorted IssuedAt DESC"; §2.3 rule 6 specifies aggregator caps + dedup + 24h TTL |
| PL-20 | Pedantic Lawyer | Major | Pause button only pauses polite region — assertive must continue; spec was ambiguous | Applied: §4 specifies assertive region MUST continue announcing High alerts regardless of pause state; pause button labelled "Pause non-critical alerts" |
| PL-21 | Pedantic Lawyer | Critical | §A0 ships with three self-acknowledged unresolved citations | Applied: all three resolved (IShipRoleRegistry removed; MissionEnvelopeSummary projection rules added; OodRoleSummary alignment stated) |
| S1 | Security Engineer | Critical | Cross-tenant cache poisoning risk in permission pre-resolution — cache key missing TenantId | Applied: §5.2 Tenant context binding added; cache key MUST include TenantId as primary component; MUST NOT share across TenantId boundaries |
| S2 | Security Engineer | Critical | `AcknowledgeAlertAsync` undefined — authorization sequence unspecified | Applied: `IQuarterdeckCommandService` + §5 pre-flight added (see SI-8 + PL-04) |
| S3 | Security Engineer | Major | TOCTOU between watch resolution and `HandoverWatchAsync` | Applied: §3 step 3 specifies capture `activeWatch.Id` at dialog-open; `OodWatchId` serves as optimistic concurrency token; `OodWatchConflictException` handles TOCTOU at service level |
| S4 | Security Engineer | Major | `IQuarterdeckAlertSource.SourceName` uniqueness unenforced — impersonation risk | Applied: §5.3 startup uniqueness check added; `SourceName` registered-prefix requirement (`sunfish.{domain}`) |
| S5 | Security Engineer | Major | Alert topology disclosure to Denied-department actors | Applied: `AlertVisibilityPolicy` enum added to §1 + §1 `QuarterdeckAlert`; §2.3 rule 5 specifies policy enforcement; default `OmitForDeniedActors` |
| S6 | Security Engineer | Major | Snapshot identity binding gap — actor-specific fields could be served cross-actor if cached | Applied: §1 `QuarterdeckSnapshot` doc note: "actor-specific; implementations MUST NOT cache across actors or TenantId boundaries" |
| S7 | Security Engineer | Major | No two-phase audit for watch handover at Quarterdeck layer | Applied: §3 steps 4–6 add `WatchHandoverRequested` pre-op audit; ADR 0078's `OodWatchRelieved` is post-op; §A0.5 adds 3 new `AuditEventType` constants |
| S8 | Security Engineer | Major | No §4.3-equivalent ShipAction startup registration verification | Applied: §5.1 ShipAction registration verification added (mirrors ADR 0079 §4.3) |
| A11Y-1 | WCAG/a11y | Major | Skip link target unspecified — no `id` on `<main>`, no `tabindex="-1"` | Applied: §6 specifies `<main id="main-content" tabindex="-1">`; skip link `<a href="#main-content">` |
| A11Y-2 | WCAG/a11y | Major | KPI card `role="article"` or `role="listitem"` — two options, no criterion | Applied: §6 picks `role="listitem"` + `<ul role="list">` (explicit role for Safari/VoiceOver; see PL-02) |
| A11Y-3 | WCAG/a11y | Critical | `role="banner"` won't apply inside `<main>`; second `banner` if app-shell has one | Applied: §3 + §6 changed to `role="region" aria-label="Watch status"` (NOT `role="banner"`); banner reserved for app-shell top-level header per ADR 0077 |
| A11Y-4 | WCAG/a11y | Critical | `aria-atomic="true"` on alert ticker list — re-announces full list on every change | Applied: §4 changed to `aria-atomic="false" aria-relevant="additions"` on both regions; per-item announcements only; acknowledge-button activation MUST NOT trigger re-announcement |
| A11Y-5 | WCAG/a11y | Major | `aria-activedescendant` ID strategy unspecified — collision risk, staleness | Applied: §6 specifies `quarterdeck-search-result-{stableKey}` convention (Wayfinder canonical address); stable within session; `SunfishA11yAssertions.ActiveDescendantIdResolves` |
| A11Y-6 | WCAG/a11y | Major | Result-count announcement location unspecified — wrong placement pollutes combobox/listbox | Applied: §6 specifies sr-only `<div>` SIBLING to combobox (not child); `aria-live="polite" aria-atomic="true"` |
| A11Y-7 | WCAG/a11y | Major | Watch banner `aria-label` includes elapsed time — SRs cache label; stale | Applied: §6 specifies elapsed time as visible text with `aria-hidden="true"`; static `aria-label` excludes elapsed time; 60s cadence polite announcement for elapsed |
| A11Y-8 | WCAG/a11y | Major | `{relievingActor}` undefined in `aria-describedby` consequence text | Applied: §3 specifies pre-select interaction model (relieving actor selected before dialog opens); consequence text fully resolved on dialog open |
| A11Y-9 | WCAG/a11y | Major | `aria-disabled="true"` does not suppress click/keyboard activation — spec silent | Applied: §6 adds: click/keyboard handlers MUST return early; CSS `cursor: not-allowed`; denial reason on visible element (not sr-only); `SunfishA11yAssertions.AriaDisabledSuppressesActivation` |
| A11Y-10 | WCAG/a11y | Critical | SC 3.3.4 confirm-button timing inverted — "disabled 2 seconds after open" reads as enabled-for-2s window | Applied: §3 + §6 rewritten (see PL-11); confirmed SC 3.3.4 interpretation = deliberation pause before confirm, not race-the-clock window |
| A11Y-11 | WCAG/a11y | Minor | Pause button `aria-pressed` vs label-change pattern — not specified | Applied: §4 + §6 specify `aria-pressed`-only pattern with static label "Pause non-critical alerts" |
| A11Y-12 | WCAG/a11y | Minor | `prefers-reduced-motion` doesn't default ticker to paused on initial load | Applied: §4 + §6 specify default-to-paused under `prefers-reduced-motion: reduce`; `SunfishA11yAssertions.ReducedMotionDefaultsToPaused` |
| A11Y-13 | WCAG/a11y | Minor | No focus-restoration fallback when trigger element leaves DOM | Applied: §3 + §6 specify `IFocusTrap.RestoreFocus(fallback: MainLandmark)` + polite "Dialog closed" announcement |

---

## Structural-citation spot-check (post-amendment)

| Symbol | Status |
|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | PASS ✓ |
| `Sunfish.Foundation.Assets.Common.ActorId` | PASS ✓ |
| `Sunfish.Foundation.Capabilities.Principal` | PASS ✓ |
| `Sunfish.Foundation.MissionSpace.IMissionEnvelopeProvider` | PASS ✓ (W#40 built) |
| `Sunfish.Foundation.Wayfinder.IStandingOrderRepository.EnumerateAsync` | PASS ✓ (verified `IStandingOrderRepository.cs`) |
| `Sunfish.Foundation.Ship.Common.ShipLocation`, `IPermissionResolver`, `ShipAction` | FORWARD-REF (W#46 build) |
| `Sunfish.Foundation.Wayfinder.IOodWatchService.HandoverWatchAsync(OodWatchId, ActorId)` | FORWARD-REF (W#49 build; signature corrected from prior §A0.4) |
| `NodaTime.Instant`, `NodaTime.Duration` | PASS ✓ (external) |
| `IShipRoleRegistry` | REMOVED (was cited in §1 comment only; no backing ADR found; replaced with prose) |
