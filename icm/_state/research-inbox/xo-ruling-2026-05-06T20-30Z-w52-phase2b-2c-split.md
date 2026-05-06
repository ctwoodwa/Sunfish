---
type: ruling
workstream-or-chapter: W#52 Phase 2b/2c — DefaultTacticalRuleEngine + DefaultThreatTriggerService split ruling
resolves: cob-question-2026-05-06T20-15Z-w52-rule-engine-and-trigger-deferred.md
---

**XO ruling: ship as two separate PRs.**

- Phase 2b = DefaultTacticalRuleEngine (§2.1)
- Phase 2c = DefaultThreatTriggerService (§2.3)

**Rationale:**
1. §2.3 ThreatTriggerService is a 8-step pipeline with ISystemPrincipalProvider authority check —
   security council will have broader surface to cover if bundled with §2.1. Splitting ensures
   each gets a focused security review.
2. §2.1 (RuleEngine) has its own non-trivial invariant (Channel<T> per-tenant partitioning +
   rule-error-rate tracking + _firstSignalProcessed ordering). These are independent state machines.
3. The split-PR pattern established by W#50 (Phase 2a/2b) and W#54 (Phase 2/2b) is the active
   cohort precedent for complex Phase 2 implementations.
4. Total scope: ~3-4h for §2.1, ~4-5h for §2.3 — each is within single-iteration budget.

**Phase 2b acceptance criteria (DefaultTacticalRuleEngine):**
- Channel<TacticalSignal> per-tenant partitioning (ordering invariant: signals from same tenant
  processed in submission order; cross-tenant independent)
- Rule-error-rate tracking: >100 throws/min/rule → emit TacticalAuthorizationDenied denial once
  per minute (not per-throw); reset window after silence period
- `sunfish.*` prefix restriction: rules with names matching reserved prefix MUST be rejected at
  RegisterRule with ArgumentException
- _firstSignalProcessed ordering invariant (from hand-off): first signal sets the epoch; rules
  registered after first signal receive only signals from registration forward (no backfill)
- Security-engineering subagent mandatory pre-merge

**Phase 2c acceptance criteria (DefaultThreatTriggerService):**
- 8-step TryIssueAsync per hand-off: tenant binding → dedup → rate limits → signal-fingerprint
  budget → template substitution (overflow-safe) → audit ordering → AppendAsync error path
- ISystemPrincipalProvider authority check per tactical-p2-system-principal-authority-addendum.md
  (NOT IPermissionResolver — identity-based authority; systemPrincipal.ActorId used for audit)
- Emit TacticalAuthorizationDenied if systemPrincipal is null (see addendum §ruling)
- Security-engineering subagent mandatory pre-merge
- Also: DI wiring — AddSunfishTactical should wire DefaultAlertRouter + DefaultTacticalRuleEngine +
  DefaultThreatTriggerService together in Phase 2c (or a Phase 2d wiring-only PR)

**Note on COB's council findings from PR #697:** adversarial council ran; security council hit
usage limit. Security council (XO) ran in this session and found:
- M1 (XOR guard): APPLIED in amendment commit e55a13db on feat/w52-p2-alert-router
- M2 (exception-shadowing): disposed as Advisory (§Trust propagation policy is intentional and documented)

**Post-merge security council on DefaultTacticalRuleEngine (PR #704):** returned PASS-WITH-AMENDMENTS.
- Blocking #4 FIXED PR #707: cross-tenant error tracker (per-tenant key `(RuleName,TenantId)` now).
- Advisory #7 FIXED PR #707: tenant_id in signed AuditPayload body.
- Advisory findings deferred to Phase 2c (COB must address in DefaultThreatTriggerService PR):
  - **#1 (High):** `TryEmitFailureRateDenialAsync` uses `default` CT — on engine shutdown, in-flight
    signing/append cannot be cancelled; a caught `OperationCanceledException` would silently lose
    the denial. Fix: plumb an engine-lifetime `CancellationTokenSource` (or derived scope token);
    log `LogCritical` on cancelled emission, don't swallow.
  - **#2 (High):** `_ = TryEmitFailureRateDenialAsync(...)` — discard discards the Task; a sync
    throw before the first `await` goes unobserved. Fix: wrap in `Task.Run`+exception observer or
    use `ConfigureAwait`-based try/catch for the synchronous entry path.
  - **#3 (Medium):** Unbounded per-tenant pipe creation in `EvaluateStreamAsync`. A hostile signal
    source with fabricated `TenantId` values can exhaust memory. Fix in Phase 2c or follow-up:
    validate `TenantId` against a registry; cap concurrent pipes (configurable default 256); use
    `BoundedChannelOptions` per advisory.
  - **#9 (Low):** `GetRegisteredRules()` exposes rule object references. Consider returning
    `IReadOnlyList<string>` (rule names only) to limit state leakage surface.
