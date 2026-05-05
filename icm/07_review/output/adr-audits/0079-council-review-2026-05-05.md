# ADR 0079 Council Review — 2026-05-05

**ADR:** `docs/adrs/0079-engine-room-observability.md`
**Branch:** `docs/adr-0079-engine-room-observability`
**Date:** 2026-05-05
**Verdict:** NEEDS-AMENDMENT → amendments applied same session → **re-review queued**

---

## Final verdict (post-amendment)

All 13 blocking findings and 10 non-blocking findings applied in the same authoring session.
Pending re-review to confirm all Majors resolved. Structure, §A0 cited-symbol audit, and OTel
metric catalog are solid. No Critical findings were raised.

---

## Findings

| ID | Perspective | Severity | Finding | Resolution |
|---|---|---|---|---|
| F1 | Skeptical Implementer | Major | §3 said "MUST emit audit events" but provided no `AuditRecord` construction pattern or payload-schema spec; implementer cannot build a working `IAuditTrail.AppendAsync` call from the spec | Applied: §3a added with `AuditRecord` construction note (per ADR 0049 pattern), `DateTimeOffset` conversion from `NodaTime.Instant`, and canonical payload schema table for all 8 event types |
| F2 | Skeptical Implementer | Major | `ReleaseQuarantineAsync` returned bare `ValueTask` while `QuarantineDocumentAsync` returned `QuarantineResult`; release can fail and caller has no signal | Applied: `ReleaseQuarantineAsync` now returns `ValueTask<ReleaseResult>`; `ReleaseResult` record added |
| F3 | Skeptical Implementer | Major | ADR `Status: Proposed` with forward-refs that are not yet on origin/main; council cannot verify §4 permission rules or §A0.3 structural-citation-correctness without the cited shapes | Applied: §A0.4 added with expected forward-reference signatures for `ShipRole`, `IPermissionResolver`, `IOodWatchService`, `OodRole` with explicit "subject to final W#46/W#49 shape; amend if diverged" language |
| F4 | Skeptical Implementer | Minor | `EngineRoomHealthSummary` had 4 hardcoded SubsystemHealth fields — binary-compat break if a 5th sub-room ships | Applied: changed to `IReadOnlyList<SubsystemHealth> Subsystems` with `For(EngineRoomSubsystem)` helper |
| F5 | Skeptical Implementer | Minor | `GetCrdtGrowthMetricsAsync` lacked pagination/filter; 100k-document tenants would enumerate all per call | Applied: `CrdtGrowthQuery` record + filtered overload added |
| F6 | Skeptical Implementer | Minor | `SubscribeHealthAsync` cardinality undefined; no contract to test against | Applied: XML doc specifies "emits on subsystem-status transition AND heartbeat every 30s (configurable)" |
| F7 | Pedantic Lawyer | Major | §4 `CompactDocument` permission rule mixed a role predicate with a state predicate (`CompactionEligible`); creates TOCTOU without clear owner between `IPermissionResolver` and the command service | Applied: permission table shows role-only authority for `CompactDocument`; §2 `CompactDocumentAsync` doc specifies service-side state eligibility check (throws `InvalidOperationException`, not auth exception) |
| F8 | Pedantic Lawyer | Major | `EngineRoomHealthDegraded` cooldown semantics ambiguous — under one interpretation a recovery emission could be suppressed | Applied: §3 now specifies dedup is per `(TenantId, EngineRoomSubsystem, status_from, status_to)` tuple; transitions with different tuples are NOT suppressed even within the window |
| F9 | Pedantic Lawyer | Major | §3 audit payload fields had no canonical key names; `NodaTime.Instant` vs `DateTimeOffset` mismatch unresolved | Applied: §3a payload schema table with per-event canonical key names + .NET types; `Instant` values convert to `DateTimeOffset` via `.ToDateTimeOffset()` at the audit boundary |
| F10 | Pedantic Lawyer | Minor | "Visually hidden" language was ambiguous — `display:none` and `visibility:hidden` remove elements from the SR tree | Applied: §6 metric charts spec now names sr-only/clip technique explicitly and lists forbidden methods (`display:none`, `visibility:hidden`, `aria-hidden`) |
| F11 | Pedantic Lawyer | Minor | Single `aria-live` region cannot change politeness at runtime without SR inconsistency (NVDA/JAWS) | Applied: §6 live regions now specifies TWO sibling regions — one `assertive` (degradations) + one `polite` (recoveries) each with `aria-atomic="true"` |
| F12 | Pedantic Lawyer | Minor | `ViewEngineRoom` "DivisionOfficer or higher" assumed total ordering on `ShipRole` which may be a poset | Applied: §4 table now enumerates explicit roles (`DivisionOfficer`, `EngineerOfficer`, `XO`, `Captain`) instead of "or higher" |
| F13 | Pedantic Lawyer | NM | `AuditEventType as readonly record struct(string Value)` ✓ verified | None |
| F14 | Security Engineer | Major | `IEngineRoomCommandService` accepted `tenantId` as a parameter — attacker with DI access could pass any TenantId; no ambient-vs-parameter cross-check | Applied: §4.1 Tenant context binding: implementations MUST resolve ambient tenant from `ITenantContext` and reject if `tenantId` parameter doesn't match |
| F15 | Security Engineer | Major | TOCTOU on EOOW check — `IPermissionResolver` reads watch state at time T; operation runs at T+δ; watch rotation between T and T+δ creates disputable audit | Applied: §4.2 Watch-pin atomicity: command service MUST capture `GetActiveWatchAsync` once at operation start; captured `OodWatch.Id` MUST be embedded in pre-op audit payload as `watchId` |
| F16 | Security Engineer | Major | "Without emitting audit events" on authorization denial silenced security events — attacker could probe authorization boundaries with no trace | Applied: reversed; pre-flight now: (1) call `IPermissionResolver`, (2) if Denied → emit `DamageControlAuthorizationDenied` audit event THEN throw exception; `DamageControlAuthorizationDenied` added as 8th `AuditEventType` |
| F17 | Security Engineer | Major | Single-phase audit for quarantine/release; crash between operation-success and audit-write would lose audit trail | Applied: two-phase pattern (per compaction precedent): `DocumentQuarantineRequested` (pre-op) + `DocumentQuarantined` (post-op); `DocumentQuarantineReleaseRequested` + `DocumentQuarantineReleased`; total AuditEventType constants raised from 5 to 8 |
| F18 | Security Engineer | Minor | `EngineRoomUnauthorizedException : InvalidOperationException` — wrong base; retry handlers for state-error may accidentally swallow auth failures | Applied: changed to `: UnauthorizedAccessException`; added doc note "MUST NOT be caught by routine retry handlers" |
| F19 | Security Engineer | Minor | No startup verification that `ShipAction` constants are registered in `foundation-ship-common`; unknown actions silently deny all users | Applied: §4.3 added — "startup analyzer or DI-validation check MUST verify all ShipAction refs resolve to registered actions; unknown actions MUST log a warning" |
| F20 | WCAG/a11y | Major | `role="dialog"` specified for Damage Control dialog — incorrect; destructive elevated-authority ops requiring confirmation are exactly the `alertdialog` use case per ARIA 1.2 | Applied: changed to `role="alertdialog"` in §6; ADR's note "(not alertdialog)" removed |
| F21 | WCAG/a11y | Major | `role="grid"` spec missing `aria-colcount`/`aria-colindex` — NVDA/JAWS announce "column X of Y" using these; omission = incomplete virtualization announcement | Applied: §6 log table spec now includes `aria-colcount` on grid container + `aria-colindex` on each `gridcell`/`columnheader`; ARIA 1.2 §grid cited |
| F22 | WCAG/a11y | Major | Trace timeline accessible-table alternative was gated behind a toggle; keyboard users hit non-accessible chart first, must find toggle — violates SC 1.1.1/1.4.5 | Applied: §6 trace timeline spec changed: accessible table alternative is ALWAYS in SR tree (sr-only/clip); chart is `aria-hidden="true"`; toggle controls visual presentation only |
| F23 | WCAG/a11y | Minor | Initial focus on confirm button hostile for destructive action (stray Enter = quarantine) | Applied: §6 dialog focus moves to Cancel button or dialog heading on open; confirm button disabled 1-2 seconds after open (SC 3.3.4 cited) |
| F24 | WCAG/a11y | Minor | Consequence-summary constraints missing; no mechanism to ensure operator reads before confirming | Applied: §6 consequence summary MUST be ≤3 sentences including documentId + effect; confirm button disabled 1-2s after open per SC 3.3.4 |
| F25 | WCAG/a11y | Minor | Missing reference to ADR 0034 a11y harness | Applied: §6.1 added requiring `SunfishA11yAssertions` verification for log-table grid semantics, dialog focus-trap, sr-only alternatives |
| F26 | WCAG/a11y | NM | `IFocusTrap` forward-ref consistent with W#42 substrate memory | None |

---

## Structural-citation spot-check

| Symbol | Status |
|---|---|
| `Sunfish.Foundation.Assets.Common.TenantId` | PASS (verified at `packages/foundation/Assets/Common/TenantId.cs`) |
| `Sunfish.Foundation.Assets.Common.ActorId` | PASS (verified at `packages/foundation/Assets/Common/ActorId.cs`) |
| `Sunfish.Kernel.Crdt.ICrdtEngine` | PASS (`CreateDocument`, `OpenDocument`, `EngineName`, `EngineVersion` at `packages/kernel-crdt/ICrdtEngine.cs`) |
| `Sunfish.Kernel.Audit.AuditEventType` as `readonly record struct(string Value)` | PASS (verified at `packages/kernel-audit/`) |
| `IAuditTrail.AppendAsync(AuditRecord, CancellationToken)` | PASS (verified at `packages/kernel-audit/` per ADR 0049) |
| `Sunfish.Foundation.Ship.Common.ShipRole` | FORWARD-REF (W#46 build; expected signature in §A0.4) |
| `Sunfish.Foundation.Ship.Common.IPermissionResolver` | FORWARD-REF (W#46 build; expected signature in §A0.4) |
| `Sunfish.Foundation.Wayfinder.IOodWatchService` | FORWARD-REF (W#49 build; expected signature in §A0.4) |
| `NodaTime.Instant` | PASS (external dependency) |
