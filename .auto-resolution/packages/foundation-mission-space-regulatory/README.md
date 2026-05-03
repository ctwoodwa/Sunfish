# Sunfish.Foundation.MissionSpace.Regulatory

Foundation-tier substrate for runtime regulatory policy evaluation. Implements [ADR 0064](../../docs/adrs/0064-runtime-regulatory-policy-evaluation.md) + amendment A1.

## Reader caution

**Sunfish does not provide legal advice. This substrate is not a substitute for qualified counsel.** Phase 1 substrate ships the framework only — the *content* of policy rules per jurisdiction is a legal-review work product NOT in this package's scope. Phase 1 substrate-only deployments are **NOT** regulatory-compliant by virtue of the substrate alone (per ADR 0064-A1.8). General counsel must engage before Phase 3 rule-content authoring.

## Phase 1 scope (this package)

- Substrate types, contracts, and reference implementations for the regulatory dimension of the W#33 §7.2 mission-space cohort.
- 10 new `AuditEventType` discriminators in `Sunfish.Kernel.Audit`.
- Default regime-stance table (subject to legal-counsel review per A1.13): GDPR / CCPA / FHA / SOC 2 / EU AI Act = `InScope`; HIPAA = `CommercialProductOnly`; PCI-DSS v4 = `ExplicitlyDisclaimedOpenSource`.
- Composite-confidence `JurisdictionProbe` with the A1.15 tie-breaker rule (`user-declaration > tenant-config > IP-geo`).
- Bridge-tier `DataResidencyEnforcerMiddleware` (HTTP 451 RFC 7725).
- `ScreeningPolicy.AdvisoryOnly` opt-out path per A1.3.

## Out of Phase 1 scope

- **Per-jurisdiction rule content** (Phase 3) — gated on legal sign-off.
- **Cross-cutting refactor of ADR 0057 + ADR 0060** (Phase 4) — gated on Phase 3.
- **Commercial productization** (Phase 5) — separate work product.

See ADR 0064-A1.10 for the full 5-phase migration plan.
