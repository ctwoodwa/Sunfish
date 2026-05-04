# Intake — Sunfish Wayfinder Configuration UX Research

**Date:** 2026-05-01
**Requestor:** CO (BDFL) via XO research session
**Request:** Survey industry best practices for configuration management + UX patterns for finding and editing configurations across desktop OS (macOS / Windows / Linux), mobile OS (iOS / iPadOS / Android), reduced-surface form factors (WatchOS / VisionOS), and pro-tool / SaaS exemplars (VSCode / JetBrains / Stripe / Linear / Notion). Synthesize a unified configuration UX surface for Sunfish — the **Wayfinder** — covering all 8 configuration layers (user prefs / tenant config / feature management / capability declarations / domain config / integration config / security config / account-identity).
**Pipeline variant:** `sunfish-gap-analysis`
**Stage:** 00 → 01 (proceed to discovery)
**UPF plan:** `~/.claude/plans/sunfish-wayfinder-configuration-research.md` (UPF v1.2; Phase 2 spot-check 2-of-3 PASS, 1 pre-identified gap; A4 holds)
**Active workstream:** W#34 in `icm/_state/active-workstreams.md` (`design-in-flight`)

---

## Problem Statement

Sunfish has eight scattered configuration layers — user preferences, tenant configuration, feature management (ADR 0009), capability declarations (W#33 Mission Space + ADRs 0062/0063 just-shipped), domain configuration (ADRs 0055 + 0056), integration configuration (ADRs 0013 + 0051 + 0052 + 0061), security configuration (gap — see §A4 spot-check below), and account-identity (ADR 0046) — but **no unified UX surface** that lets users and admins find, understand, edit, validate, and audit configuration consistently across these layers. Each feature implementer derives "where do I put this setting, how does the user find it, how is the change recorded, who can change it, how is it audited" from scratch. Cross-platform OS settings (macOS Settings, iOS Settings, Android Settings, Windows 11 Settings) have 13+ years of mature precedent — search-as-you-type, dual-surface UI+JSON, schema-driven forms, change-audit, sync — that Sunfish can adopt rather than re-derive.

This research project produces a single canonical **Wayfinder** discovery doc that surveys industry configuration UX, maps each Sunfish layer's current coverage, identifies gaps, and queues 3–5 follow-on ADR intakes. The matrix is a *map*, not a *specification* — the actual Wayfinder system architecture, Helm composition, Atlas UI contract, and Standing Order type signature each become their own downstream ADRs.

## Naming (locked 2026-05-01)

| Layer | Name | What it is |
|---|---|---|
| System / umbrella | **Sunfish Wayfinder** | The configuration system as a whole |
| Live-state pane | **Helm** | Glance pane: current Mission Envelope + sync state + active team + quick toggles |
| Deep configuration surface | **Atlas** | User-facing settings UI; navigable pages of structured configuration |
| Configuration-change record | **Standing Order** | Internal type/event representing a single configuration change |

Architecture: Atlas-as-projection-of-Standing-Orders. Atlas is what users see; Standing Orders are append-only events that materialize into Atlas's effective state. Composes naturally with **ADR 0049** audit trail (every Standing Order is an audit event by construction) and **ADR 0028** CRDT semantics (Standing Orders are append-only operations).

Composes with **W#33 Mission Space**: Wayfinder is the *operational system* that operates within the Mission Space *envelope*.

## Affected Areas

This research is systemic — all packages and accelerators are within the Wayfinder's analytical reach. The matrix reads existing artifacts; it does not modify any package directly. Implementation work flows out via downstream ADR intakes.

- **foundation:** referenced (ADR 0007 bundle manifest; ADR 0028 CRDT; ADR 0049 audit; ADR 0009 FeatureManagement)
- **foundation-taxonomy:** referenced (ADR 0056 — domain-config layer)
- **foundation-recovery:** referenced (ADR 0046 — identity layer)
- **foundation-integrations:** referenced (ADRs 0013 + 0051 + 0052 — integration layer)
- **ui-core:** referenced (Atlas + Helm contracts; ADR 0041 dual-namespace degradation primitive)
- **ui-adapters-blazor / ui-adapters-react:** referenced (Atlas per-adapter rendering)
- **blocks-\*:** consumed downstream by W#22 (regulatory/commercial-tier rows), W#23 (form-factor adaptations), W#28 (tier-aware rendering), W#29 (Owner Web Cockpit — adjacent surface)
- **apps/docs:** future Atlas user-facing documentation
- **apps/kitchen-sink:** future Wayfinder demonstration
- **accelerators/anchor + accelerators/bridge:** referenced (per-accelerator Wayfinder rendering — Anchor is desktop-style sidebar; Bridge is web-admin)

## Dependencies and Constraints

- **No hard blockers.** Naming-collision resolution is the only structural risk and is resolved by this intake (full reasoning in `project_workstream_34_wayfinder_naming.md`).
- **30-day update check (Stage 0.8 re-run, executed 2026-05-01):** clean. **Notable**: ADR 0062 (Mission Space Negotiation Protocol — runtime) and ADR 0063 (Mission Space Requirements — install-UX) both shipped 2026-04-30/05-01. These are now load-bearing predecessors for the Helm and Atlas surfaces respectively, *strengthening* the predecessor gradient.
- **A4 spot-check (executed 2026-05-01):** 2 of 3 randomly-selected layers PASS; 1 (Layer 7 Security configuration — ADR 0043) FAILed substantive coverage (ADR 0043 is the OSS-project threat model, not tenant-configurable security posture). A4 holds (1-of-3 fail below the F4 reset threshold of ≥2 fails). Layer 7's predecessor will be adjusted in the discovery doc — either swap to a different ADR or tag as a genuine gap (analog to W#33's Version Vector case).
- **Effort budget:** xhigh ~12–16h XO/COB time; ~90–120 min CO sparring/review across 5 phase gates. Hard stop 20h.
- **Soft dependencies:** outputs of W#33 Mission Space follow-on authoring (ADRs 0062 + 0063 + ~0064) become predecessors as they land; ~ADR 0064 (regulatory) still queued.
- **Pipeline exit:** **"Approved Gap"** per `icm/pipelines/sunfish-gap-analysis/routing.md`.

## Configuration Layers in Scope (8 layers)

1. **User preferences** (cosmetic, behavioral) — *gap; no current infrastructure*
2. **Tenant configuration** (locale, jurisdiction, multi-actor permissions) — *partial; scattered across ADRs 0032 / 0046 / Phase 2 commercial scope*
3. **Feature management** (entitlements, editions, flags) — *specified; ADR 0009*
4. **Capability declarations** (what hardware/runtime affords) — *specified; W#33 Mission Space + ADRs 0062 + 0063*
5. **Domain configuration** (custom forms, taxonomies, dynamic schemas) — *specified; ADRs 0055 + 0056*
6. **Integration configuration** (payments / messaging / mesh-VPN / providers) — *partial; ADRs 0013 + 0051 + 0052 + 0061 cover provider-neutrality + per-vendor adapters but not unified UX*
7. **Security configuration** (MFA, attestation, audit policies) — *gap (A4-confirmed); ADR 0043 is threat model not tenant-config; needs new ADR or substantial amendment*
8. **Account / identity** (profile, keys, recovery contacts) — *partial; ADR 0046 covers key handling but not user-facing identity-config UX*

## Deliverables

- **Stage 01:** `icm/01_discovery/output/2026-05-01_wayfinder-configuration-ux.md` — 6,500–9,000 words, 7-section structure mirroring W#33 precedent + 2 appendices (cross-platform OS survey + pro-tool/SaaS survey). Verdict table at top (each layer × coverage tag {Specified, Partial, Gap} × confidence × recommended next step). Per-layer §5 with 6-field schema.
- **Stage 04 (synthesis):** 3–5 follow-on intake stubs at `icm/00_intake/output/2026-05-01_<gap-slug>-intake.md`. Recommended split per meta-plan §13.2: Wayfinder + Standing Order bundled (event-sourcing system + event type interdependent), Helm composition, Atlas UI surface, ADR 0048 form-factor amendment for cross-form-factor Atlas rendering.
- **Stage 05 (handoff):** ADR-amendment intakes routed against predecessors {0009, 0048, 0049} per Phase 4 synthesis.

## Next Steps

Proceed to **Stage 01 Discovery**. Author drafts the Wayfinder configuration UX discovery doc per the meta-plan's Phase 3 acceptance criteria. CO review gate at end of Phase 3 (load-bearing review). **Stage 1.5 narrow exception**: WCAG / a11y subagent dispatched once at Phase 3 review on §5 Atlas surface (~1h additional effort) per meta-plan §13.3.

## Cross-references

- UPF plan: `~/.claude/plans/sunfish-wayfinder-configuration-research.md`
- Active workstream ledger: `icm/_state/active-workstreams.md` row W#34
- Project memory (naming + composition): `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_34_wayfinder_naming.md`
- Precedent discovery (W#33 Mission Space): `icm/01_discovery/output/2026-04-30_mission-space-matrix.md`
- W#33 research-methodology playbook: `~/.claude/plans/mission-space-research-methodology.md`
- Pipeline contract: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- Predecessor ADRs: 0007 + 0009 + 0013 + 0028 + 0029 + 0036 + 0041 + 0046 + 0046-a1 + 0048 + 0049 + 0051 + 0052 + 0055 + 0056 + 0057 + 0061 + 0062 + 0063
