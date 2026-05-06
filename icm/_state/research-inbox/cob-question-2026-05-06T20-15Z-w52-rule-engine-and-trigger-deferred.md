---
type: question
workstream-or-chapter: W#52 Phase 2 — DefaultTacticalRuleEngine + DefaultThreatTriggerService deferred
last-pr: 696
---

PR for W#52 Phase 2 ships **DefaultAlertRouter only** (§2.2). The other two
Phase 2 implementations (§2.1 DefaultTacticalRuleEngine + §2.3
DefaultThreatTriggerService) are deferred to follow-up PRs because the
combined scope exceeds a single-iteration tight-PR target and each carries
non-trivial subsystem complexity:

**§2.1 DefaultTacticalRuleEngine — deferred:**
- Channel<TacticalSignal> per-tenant partitioning (signal ordering invariant)
- Rule-error-rate tracking (>100 throws/min/rule → emit denial once/min)
- `sunfish.*` prefix restriction enforcement (council-grade trust check)
- `_firstSignalProcessed` ordering invariant on RegisterRule

**§2.3 DefaultThreatTriggerService — deferred:**
- 8-step TryIssueAsync pipeline (tenant binding, dedup, rate limits,
  signal-fingerprint budget, template substitution with overflow,
  audit ordering, AppendAsync error path)
- ISystemPrincipalProvider authority check per
  `tactical-p2-system-principal-authority-addendum.md`
  (NOTE: ShipRole.System does NOT exist; addendum says use identity-based
  authority check via systemPrincipal.ActorId, not IPermissionResolver)

Phase 2 (this PR — DefaultAlertRouter only) ships:
- 8-step RouteAsync per ADR 0081 §2 contract
- AlertId regex validation + per-(TenantId, RuleName) sliding rate limit
- Audit emission ordered (AnomalyDetected → AlertRouted → destination)
- High-priority allowlist downgrade per §8.3
- Tenant binding per §8.2 (TacticalUnauthorizedException on mismatch)
- Audit emission gated on BOTH IAuditTrail AND IOperationSigner per the
  W#50 P2 cohort precedent — fail-closed silent skip when missing,
  not placeholder-bytes throw-and-swallow.
- 8 unit tests covering all 6 hand-off acceptance items + tenant-mismatch
  + no-audit-still-routes path.

**What would unblock me on the deferred parts:** XO ruling on whether
to ship §2.1 + §2.3 as one Phase 2b PR or split per service. Phase 2a
(this PR) and Phase 2b can land independently since they share no
state.
