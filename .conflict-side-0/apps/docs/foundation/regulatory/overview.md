# Foundation.MissionSpace.Regulatory substrate

`Sunfish.Foundation.MissionSpace.Regulatory` is the foundation-tier substrate for runtime regulatory policy evaluation — the building block behind data-residency enforcement, sanctions screening, jurisdictional probe resolution, and the per-regime stance acknowledgment surface across the Sunfish substrate cohort.

It implements [ADR 0064](../../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) and amendment A1.

---

## Reader caution

**Sunfish does not provide legal advice. This substrate is not a substitute for qualified counsel.** Phase 1 substrate ships the framework only — the *content* of policy rules per jurisdiction is a legal-review work product NOT in this package's scope. Phase 1 substrate-only deployments are **NOT** regulatory-compliant by virtue of the substrate alone (per ADR 0064-A1.8). General counsel must engage before Phase 3 rule-content authoring.

This caution propagates from A1.2 and is surfaced in the package README, every audit-enabled DI registration, and every consumer-facing artifact.

---

## What it gives you

| Type | Role |
|---|---|
| `JurisdictionProbe` | Composite-confidence probe result (jurisdiction code + `Confidence` band + signal sources + probed-at). |
| `ICompositeJurisdictionProbe` / `DefaultCompositeJurisdictionProbe` | Pure-function 3-signal composition with the A1.15 tie-breaker (`user-declaration > tenant-config > IP-geo`) on disagreement. |
| `JurisdictionalPolicyRule` | Wire-shape for a single counsel-reviewed rule. Phase 1 ships the type + JSON Schema; Phase 3 ships the content. |
| `IPolicyEvaluator` / `DefaultPolicyEvaluator` | `RelevantFeatures` pre-filter; empty rule set → silent `Pass` per A1.8; `InvalidateCache()` per A1.7. |
| `IDataResidencyEnforcer` / `DefaultDataResidencyEnforcer` | Pure function over `(recordClass, jurisdictionCode)`; prohibited list takes precedence over allowed list. |
| `ISanctionsScreener` / `DefaultSanctionsScreener` | Operator-decision-aware emit-only; `ScreeningPolicy.AdvisoryOnly` opt-out per A1.3. |
| `IDataResidencyEnforcerMiddleware` / `DataResidencyEnforcerMiddleware` | Bridge-boundary ASP.NET Core middleware; HTTP 451 (Unavailable for Legal Reasons; RFC 7725) per A1.4. |
| `RegimeAcknowledgment` + `DefaultRegimeStances` | 7-entry default stance table per A1.13: GDPR / CCPA / FHA / SOC 2 / EU AI Act = `InScope`; HIPAA = `CommercialProductOnly`; PCI-DSS v4 = `ExplicitlyDisclaimedOpenSource` (post-A1.13 reframe). |
| `RegulatoryAuditPayloads` + `RegulatoryAuditEmitter` | Body factory + central emitter with W#34 P4 dedup pattern (per-event ConcurrentDictionary keyed on per-event tuple). |

## Composite jurisdiction probe (A1.5 + A1.15)

`DefaultCompositeJurisdictionProbe` aggregates 3 signals — IP-geo, user-declaration, tenant-config — and produces a single `JurisdictionProbe` with a confidence band:

| State | Confidence |
|---|---|
| 3-of-3 signals agree | `High` |
| 2 signals present, both agree | `Medium` |
| 1 signal present (others null) | `Low` |
| Any disagreement | `Low` (tie-breaker `user-declaration > tenant-config > IP-geo` resolves the code) |
| 0 signals present | null result (no probe) |

## Per-event audit dedup (A1.7)

| Event | Window | Key |
|---|---|---|
| `PolicyEnforcementBlocked` | 1h | `(feature, jurisdiction, rule_id)` |
| `JurisdictionProbedWithLowConfidence` | 1h | `(jurisdiction)` |
| `DataResidencyViolation` | 1h | `(record_class, jurisdiction)` |
| `SanctionsScreeningHit` | 1h | `(subject, list_source)` |
| `RegimeAcknowledgmentSurfaced` | 24h | `(regime)` |
| `PolicyEvaluated` / `EuAiActTierClassified` / `RegulatoryRuleContentReloaded` / `RegulatoryPolicyCacheInvalidated` / `SanctionsAdvisoryOnlyConfigured` | unrate-limited | — |

## Audit emission

10 new `AuditEventType` discriminators ship with this substrate:

| Event type | Emitted by |
|---|---|
| `PolicyEvaluated` | `IPolicyEvaluator.EvaluateAsync` (always-on telemetry per A1.7) |
| `PolicyEnforcementBlocked` | A policy rule's `EnforcementAction` blocked the operation |
| `JurisdictionProbedWithLowConfidence` | Probe resolved with `Confidence.Low` |
| `DataResidencyViolation` | Record-class write violated a `DataResidencyConstraint` |
| `SanctionsScreeningHit` | Sanctions screener matched against a list |
| `RegimeAcknowledgmentSurfaced` | `RegimeAcknowledgment` shown to a UX consumer |
| `EuAiActTierClassified` | An `EuAiActTierClassification` was assigned (Phase 1 carries the type; emission paths are downstream) |
| `SanctionsAdvisoryOnlyConfigured` | Operator configured `ScreeningPolicy.AdvisoryOnly` (one-shot at host configuration) |
| `RegulatoryRuleContentReloaded` | Rule content / sanctions list was reloaded (per A1.16) |
| `RegulatoryPolicyCacheInvalidated` | Policy-evaluation cache invalidated due to probe-status transition (per A1.7) |

`RegulatoryAuditPayloads` is the canonical body factory — alphabetized keys, opaque to the substrate, mirrors the `VersionVectorAuditPayloads` / `MissionSpaceAuditPayloads` conventions.

Audit emission is opt-in: pass `IAuditTrail` + `IOperationSigner` + `TenantId` via the audit-enabled DI overload (W#32 both-or-neither). Without them, the substrate runs but no records fire.

## Bridge HTTP 451 middleware (A1.4)

`DataResidencyEnforcerMiddleware` is an ASP.NET Core `IMiddleware` that resolves `(recordClass, jurisdictionCode)` from request context — by default from the `X-Sunfish-Record-Class` and `X-Sunfish-Jurisdiction` headers — and short-circuits with **HTTP 451 Unavailable for Legal Reasons** (RFC 7725) when the underlying `IDataResidencyEnforcer` returns `IsPermitted=false`.

Phase 1 substrate omits the `Retry-After` header (semantic = "never"); hosts that know a time-bounded restriction subclass the middleware and override `ResolveContextAsync` to set the header.

## Per-regime stance table (A1.13)

The default `RegimeAcknowledgment` set is subject to legal-counsel review:

| Regime | Stance | Rationale key |
|---|---|---|
| CCPA | `InScope` | `regulatory.stance.ccpa.inscope` |
| EU AI Act | `InScope` (placeholder) | `regulatory.stance.euaiact.inscope.placeholder` |
| FHA | `InScope` | `regulatory.stance.fha.inscope` |
| GDPR | `InScope` | `regulatory.stance.gdpr.inscope` |
| SOC 2 | `InScope` | `regulatory.stance.soc2.inscope` |
| HIPAA | `CommercialProductOnly` | `regulatory.stance.hipaa.commercial-only` |
| PCI-DSS v4 | `ExplicitlyDisclaimedOpenSource` (per A1.13 reframe) | `regulatory.stance.pcidss.disclaimed` |

The `RationaleKey` values are localization keys — the host resolves them to human-readable text via its localization stack.

## API at a glance

```csharp
// Bootstrap (audit-disabled — test/bootstrap). Wires the substrate with empty
// rule / constraint / sanctions sources. Hosts override after this call to
// plug counsel-reviewed sources.
services.AddInMemoryRegulatoryPolicy();

// Bootstrap (audit-enabled — production; both IAuditTrail + IOperationSigner
// must already be registered, per the W#32 both-or-neither pattern).
services.AddInMemoryRegulatoryPolicy(currentTenantId);

// Audit-enabled with operator-opt-out for sanctions screening:
services.AddInMemoryRegulatoryPolicy(
    currentTenantId,
    ScreeningPolicy.AdvisoryOnly,
    operatorPrincipalId: "ops-admin");
// → fires SanctionsAdvisoryOnlyConfigured at first resolution.

// Wire the Bridge middleware in the Bridge accelerator's pipeline:
app.UseMiddleware<IDataResidencyEnforcerMiddleware>();
```

## Phase 1 scope (this package)

- Substrate types, contracts, and reference implementations.
- 16 type signatures (7 enums + 9 records) per A1.6.
- 10 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`.
- Composite-confidence probe + 3-signal tie-breaker per A1.15.
- Default-stance table per A1.13 (PCI-DSS reframed to `ExplicitlyDisclaimedOpenSource`).
- Bridge HTTP 451 middleware per A1.4.
- `ScreeningPolicy.AdvisoryOnly` opt-out path per A1.3.
- W#34 P4 dedup pattern per A1.7.
- Canonical JSON Schema for `JurisdictionalPolicyRule` per A1.14.
- `AddInMemoryRegulatoryPolicy()` DI extension (audit-disabled + audit-enabled overloads).

## Out of Phase 1 scope

- **Per-jurisdiction rule content** (Phase 3) — gated on legal-counsel engagement letter.
- **Cross-cutting refactor of ADR 0057 + ADR 0060** (Phase 4) — gated on Phase 3.
- **Sunfish.Regulatory.Jurisdictions taxonomy charter** (Phase 2) — separate work product.
- **Commercial productization** (Phase 5) — separate work product.

See ADR 0064-A1.10 for the full 5-phase migration plan.

## Cohort lesson — substrate-only Phase 1 means substrate-only

This package ships the substrate. It does **not** ship rule content, regime-specific evaluation strategies, or compliance certifications — those compose downstream and require legal-counsel engagement. Per A1.8, deployments that compose, evaluate, and audit through this substrate alone are **not regulatory-compliant** without the corresponding rule content and counsel review.
