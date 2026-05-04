# Intake — Tenant-Configurable Security Policy + Atlas Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#34 Wayfinder configuration UX discovery)
**Request:** New ADR ~0068 (numbering speculative) specifying tenant-configurable security posture — required MFA factors, device-attestation requirements, audit-log retention policy, role-key rotation cadence, recovery-contact enforcement — distinct from the OSS-project threat model in ADR 0043.
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

> **Reader caution**: this intake names a layer where MFA / attestation policy intersects regulatory compliance (HIPAA / PCI-DSS / SOC 2 / EU AI Act). The downstream ADR MUST engage qualified general counsel + security-engineering perspective before specifying enforcement behavior. This intake describes a *gap*, not a *solution*.

---

## Problem Statement

The Wayfinder discovery (W#34) §5.7 identifies Layer 7 (security configuration) as a **genuine gap** — the A4 spot-check confirmed a citation category mismatch: ADR 0043 (Unified Threat Model) covers the OSS-project merge-path threat model, NOT tenant-configurable security posture. Per-domain security ADRs (0046 key handling, 0049 audit, 0058 vendor onboarding, 0061 transport tiers) exist but no cross-cutting ADR specifies the policy layer (what posture each tenant configures) or the UX surface (how admins set those policies).

This is the **highest commercial priority** of the W#34 follow-ons per discovery §6.4: security-policy gaps are launch-blockers for regulated-industry tenants.

## Predecessor

**No clean predecessor** for tenant-configurable security policy. Adjacent:
- ADR 0046 + 0046-a1 (key handling — the *mechanism*; not policy)
- ADR 0049 (audit infrastructure — the *substrate*; not retention policy)
- ADR 0058 (vendor onboarding security posture — domain-specific)
- ADR 0061 (transport tiers + attestation at federation time — not tenant-policy)
- W#33 Mission Space §5.9 + ~ADR 0064 (regulatory dimension; in W#33 follow-on queue, not yet shipped) — overlapping but distinct (~0064 is runtime jurisdictional evaluation; this is admin-configured security posture)

## Industry prior-art

- **Apple Identity / Managed Apple ID + DEP** — enterprise MFA enrollment policy, device attestation via T2/Apple Silicon Secure Enclave; admin configures via Apple Business Manager
- **Microsoft Entra ID Conditional Access** — policy-based access control with MFA, device-trust, location signals; Azure portal UX is the industry reference for tenant-security-policy editing
- **Okta Policy Framework** — tenant-configurable policies (sign-on, password, MFA enrollment, session); per-policy + per-application binding
- **Open Policy Agent (OPA) / Rego** — general-purpose policy language; could ground the policy data model

## Scope

- **Security-policy data model** — typed-DSL composing OPA/Rego patterns (recommend) vs free-form rules vs decision-tree
- **MFA enrollment policy** — required factors per role; grace period for enrollment; recovery flow; per-jurisdiction defaults
- **Device-attestation requirement** — when attestation is mandatory vs optional; what counts as "attested" (T2 / Secure Enclave / StrongBox / TPM 2.0); composition with ADR 0061's federation-time attestation
- **Audit-policy specification** — retention windows; export rights; right-to-be-forgotten interplay with audit immutability (ADR 0049); per-jurisdiction defaults (HIPAA 6yr / GDPR variable / SOC 2 7yr)
- **Role-key rotation policy** — cadence (default 90d); trigger conditions (compromise indicators); grace period; emergency-rotation flow
- **Recovery-contact policy** — enrollment deadline; minimum count (default 1, with multi-actor optional); verification cadence
- **Atlas UI surface** — admin sets policies; end-user surfaces ("your team requires MFA enrollment within 30 days")
- **Standing Order shape for security-policy changes** — higher-stakes than user preferences; require multi-actor approval (composes Phase 2 commercial scope's permissions matrix); elevated audit emission
- **WCAG 2.2 AA conformance** — **3.3.8 Accessible Authentication (AA, *new*)** is *directly* in this scope. Cognitive-function tests (typing OTP from memory, image-puzzle CAPTCHA) are non-conforming without accessible alternative. EN 301 549 procurement risk if missed.

## Dependencies and Constraints

- **Hard prerequisite**: ~ADR 0065 (Wayfinder system + Standing Order contract) — security policy issues Standing Orders
- **Soft cross-reference**: ~ADR 0064 (W#33 follow-on regulatory; queued but not yet shipped) — may compose if jurisdictional defaults drive security-policy defaults
- **Hard requirement**: general counsel engagement before specifying enforcement behavior
- **Effort estimate:** large (~18–24h authoring + extended council review)
- **Council review posture:** standard adversarial + **security-engineering subagent** + **Pedantic Lawyer subagent** (where MFA / attestation policy intersects regulatory compliance) + **WCAG / a11y subagent** (3.3.8 Accessible Authentication)

## Affected Areas

- foundation-recovery: composed (key rotation; recovery-contact verification)
- foundation-policy (potentially new package): policy DSL + evaluator
- ui-core: security-policy Atlas surface contract
- accelerators/bridge: tenant-policy admin UX (Bridge is where multi-tenant policy lives)

## Downstream Consumers

- **W#22 Leasing Pipeline** — Phase 6 compliance half (currently deferred); consumes audit-policy + MFA-policy for FCRA/HIPAA
- Phase 2 commercial MVP (W#5) — multi-actor + multi-tenant security policy
- All regulated-industry tenants (HIPAA / PCI-DSS / SOC 2 / EU AI Act)

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after ~ADR 0065 lands. **General counsel engagement required** before Stage 02 Architecture.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.7 + §6.4 + §7
- Active workstream: W#34 in `icm/_state/active-workstreams.md`
- Sibling intake: `icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md` (~ADR 0065; hard prerequisite)
- W#33 sibling: `icm/00_intake/output/2026-04-30_runtime-regulatory-policy-evaluation-intake.md` (~ADR 0064; cross-reference for jurisdictional defaults)
