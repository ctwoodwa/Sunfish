# ADR 0078 Council Review — 2026-05-05

**ADR:** `docs/adrs/0078-ood-watch-rotation.md`
**Branch:** `docs/adr-0078-ood-watch-rotation`
**Date:** 2026-05-05
**Verdict:** NEEDS-AMENDMENT → amendments applied same session → **re-review queued**

---

## Final verdict (post-amendment)

Amendments applied in the same authoring session. Pending re-review to confirm all 4 Majors
resolved. Structure and pre-acceptance audit are solid; no Critical findings were raised.

---

## Findings

| ID | Perspective | Severity | Finding | Resolution |
|---|---|---|---|---|
| R1 | Pedantic Lawyer | Major | §3 notification Standing Order had no concrete `StandingOrderScope` assigned in body; §A0.3 proposed `Platform` which contradicts per-tenant semantics | Applied: §3 updated to `StandingOrderScope.Tenant`; Open Question #3 removed; §A0.3 corrected |
| R2 | Security Engineer | Major | §Trust impact signing message omitted `IOperationSigner` nonce/issuedAt; implementer could build nonce-less scheme | Applied: §Trust impact point 1 now names `IOperationSigner.SignAsync` nonce+issuedAt binding; server-set `StartedAt = envelope.issuedAt ± 5min` |
| R3 | Security Engineer | Major | §2 persistence uniqueness stated in prose only; no DB-level enforcement mechanism named | Applied: §2 "Persistence uniqueness contract" block added; partial unique index on `(TenantId, OodRole)` filtered to `State = Active` required |
| R4 | WCAG/a11y Specialist | Major | `aria-live="assertive"` assigned to handover announcements; assertive should be reserved for expiry (operational degradation) | Applied: handover downgraded to `polite`; `assertive` kept only for expiry |
| R5 | WCAG/a11y Specialist | Minor | §7 missing dialog a11y contract (role, focus, aria-labelledby, Esc) | Applied: §7.1 added |
| R6 | WCAG/a11y Specialist | Minor | Announcement text hard-coded "Officer of the Deck" — breaks EOOW | Applied: `{Role.DisplayName}` parameterization in both relief + expiry announcements |
| R7 | Pedantic Lawyer | Minor | `OodWatchStarted/Expired` severity was prose-only; no exact string | Applied: §4 now specifies `"severity": "High"` / `"severity": "Normal"` as literal JSON payload strings |
| R8 | Pedantic Lawyer | Minor | `StandingOrder.IssuedDuringWatchId` binary compat concern not surfaced as halt-condition | Applied: §Decision §1 binary-compat halt-condition block added; Phase 1 checklist halt updated |
| R9 | Skeptical Implementer | Minor | `IOodWatchExpiryService` sweep needed all-tenants query but `IOodWatchRepository` had no such method | Applied: `GetExpiredCandidatesAsync(Instant cutoff, CancellationToken)` added to `IOodWatchRepository` in §2 |
| R10 | Skeptical Implementer | Minor | `OodWatchConflictException` base class (`InvalidOperationException`) not confirmed vs. domain base; no `/// <exception>` XML docs | Applied: Open Question #3 added for base-class verification; XML doc `<exception>` tags added in §2 interface snippets |
| R11 | Skeptical Implementer | Minor | `GetActiveWatchAsync` lacked normative "returns Active only" contract in §2 | Applied: `/// <summary>` doc added specifying "returns null for Relieved or Expired" |
| R12 | Skeptical Implementer | NM | §A0.1 had `PR #...` placeholder | Applied: replaced with `(lines 119-123)` reference |
| R13 | Pedantic Lawyer | NM | `TenantId`/`ActorId` in `Foundation.Assets.Common` is anomalous namespace | Noted; no action required in this ADR |
| R14 | Skeptical Implementer | NM | W#49 not yet allocated in active-workstreams.md | Applied: W#49 row added in same commit |

---

## Structural-citation spot-check

| Symbol | Status |
|---|---|
| `Sunfish.Foundation.Wayfinder.StandingOrder` | PASS (forward-reference; W#42 ready-to-build; halt-condition documented) |
| `Sunfish.Foundation.Assets.Common.TenantId` | PASS (verified at `packages/foundation/Assets/Common/TenantId.cs`) |
| `Sunfish.Foundation.Assets.Common.ActorId` | PASS (verified at `packages/foundation/Assets/Common/ActorId.cs`) |
| `Sunfish.Kernel.Audit.AuditEventType` as `readonly record struct(string Value)` | PASS (verified in `packages/kernel-audit/`) |
| `IAuditTrail.AppendAsync(AuditRecord, CancellationToken)` | PASS (ADR 0049 lines 119-123) |
| `IOperationSigner` in `packages/foundation/Crypto/` | PASS (verified; `SignAsync<T>(payload, issuedAt, nonce, ...)` exists) |
| `NodaTime.Instant` | PASS (external dependency) |
| `StandingOrderTriple(Path, OldValue, NewValue)` | PASS (ADR 0065 §1 exact shape) |
| `StandingOrderScope.Tenant` | PASS (enum value confirmed in ADR 0065) |
