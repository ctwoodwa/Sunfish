# Intake — Atlas Integration-Config UI Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#34 Wayfinder configuration UX discovery)
**Request:** New ADR ~0067 (numbering speculative) specifying the Atlas UI surface for tenant-configurable integration providers — payments / messaging / mesh-VPN / captcha / email / SMS — composing on existing provider-neutrality contracts (ADRs 0013, 0051, 0052, 0061).
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The Wayfinder discovery (W#34) §5.6 identifies Layer 6 (integration configuration) as **Partial coverage**: provider-neutrality contracts and per-vendor adapters are fully specified (ADRs 0013, 0051, 0052, 0061), but no UX surface exists for admins to select providers, configure credentials, validate connections, and switch between providers. Each per-provider package implements its own ad-hoc config surface; cross-provider consistency is missing.

## Predecessor

- **ADR 0013** (Provider neutrality) — *"Domain modules never reference vendor SDKs directly."* The neutrality contract.
- **ADR 0051** (Foundation.Integrations.Payments) — `IPaymentGateway` adapter contract; providers-stripe / providers-square shape
- **ADR 0052** (Bidirectional Messaging Substrate) — messaging-provider adapter contract; providers-postmark / providers-sendgrid / providers-twilio shape
- **ADR 0061** (Three-Tier Peer Transport) — mesh-VPN provider abstraction; providers-mesh-headscale / providers-mesh-tailscale / providers-mesh-netbird shape; license-screening posture
- **ADR 0046-A2** (`EncryptedField` + `IFieldDecryptor`) — credential storage substrate (W#32 built 2026-04-30)
- **W#33 Mission Space matrix** — capability negotiation when provider selection affects Sunfish-feature availability

## Why net-new

Provider-neutrality contracts cover *how* adapters work (the data model) but not *how admins configure them* (the UX). Each provider currently has ad-hoc configuration in scattered locations; cross-provider consistency requires a unified Atlas surface.

## Scope

- **Provider-selection UX** — admin chooses which adapter implements a given integration category (Payments → providers-stripe; Email transactional → providers-postmark; etc.)
- **Credential capture** — API keys, secrets, callback URLs; sensitive-field UX with masking + reveal-on-explicit-action; encrypted storage via `EncryptedField`
- **Multi-provider per-category selection** — e.g., providers-postmark for transactional + providers-sendgrid for marketing simultaneously; routing rules
- **Connection validation** — pre-activation handshake with the provider; error-mode UX (invalid credentials, network unreachable, account suspended)
- **Capability-negotiation surface** — when provider X is selected, which Sunfish features become available / unavailable (cross-references W#33 Mission Space + ADR 0062 Negotiation Protocol)
- **Provider rotation** — graceful migration from provider A to provider B (no in-flight transactions lost; audit-trail shows the transition)
- **License-screening enforcement** — Atlas surface respects ADR 0061's license-posture rules (e.g., SSPL/BSL providers excluded by default; admin override requires acknowledgement)
- **WCAG 2.2 AA conformance** — credential-capture forms hit 3.3.7 (Redundant Entry) + 3.3.8 (Accessible Authentication) + sensitive-data masking a11y; diff-preview a11y per Stripe-pattern (W#34 Appendix B.3)

## Industry prior-art

- AWS Console (Connectivity & Networking → Integrations → Provider Selection)
- Stripe Dashboard (provider/connector configuration with diff-preview)
- HashiCorp Vault (provider secrets management)
- Microsoft Entra ID Conditional Access (multi-provider auth with per-policy binding)

## Dependencies and Constraints

- **Hard prerequisite**: ~ADR 0065 (Wayfinder system + Standing Order contract) — Atlas integration-config issues Standing Orders for provider changes
- **Composes on**: ADRs 0013, 0051, 0052, 0061, 0046-A2 (EncryptedField), 0062 (Mission Space Negotiation Protocol)
- **Effort estimate:** medium-large (~12–18h authoring + council review)
- **Council review posture:** standard adversarial + **WCAG / a11y subagent** (credential-capture forms hit 3.3.7 + 3.3.8) + **security-engineering subagent** (credential storage + transit + rotation)

## Affected Areas

- foundation-integrations: provider-config substrate
- providers-* packages: each adapter exposes its config schema for Atlas to render
- ui-core: integration-config Atlas surface contract
- accelerators/bridge: Bridge admin integration-config rendering (Bridge is where multi-tenant provider config lives)

## Downstream Consumers

- W#22 Leasing Pipeline (payment + email provider config)
- W#28 Public Listings (CAPTCHA + email provider config)
- Phase 2 commercial MVP (W#5) — payment / banking / SMS provider config across 6 tenants
- W#30 Mesh VPN / Cross-Network Transport (mesh-VPN provider selection)

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after ~ADR 0065 lands.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` §5.6 + §6.3 + §7
- Active workstream: W#34 in `icm/_state/active-workstreams.md`
- Sibling intake: `icm/00_intake/output/2026-05-01_wayfinder-system-and-standing-order-intake.md` (~ADR 0065; hard prerequisite)
