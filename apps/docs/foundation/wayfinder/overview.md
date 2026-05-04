# Foundation.Wayfinder substrate

`Sunfish.Foundation.Wayfinder` is the foundation-tier substrate for the Wayfinder system + Standing Order contract — the building block behind every operator-issued configuration change in Sunfish: feature toggles, tenant policy, integration config, security posture, per-user preferences. Per [ADR 0065](../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md).

## What it gives you

| Type | Role |
|---|---|
| `StandingOrder` | A single operator intent: bundles one or more `(path, oldValue, newValue)` triples under a scope, an actor, an issued timestamp, an optional approval chain, and a lifecycle state. |
| `StandingOrderTriple` | One atomic mutation: dotted path within scope + old + new JSON values. |
| `ApprovalChain` + `ApprovalStep` | Multi-party sign-off attached to an order. |
| `StandingOrderScope` (5) | `User` / `Tenant` / `Platform` / `Integration` / `Security`. |
| `StandingOrderState` (6) | `Issued` → `Validated` → `Applied`, plus terminal `Rescinded` / `Rejected` / `Conflicted`. |
| `IStandingOrderRepository` | Per-tenant log accessor. `CrdtStandingOrderRepository` ships in Phase 2 over `Sunfish.Kernel.Crdt.ICrdtEngine`. |
| `IStandingOrderIssuer` | Audit-by-construction issuance: `IssueAsync` + `RescindAsync` both require an `IAuditTrail` parameter (not optional). `DefaultStandingOrderIssuer` ships in Phase 2. |
| `IStandingOrderValidator` (+ `StandingOrderValidatorPriority`) | Deterministic validator chain: `Schema=100` → `Policy=200` → `Authority=300` → `Conflict=400`. The chain runs every validator to accumulate issues; Block-severity issues fail the verdict and reject the order. Rejection still emits an audit event. |
| `IAtlasProjector` (+ `AtlasView` / `AtlasSettingSnapshot` / `AtlasSchemaDescriptor` / `AtlasSearchHit`) | Phase 3a — projects per-tenant logs into a queryable settings catalog with last-writer-wins-by-IssuedAt-then-IssuedBy at the (Scope, Path) grain. |
| `Sunfish.Wayfinder.Analyzers.SchemaRegistrationAnalyzer` | Phase 3b — Roslyn diagnostic `SUNFISH_WAYFINDER001` warning when a project calls `AddSunfishWayfinder()` but never registers an `AtlasSchemaDescriptor`. |

## Audit-by-construction (per ADR 0065 §4)

Five new `Sunfish.Kernel.Audit.AuditEventType` constants flow through every issuance / rescission / conflict-resolution / rejection event:

| AuditEventType | Emitted by |
|---|---|
| `StandingOrderIssued` | Successful `IStandingOrderIssuer.IssueAsync` (state = Validated) |
| `StandingOrderAmended` | Re-issuance after a `Conflicted` order is amended (Phase 4+ flow) |
| `StandingOrderRescinded` | `IStandingOrderIssuer.RescindAsync` — emits a NEW record without redacting the original (audit immutability per ADR 0049) |
| `StandingOrderRejected` | Block-severity validation failure; state flips to `Rejected` |
| `StandingOrderConflictResolved` | Per concurrent-issuance pair on the same `(Scope, Path)`; emitted once with both `StandingOrderId` values |

The `IStandingOrderIssuer` interface enforces this at the type level — both `IssueAsync` and `RescindAsync` require an `IAuditTrail` parameter. A configuration change made via the issuer cannot emit-elide an audit record. (`IStandingOrderRepository.AppendAsync` is the substrate-replay path used during snapshot rehydration; production write paths route through the issuer.)

## CRDT-native (per ADR 0065 §2)

The per-tenant Standing Order log is materialized at `wayfinder/standing-orders/{tenantId}` via `Sunfish.Kernel.Crdt.ICrdtEngine`. Concurrent issuances on disjoint paths merge cleanly. Concurrent issuances on the same `(Scope, Path)` produce `StandingOrderState.Conflicted` for the loser of the LWW tiebreak (substrate-tier guarantee). Per ADR 0065 §"Decision §7" + W#34 §5.7, **adapters MUST present a single-action amend-and-re-issue UX (not a three-way merge dialog)** — the latter pattern fails WCAG 3.3.7 redundant-entry for non-technical operators.

## Atlas projection (per ADR 0065 §5)

`IAtlasProjector.ProjectAsync(tenantId, scopeFilter)` walks the per-tenant log, applies LWW-by-IssuedAt-then-IssuedBy, skips Rescinded / Rejected / Conflicted / Issued (validation-incomplete) orders, and returns an `AtlasView` keyed by composite `"<scope>:<path>"` strings (so the same path under two scopes yields two distinct snapshots, never a non-deterministic collision).

`IAtlasProjector.SearchAsync(tenantId, query, limit)` streams hits descending by score:

| Match | Score |
|---|---|
| Path exact equality | 1.0 |
| Path prefix | 0.85 |
| Display-name prefix | 0.75 |
| Path substring | 0.55 |
| Display-name substring | 0.4 |

Deterministic tiebreak by path under `string.CompareOrdinal`.

## Schema-registration analyzer (Phase 3b)

`Sunfish.Wayfinder.Analyzers.SchemaRegistrationAnalyzer` emits `SUNFISH_WAYFINDER001` (Warning) on every `AddSunfish*()` invocation in a project that never instantiates an `AtlasSchemaDescriptor`. Detection is purely syntactic — the analyzer walks `InvocationExpressionSyntax` for `AddSunfish`-prefixed calls and `ObjectCreationExpressionSyntax` for `AtlasSchemaDescriptor` constructions; if any AddSunfish call was seen but no descriptor was created, every call site gets a diagnostic. Adding one descriptor anywhere in the project clears the warning everywhere.

The cost trade-off: false positives on unrelated `AddSunfishX` methods are accepted; false negatives (target-typed `new()` silently crediting a descriptor) are explicitly rejected per the W#42 P3b council A-1 ruling.

## Where the substrate lands

| Phase | PR | Status |
|---|---|---|
| P1 — types + interfaces + 5 AuditEventType | #503 | merged |
| P2 — CRDT-backed repository + reference issuer | #504 + #505 (amendments) | merged |
| P3a — Atlas projector + search | #510 | merged |
| P3b — SchemaRegistrationAnalyzer | #513 | merged |
| P3b — perf tests (P95 ≤ 200ms cold / ≤ 100ms warm at 10K projection per ADR 0065 council F9) | follow-up | deferred |
| P4 — kitchen-sink wiring + apps/docs + WCAG baseline | this PR | in flight |
| P5 — ledger flip → built | follow-up | queued |

## Consumers

- `~ADR 0066` — Helm + Identity Atlas (consumes the Wayfinder system)
- `~ADR 0067` — Atlas integration config (consumes `AtlasView`)
- `~ADR 0068` — Tenant security policy (issues Standing Orders for security posture)
- `ADR 0009 amendment` (W#43) — extends FeatureManagement with operator-issued feature toggles via Standing Orders (load-bearing 5th concept)

## See also

- [ADR 0065](../../../docs/adrs/0065-wayfinder-system-and-standing-order-contract.md) — Wayfinder System + Standing Order Contract
- [ADR 0049](../../../docs/adrs/0049-audit-trail.md) — audit immutability that Wayfinder rescission semantics compose
- [ADR 0028](../../../docs/adrs/0028-crdt-engine-selection.md) — CRDT engine that the per-tenant log materializes against
- [ADR 0048](../../../docs/adrs/0048-anchor-multi-backend-maui.md) — multi-backend MAUI + native a11y APIs (UIA / NSAccessibility / UIAccessibility / AccessibilityNodeInfo) that adapter Stage 06s surface against
- [WCAG 2.2 AA + EN 301 549 v3.2.1 conformance baseline](wcag.md)
