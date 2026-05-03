# Mission Space Matrix — Sunfish Dimensional Coverage Discovery

**Stage:** 01 Discovery
**Pipeline:** `sunfish-gap-analysis` (exit: **Approved Gap**)
**Date:** 2026-04-30
**Author:** XO research session
**Status:** Draft — pending CO Phase 3 PASS-gate review (Pedantic-Lawyer hardening pass invoked on §5.10)
**Companion plans:** `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md` (UPF execution plan, Grade A) + `~/.claude/plans/mission-space-research-methodology.md` (research methodology)
**Intake:** `icm/00_intake/output/2026-04-30_mission-space-intake.md`
**Active workstream:** W#33 in `icm/_state/active-workstreams.md`

---

## 1. Executive Summary

Sunfish gates which features a deployment can run along ten dimensions: hardware/environment, identity/user, regulatory jurisdiction, trust/security, Sunfish-architecture-native (CP/AP/zone/sync state/adapter), lifecycle/negotiation, migration, version vector, form factor, and commercial tier. The foundational paper and 19 ADRs cover the architectural-native and partially cover the user-facing dimensions. Three of the ten are **genuine gaps** with no current artifact coverage: the **lifecycle/negotiation** protocol (how a deployment discovers and re-evaluates its capability profile), the **version-vector compatibility contract** for mixed-version clusters, and the **runtime regulatory/jurisdictional** evaluation logic. A fourth, **migration**, is mostly gap with one peripheral hint in ADR 0028-A1.

This document maps each dimension to its current coverage tag (**Specified** / **Partial** / **Gap**), confidence, and recommended next step. It is a *map*, not a *specification* — protocol design, regulatory evaluation, and version-vector contracts each become their own ADR amendments downstream. The synthesis (§6) names 4 recommended follow-on intakes; implementation guidance (§7) routes them.

### Verdict table

| # | Dimension | Coverage | Confidence | Recommended next step |
|---|---|---|---|---|
| 5.1 | Sunfish-architecture-native (CP/AP, zones, sync state, schema epoch, adapter, compat-vendor) | **Specified** | High | Reference only |
| 5.2 | Hardware / environment | **Partial** | Medium-High | New ADR ~0062 — Mission Space Requirements (install-UX layer) |
| 5.3 | Form factor | **Partial** | Medium-High | ADR 0048 amendment A2 — form-factor capability gradient |
| 5.4 | Identity / user | **Partial** | Medium-High | Reference only — ADR 0009 + 0046 + 0032 cover infrastructure; per-bundle policy is downstream module work |
| 5.5 | Trust / security | **Partial** | Medium-High | Reference only — ADR 0043 + 0061 cover the surface |
| 5.6 | Lifecycle / negotiation | **Gap** | Medium | New ADR ~0063 — Mission Space Negotiation Protocol |
| 5.7 | Migration | **Gap** | Medium | ADR 0028 amendment A5 — cross-device + cross-form-factor migration semantics |
| 5.8 | Version vector | **Gap** *(A4 spot-check confirmed)* | Medium | ADR 0028 amendment A6 — version-vector compatibility contract |
| 5.9 | Regulatory / jurisdictional | **Gap** | Medium *(Pedantic-Lawyer pass applied)* | New ADR ~0064 — runtime regulatory/jurisdictional policy evaluation |
| 5.10 | Commercial tier | **Partial** | Medium-High | Reference only — ADR 0009 covers; Phase 2 module work refines |

**Summary statistic:** 1 Specified + 5 Partial + 4 Gap. Of the 4 Gaps, 3 are net-new ADRs and 1 (migration) is an amendment. The 5 Partials all have predecessors covering infrastructure with policy or substantive coverage missing; their next-step recommendations route mostly to existing ADR slots.

---

## 2. Research Question

CO directive (paraphrased from 2026-04-30 brief): *"Examine the dimensional aspects of the Sunfish platform — the value proposition Sunfish provides to end users, the throttling controls and threshold levels for each feature, and how those controls are enabled / reduced / disabled / set to unviable levels along dimensions including hardware, user type, region, regulatory jurisdiction, runtime version, form factor (TVs / watches / IoT / laptops / tablets), and commercial tier. Determine the maximum value Sunfish can provide to customers given the hardware-and-environment context the code is installed on. Manage transitions between environments (e.g., adding GPU/CPU/RAM unlocks features; downgrade locks them). Manage migrations between hardware platforms. Manage versions of Sunfish over time. Provide install-time minimum-spec evaluation so users understand limitations."*

The matrix produced here is the canonical reference answering "for any Sunfish feature, what dimensions gate its availability, and what does the deployment do at each gate?" It does not specify the negotiation protocol, regulatory evaluation logic, or version-vector contract — those become downstream ADRs.

## 3. Method

### 3.1 Verdict-tag scheme

| Tag | Meaning | What's required |
|---|---|---|
| **Specified** | Predecessor artifact (paper §, ADR, or shipped package) covers the dimension substantively, including gating semantics and graceful-degradation rules where applicable. | Citation + one-line gate definition + "reference only" recommendation. No new artifact required. |
| **Partial** | Predecessor exists for the infrastructure layer; policy or specific substantive coverage is missing or scattered. | Citation of what IS covered + named gap + recommended ADR amendment or new ADR. |
| **Gap** | No current artifact covers the dimension as a feature-gating concept. (The artifact may discuss adjacent topics — e.g., paper §6.1 mentions vector clocks for gossip mechanics, but version-vector compatibility for mixed-version clusters is a different concept.) | Industry prior-art (1–3 references) + sketch of contract shape + new ADR or substantial amendment recommendation. |

### 3.2 Per-dimension §5 schema

Every dimension's subsection follows: **Coverage tag** / **Confidence** / **Recommended next step** header, then **Gate definition** / **Examples** / **Current coverage** / **What's missing** / **Recommendation**. Word budget per dimension: ~300w (Specified), ~500w (Partial), ~700w (Gap).

### 3.3 What's in scope

Dimensions along which Sunfish features are gated, the negotiation protocol that determines a deployment's profile, and the migration/transition semantics between profiles. The matrix is dimensional-analysis output; downstream artifacts (the actual contracts and protocols) are the deliverable's *consumers*, not its content.

### 3.4 What's out of scope

- **Concrete protocol specifications** — the Mission Space Negotiation Protocol contract (sequence of probes, manifest format, re-evaluation cadence) is a downstream ADR.
- **Runtime regulatory evaluation logic** — the rule engine and jurisdictional-policy DSL is a downstream ADR.
- **Version-vector compatibility-contract specification** — the type signature + cross-version invariants are a downstream ADR amendment.
- **Pricing-tier policy** — the matrix names commercial tier as a dimension; the actual edition/SKU matrix is product/marketing work outside ICM.
- **Per-feature dimension assignments** — the matrix defines the dimensions; assigning specific features to specific gates is per-block module work (W#22, W#23, W#28, W#31, plus future modules).

### 3.5 Anti-pattern guards (drafting time)

Per the methodology plan (`mission-space-research-methodology.md` §Anti-pattern guard): every claim in §5 cites a paper §, ADR #, or external standard. Industry-prior-art is constrained to 1–3 references per gap. Specified-tier dimensions stay light; Gap-tier dimensions go deep. Pedantic-Lawyer adversarial pass applied to §5.9 (regulatory).

---

## 4. Sunfish Substrate Recap

Mission Space sits *atop* the existing capability-related infrastructure in Sunfish. To avoid re-relitigating substrate decisions, this section recaps the relevant substrate and explicitly distinguishes Mission Space from its near-namesakes.

### 4.1 The five existing `Capability` usages (and why Mission Space is distinct from each)

| Package / location | What "capability" means there | Distinct from Mission Space because… |
|---|---|---|
| `packages/foundation/Capabilities/` | Capability-based authorization closures (`CapabilityClosure`, `CapabilityGraph`, `CapabilityProof`) — proof chains for "who can invoke what action" | This is *security*, not *availability*. Mission Space gates feature *presence*; foundation/Capabilities/ gates *authorization* given presence. |
| `packages/foundation-featuremanagement/` | Feature-flag runtime registry (`IFeatureCatalog`, `DefaultFeatureEvaluator`, `FeatureSpec`) per ADR 0009 | Feature flags toggle a known feature. Mission Space determines whether the feature *can exist* given hardware/jurisdiction/version. Mission Space is upstream of feature flags. |
| `packages/federation-capability-sync/` | RIBLT-based reconciliation of capability-tokens between federated peers | Federation-level token sync; orthogonal to per-deployment capability profiles. |
| `packages/blocks-public-listings/Capabilities/` | Macaroon-based scoped grants for inquiry-defense (ADR 0059) | Per-action scoped grant; not a deployment-level dimension. |
| `packages/blocks-property-leasing-pipeline/Capabilities/` | FHA-defense capability promotion (ADR 0057) | Domain-specific gating; not cross-cutting dimensional. |

### 4.2 Substrate Mission Space composes

- **CP/AP per record class** (paper §2.2, lines 49–55) — classifies records as Available-with-CRDT-merge or Consistent-with-distributed-lease. Records with CP semantics impose stricter dimensional constraints (e.g., a CP record cannot be created from a node that lost lease quorum).
- **Schema epoch** (paper §7, §7.4, lines 211–245) — expand-contract migrations with epoch-coordinated cutover. Old clients on older epoch see a reduced surface.
- **Three Outcome Zones** (paper §20.7, lines 721–741) — Zone A (Local-First Node, the canonical Sunfish architecture), Zone B (Traditional SaaS / Website, explicitly *not* Sunfish's target), Zone C (Hybrid, the Bridge accelerator). Zone is a top-level dimensional discriminator.
- **Sync state** (ADR 0036, lines 42–47) — the five UI-canonical states `healthy / stale / offline / conflict / quarantine`. Mission Space cross-references these as the runtime presentation of negotiation outcomes.
- **Compat-vendor neutrality** (ADR 0013, lines 58–61) — domain modules never reference vendor SDKs directly. Concretely caps the cross-vendor surface available to a deployment.
- **Three-tier peer transport** (ADR 0061, lines 92–97) — `LocalNetwork` / `MeshVpn` / `ManagedRelay`. Each tier has different dimensional preconditions (LAN reachability, mesh-VPN provisioning, Bridge subscription).
- **Foundation.FeatureManagement** (ADR 0009, lines 13–18) — separates *technical flags*, *product features*, *entitlements*, and *editions*. Mission Space assumes this separation; the matrix gates *product features* given dimensional state.

### 4.3 Why Mission Space is its own concept

Mission Space is the *upstream synthesis* of all the above. Existing substrate answers questions like "is this user authorized?" or "is this feature flagged on?" or "is this record CP or AP?" Mission Space answers "given this deployment's hardware, jurisdiction, runtime version, and form factor, *what is the feature surface*?" — a question no current artifact addresses cohesively.

The matrix produced here is the canonical analytical reference for that question. Downstream ADRs operationalize it.

---

## 5. Per-dimension evaluation

### 5.1 — Sunfish-architecture-native (CP/AP, zones, sync state, schema epoch, adapter, compat-vendor)

**Coverage tag:** Specified
**Confidence:** High
**Recommended next step:** Reference only — predecessors cover the surface comprehensively.

#### Gate definition

The set of architectural primitives whose runtime state determines what Sunfish features can run on a given deployment: CP/AP record class, zone (A/B/C), schema epoch, sync state, UI adapter (Blazor/React), and compat-vendor neutrality posture.

#### Examples

- A deployment running in Zone A with no Bridge subscription cannot use Bridge-only features (managed-relay-backed federation, hosted analytics).
- A node on schema epoch N-1 syncing with a node on epoch N can read but not write to N-only fields until upgraded.
- A record marked CP (financial transaction, audit record) cannot be modified during quarantine sync state.

#### Current coverage

- Paper §2.2 (lines 49–55) carries the CP/AP per-record-class table: Documents/notes/tasks → AP; team membership → AP with deferred merge; resource reservations + financial transactions → CP; audit/governance → CP + append-only.
- Paper §20.7 (lines 721–741) defines the three zones explicitly. Zone A = "Local-First Node (this architecture)"; Zone B = "Traditional SaaS or Website"; Zone C = "Hybrid".
- ADR 0028 (lines 64–76) commits to Loro as the CRDT engine "without a sidecar process in steady state."
- ADR 0036 (lines 42–47) carries the canonical 5-state sync table with colors, ARIA roles, and aria-live policies for each.
- ADR 0013 (lines 58–61) carries the provider-neutrality rule: "Domain modules never reference vendor SDKs directly. Domain concepts are Sunfish-modeled, not vendor-mirrored."
- Schema epoch coverage (paper §7, §7.4, lines 217–245) — expand-contract pattern with distributed-lease quorum coordination.

#### Recommendation

No new artifact needed. The Mission Space Matrix references this dimension as the canonical example of "fully specified — copy the pattern" for the partial and gap dimensions below.

---

### 5.2 — Hardware / environment

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** New ADR ~0062 — Mission Space Requirements (install-UX layer).

#### Gate definition

The hardware-and-environment surface a deployment runs on: CPU, GPU, RAM, disk, network class, power posture, sensors, trust hardware, display, OS capability, accessibility primitives. A feature gated on this dimension is unavailable, degraded, or hard-failed if the environment does not meet the gate.

#### Examples

- Background sync daemon requires 1GB RAM available at idle (paper §4); not viable on a 512MB IoT device.
- HSM-backed key storage requires TPM 2.0 / Secure Enclave / StrongBox; degrades to OS keystore on absence.
- Mesh-VPN tier (Tier 2 transport per ADR 0061) requires kernel-level WireGuard support; degrades to Tier 3 (Bridge relay) on absence.
- High-DPI scheduler view degrades to MVP component pair (per ADR 0041 dual-namespace) on low-resolution displays.

#### Current coverage

- Paper §4 (line 99): *"A current mid-range workstation (16GB RAM, 8-core CPU, 500GB NVMe) has more compute than the average cloud VM serving a ten-user team five years ago. A complete three-service containerized stack — API server, sync daemon, local database — runs comfortably under 1GB of RAM at idle."* This grounds the baseline-hardware-envelope claim and establishes that Sunfish's local-first architecture is hardware-tier-aware.
- ADR 0044 (lines 50, 88–89) commits to *"Anchor ships Windows-only for Phase 1"* with a Phase-1 deliverable scope of "Anchor opens, syncs with another Anchor over LAN, syncs with Bridge over WAN, key recovery flow works end-to-end" — implicitly capping Phase 1 to Windows-class hardware.
- ADR 0048 (lines 85–87) commits Phase 2 to *"Native MAUI for Windows (already shipping), macOS, iOS, Android"* + *"MAUI Avalonia backend for Linux"*. Multi-platform support implies hardware-tier diversity.
- ADR 0061 transport tiers implicitly gate on network class (LAN reachability for Tier 1; mesh-VPN provisioning for Tier 2; HTTPS reachability for Tier 3).

#### What's missing

- No canonical **minimum-spec table** keyed by feature (e.g., "feature X requires ≥ Y RAM"). Each feature implementer would need to derive it from paper §4 or measure ad-hoc.
- No **install-time UX** specification for surfacing minimum-spec to users. Steam-style "your device can do X / cannot do Y" page is not specified anywhere.
- No **runtime probe protocol** for re-evaluating hardware capabilities (e.g., upon adding RAM, hot-plugging an HSM, or moving from AC power to battery saver).
- No **graceful-degradation policy** specification — does feature X *hide*, *disable with explanation*, or *hard-fail* when its hardware gate fails? Currently per-feature implementers' choice; no governing rule.

#### Recommendation

New ADR ~0062 — **Mission Space Requirements**. Scope: minimum-spec gradient table (≥ 8 hardware/environment sub-axes); install-time UX specification (modeled on Steam pre-install requirements page); runtime probe protocol (when to re-evaluate, what triggers re-evaluation, what state to cache); graceful-degradation taxonomy (hide / disable-with-explanation / disable-with-upsell / read-only / hard-fail). Effort estimate: medium-large (~12–18h authoring + council review).

---

### 5.3 — Form factor

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** ADR 0048 amendment A2 — form-factor capability gradient.

#### Gate definition

The physical form of the device the deployment runs on: laptop, desktop, tablet, smartphone, watch, TV, IoT/headless. Form factor often correlates with hardware tier but is its own dimension because UX patterns, input modalities, and sensor availability differ even at equivalent hardware levels.

#### Examples

- Anchor MAUI iPad (per ADR 0048-A1) and W#23 iOS Field-Capture app target the same iPad hardware but ship as distinct apps with different feature surfaces — one is the multi-team workspace, the other is offline-only event capture.
- A watch form factor cannot run the full Sunfish kernel; capture-only use cases are the realistic ceiling.
- Headless IoT (no display) requires text/voice/API-only feature surfaces; UI-bearing features hide entirely.

#### Current coverage

- ADR 0048 (lines 85–87) names the Phase-2 platform target list: *"Native MAUI for Windows, macOS, iOS, Android + MAUI Avalonia for Linux."* Form factor implicit in platform target.
- ADR 0048 amendment A1 (lines 146–164) explicitly distinguishes Anchor MAUI iOS from W#23 SwiftUI Field-Capture: *"ADR 0048's 'Native MAUI for ... iOS ...' phrasing applies to Anchor specifically — the multi-team workspace switching app per ADR 0032. The W#23 Field-Capture App is a distinct iOS app."* This recognizes that form-factor diversity inside the same platform-target requires distinct apps.
- Paper §13.4 (lines 428–432) describes multi-device onboarding via QR code: *"The user scans a QR code from an existing team member's device, transferring the role attestation bundle and initial CRDT snapshot."* Multi-device implies cross-form-factor membership.
- ADR 0028-A1 (lines 144–154) specifies that iOS Phase 2.1 ships *"a capture-only append-only event queue. NO CRDT engine on the device."* Form factor (mobile) gates the architectural primitive (CRDT engine on/off).

#### What's missing

- No **form-factor capability gradient table** mapping form factors to expected feature surfaces (e.g., "watch ⇒ glance + voice; tablet ⇒ partial Sunfish kernel + capture; laptop ⇒ full kernel").
- No specification of how features **transition between form factors** when a user adds a device to their team (e.g., "user has laptop + adds watch — what's the watch's reduced surface, derived from where?").
- TV / IoT form factors are not addressed in any current ADR. Phase 2 commercial scope hints at "headless property-management edge devices" but no current artifact covers them.

#### Recommendation

ADR 0048 amendment A2 — **Form-factor capability gradient**. Scope: per-form-factor expected-feature-surface table (laptop/desktop/tablet/watch/TV/IoT/headless); cross-form-factor membership rules (which form factors can join a team; what their reduced surface is); explicit "out of scope for v1" tag for TV / IoT until concrete demand. Effort: medium (~6–10h authoring + council review).

---

### 5.4 — Identity / user

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** Reference only — ADR 0009 + 0046 + 0032 cover the infrastructure; per-bundle policy is downstream module work (W#5 Phase 2 commercial MVP).

#### Gate definition

Properties of the identity attempting to use a feature: provenance (pseudonymous → KYC'd), role within tenant, subscription/edition tier, quota/rate-limit state, device-trust score, consent state (GDPR-style ledger), age/jurisdiction class (COPPA minor, regulated-industry user). Features can be gated on any of these.

#### Examples

- A pseudonymous user cannot access KYC-required features (financial transactions per Phase 2 commercial scope).
- A team-member role can read team data; an admin role can modify membership; per-team subkeys (ADR 0032) cryptographically bound the role.
- A user whose consent ledger lacks "data export" approval cannot trigger an export action.

#### Current coverage

- ADR 0046 (and amendment A1 historical-keys, line 354) covers role-key wrapping: *"Keys are wrapped with each qualifying member's public key (asymmetric encryption). Wrapped key bundles are distributed as administrative events in the log... Each node decrypts its role key bundle using its private key."* This grounds the role-as-cryptographic-claim primitive.
- ADR 0009 (lines 13–18) names the four distinct identity-related concepts: *"Technical flags — runtime booleans... Product features — named capabilities... Entitlements — what a tenant is allowed to use... Editions / tiers — named product configurations (lite, standard, enterprise)."*
- ADR 0032 (lines 110–119) defines per-team subkey derivation: *"the install derives a per-team subkey via HKDF(root_private, 'sunfish-team-subkey-v1:' + team_id). Operators of different teams see different public keys — they cannot correlate the same user across teams."*
- ADR 0049 (audit trail) records role transitions and consent changes.

#### What's missing

- No **canonical role taxonomy** beyond per-tenant primitives. The matrix references existing role primitives but doesn't enumerate a Sunfish-default role hierarchy.
- No **consent-ledger contract** — paper §11 mentions consent in passing; no ADR formalizes the ledger structure or consent-state evaluation.
- No **device-trust scoring** — ADR 0043 (threat model) names the surface but doesn't define a numeric or categorical trust model.

#### Recommendation

No net-new artifact at this layer. The identity infrastructure is fully specified; the gaps (role taxonomy, consent-ledger contract, device-trust scoring) are appropriately downstream of per-bundle module work. Phase 2 commercial-MVP work (W#5) and the property-ops cluster's leasing pipeline (W#22) will surface concrete role/consent requirements that drive future ADRs. Reference this dimension as "infrastructure-Specified, policy-deferred."

---

### 5.5 — Trust / security

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** Reference only — ADR 0043 + 0061 cover the surface.

#### Gate definition

The trust posture of the deployment: device-attestation level (locked vs jailbroken; dev-mode vs production), code-signing state (signed / side-loaded / ad-hoc), network-trust class (home / public / VPN), MFA enrollment, and the Sunfish-specific chain-of-permissiveness exposed by the deployment's surface.

#### Examples

- A jailbroken iOS device cannot use HSM-backed key storage; degrades to software keystore with explicit warning.
- A code-signed Anchor build attests its binary; an ad-hoc build cannot federate with attested-only Bridge nodes.
- A user on public Wi-Fi without VPN cannot use Tier 1 (LAN-only) transport; Mission Space negotiates Tier 2 or 3.

#### Current coverage

- ADR 0043 (lines 94–96, 43–49) frames the unified threat model: *"The merge-path surface composes as follows: [Subagent] → [CI runs minimal checks] → [Merge happens] OR [bypass merge]... Defense-in-depth requires a system-level threat model, not per-ADR threat models."*
- ADR 0061 (lines 92–97) defines the three transport tiers with explicit trust postures: `LocalNetwork` (mDNS, same-LAN) / `MeshVpn` (WireGuard cross-network, attested peers) / `ManagedRelay` (Bridge HTTPS relay, ciphertext-only).
- ADR 0046 + amendments cover key-rotation and historical-keys projection — trust survives operator-key rotation.
- Paper §11.3 covers role attestation as cryptographically-signed claims.

#### What's missing

- No **device-attestation policy** — when does Sunfish *require* attested-device posture? Currently per-tenant config, not a Mission Space dimension.
- No **code-signing enforcement** — attested-only Bridge nodes are described in ADR 0031 but the policy is not generalized as a Mission Space gate.
- No **network-trust class probe** — the matrix treats this as a runtime-determined sub-axis but no current artifact defines the probe.

#### Recommendation

No net-new artifact at this layer. ADR 0043 + 0061 are the canonical references; gaps surface naturally as Phase 2 commercial work or W#23 iOS field-capture surface concrete attestation requirements. Reference this dimension as "infrastructure-Specified, policy-emerges-from-modules."

---

### 5.6 — Lifecycle / negotiation

**Coverage tag:** Gap
**Confidence:** Medium
**Recommended next step:** New ADR ~0063 — **Mission Space Negotiation Protocol**.

#### Gate definition

The protocol by which a deployment discovers its current capability profile, communicates it to the user, re-evaluates it when conditions change, and degrades gracefully when a previously-available feature becomes unavailable. This dimension is the *connective tissue* of all the other dimensions — it specifies *how* the matrix is evaluated at runtime.

#### Examples

- At install: deployment probes hardware (RAM/CPU/disk/sensors), runtime version, network reachability, and computes its Mission Envelope.
- At startup: deployment re-checks since hardware/network may have changed since last run.
- Continuously: deployment monitors for capability-changing events (battery → AC, mesh-VPN provisioned, hardware added) and re-negotiates.
- Graceful degradation: when a feature's gate fails, the deployment hides / disables-with-explanation / hard-fails per a governing taxonomy.
- User communication: when capability changes ("you just gained X" / "you just lost Y" / "data created under capability Z is now read-only"), the user is informed via a non-intrusive channel.

#### Current coverage

- Paper §13.2 (lines 409–420) carries the AP/CP visibility table with staleness thresholds and UX treatments for *some* dimensions: *"Resource availability | 5 minutes | Amber indicator; booking blocked if offline. Financial balances | 15 minutes | 'As of [timestamp]' label; writes require online."* This is partial — it covers data-staleness as a runtime indicator but not capability-presence negotiation.
- ADR 0041 (lines 11–22, 38–42) frames the dual-namespace component pattern (rich vs MVP) — implicitly a graceful-degradation primitive at the UI layer: *"Each pair shares the type name Sunfish* but lives in two distinct namespaces."*
- No artifact covers the negotiation *protocol* — when probing happens, what's probed, what state is cached, what triggers re-evaluation, how user is notified.

#### What's missing (genuine gap)

The entire negotiation contract:
- **Probe mechanics** — install-time vs startup-time vs continuous; what's probed at each cadence.
- **Manifest format** — how a deployment serializes its current Mission Envelope (for diagnostics, logging, telemetry, user-visible "what your device can do" UI).
- **Re-evaluation triggers** — hot-plug events, version upgrades, network-topology changes, jurisdiction crossings (mobile devices), commercial-tier changes (Bridge subscription start/end).
- **Cache vs live-probe** — what gets cached, for how long, what invalidates.
- **Graceful-degradation taxonomy** — formalize hide / disable-with-explanation / disable-with-upsell / read-only / hard-fail per feature-gate failure mode.
- **User-communication policy** — when to surface capability changes to user, in what channel (status bar per paper §13.2, modal, toast, deferred to next session).
- **Per-feature force-enable** — power-user override surface for "force-enable an unsupported feature with warning."
- **Telemetry shape** — capability-cohort analytics for product roadmap (how many users have feature X available; how often does feature Y get hidden).

#### Industry prior-art (for downstream ADR drafting reference)

- **SIP / SDP capability negotiation** (RFC 5939) — peer-to-peer media-session negotiation. Pattern: each peer offers its capability set; intersection determines session.
- **TLS cipher-suite negotiation** — client offers ciphers; server selects. Pattern: ordered preference list.
- **OpenGL / Vulkan extension query + feature levels** — runtime-probed capability tiers (DirectX FL_9_1 / 10_0 / 11_0 / 12_0 are the closest engineering analog to Mission Space tiers).
- **HTTP content negotiation** (Accept-* headers) — client signals preferences; server selects representation.
- **WebRTC codec negotiation** — multi-dimensional matrix (codec × profile × level) negotiated per-session.

#### Recommendation

New ADR ~0063 — **Mission Space Negotiation Protocol**. Scope: probe mechanics; manifest format; re-evaluation triggers; graceful-degradation taxonomy; user-communication policy; force-enable override; telemetry shape. References DirectX Feature Levels and SIP/SDP capability negotiation as engineering precedents. Effort: large (~16–24h authoring + council review). This is the most load-bearing of the recommended new ADRs because every other capability gate ultimately surfaces through the negotiation protocol's UX channels.

---

### 5.7 — Migration

**Coverage tag:** Gap *(one peripheral hint exists)*
**Confidence:** Medium
**Recommended next step:** ADR 0028 amendment A5 — cross-device + cross-form-factor migration semantics.

#### Gate definition

The semantics of moving a Sunfish deployment between devices, between form factors, or between hardware tiers — including snapshot portability, encrypted-state key transfer, distinguishing data-loss from feature-loss, forward-compat behavior when older receives newer data, and rollback semantics when hardware is downgraded after data was created in the upgraded mode.

#### Examples

- User migrates from a laptop (full Sunfish kernel) to a watch (capture-only) — the watch sees a reduced surface, but the user's data on the laptop is unchanged.
- User upgrades a laptop from 16GB RAM to 64GB — features previously unavailable (large CRDT histories, in-memory analytics) become available; existing data is unchanged.
- User downgrades from a desktop to a tablet — features that depended on desktop hardware become *deactivated*; the data they generated remains read-only.
- User migrates between Sunfish versions across schema epoch transitions — paper §7.4 governs the data layer, but the *feature surface* shift is unspecified.

#### Current coverage

- ADR 0028-A1 (lines 144–154) is the one peripheral hint: iOS Phase 2.1 ships *"a capture-only append-only event queue. NO CRDT engine on the device."* Implicitly migrates events from the iOS form factor to the Anchor merge boundary. *"Conflicts are resolved at the Anchor merge boundary using existing Anchor CRDT primitives."* This grounds cross-form-factor capture-then-merge but does not generalize.
- Paper §15.2 (lines 483–490) covers mixed-version testing scenarios: *"Node on schema N-1 syncs with node on schema N → lenses translate operations correctly. Epoch transition while one node is offline → returning node downloads epoch snapshot and resumes correctly. 'Couch device' (offline for 3+ major versions) → capability negotiation rejects with clear error."* This implicitly references capability negotiation (the gap dimension §5.6) without specifying it.
- Paper §7.4 epoch-coordinated schema migration covers the data-layer migration; the feature-surface migration is not addressed.

#### What's missing (genuine gap)

- **Cross-form-factor migration table** — when a user adds form factor F to their team, what's F's expected feature surface, and what data does F sync vs not? (Beyond ADR 0028-A1's iOS-specific case.)
- **Cross-hardware-tier migration semantics** — when hardware is upgraded or downgraded, what's the rule for re-evaluating each feature gate? Does feature presence persist (cached) until re-probed, or is each gate re-evaluated immediately?
- **Data-loss-vs-feature-loss distinction** — explicit invariant: feature deactivation never causes data loss; data created under a feature that's now unavailable is preserved read-only.
- **Forward-compat policy** — when an older deployment receives data from a newer one (newer schema epoch, newer Mission Envelope), what does it show?
- **Rollback semantics** — if a user creates data under capability Z, then downgrades to a hardware tier where Z is unavailable, the data is read-only-but-not-lost. Specify.
- **Encrypted-state key transfer** — ADR 0046 covers key rotation; cross-device transfer is implicit in QR-onboarding (paper §13.4) but not formalized as a Mission-Space-aware migration.

#### Industry prior-art

- **NASA flight-envelope expansion** — the test-engineering process of incrementally pushing the boundary outward as hardware capability grows. Direct analog to upgrade-side migration.
- **Database migration patterns** (Liquibase / Flyway / Avro evolution) — schema-version-vector with backward/forward compatibility windows. Closest analog to Sunfish schema epochs but doesn't address feature-surface migration.
- **iOS device-to-device data restoration** — Apple's encrypted-iCloud-backup migration pattern. Form-factor-aware: restoring iPhone data to iPad applies a derived-surface filter.

#### Recommendation

ADR 0028 amendment A5 — **Cross-device + cross-form-factor migration semantics**. Scope: cross-form-factor migration table; cross-hardware-tier re-evaluation rules; data-loss-vs-feature-loss invariant; forward-compat policy; rollback semantics; encrypted-state key transfer formalization. Built on top of ADR 0028's CRDT engine + ADR 0046's key handling. Effort: medium-large (~10–14h authoring + council review).

---

### 5.8 — Version vector

**Coverage tag:** Gap *(A4 spot-check confirmed)*
**Confidence:** Medium
**Recommended next step:** ADR 0028 amendment A6 — **Version-vector compatibility contract**.

#### Gate definition

The compatibility relationship between Sunfish kernel version × plugin version × adapter version × schema epoch × stable-vs-beta channel × self-host-vs-managed-Bridge instance, expressed as an explicit contract that mixed-version cluster members can verify before federating.

#### Examples

- A v1.3 kernel can sync with a v1.2 kernel as long as schema epochs match and no v1.3-only required features are demanded by the cluster's CP records.
- A stable-channel deployment cannot sync with a beta-channel deployment if the beta is past a schema-epoch cutover the stable hasn't crossed.
- A self-hosted Bridge can present a different version surface than a managed Bridge (Sunfish-operated relay); cross-instance interop is bounded by their version-vector intersection.

#### Current coverage

- Paper §6.1 (line 180) mentions vector clocks operationally: *"Each node maintains a membership list of known peers with associated vector clocks. Periodically (default: 30 seconds), each node selects two random peers and exchanges a delta — the operations each node holds that the other lacks."* This is **vector clocks for gossip mechanics** (anti-entropy reconciliation), **not** version-vector compatibility for mixed-version clusters. The A4 spot-check confirmed this distinction explicitly.
- ADR 0028 + amendments (A1/A2/A3/A4) cover CRDT engine selection and mobile reality but **do not specify** version-vector compatibility. The A4 spot-check confirmed: "ADR 0028 is silent on cross-version cluster behavior. The CRDT engine selection is orthogonal to version-vector coordination."

#### What's missing (genuine gap)

- **Version-vector type signature** — what tuple expresses "this kernel × plugin × adapter × schema-epoch × channel × instance-class." JSON shape, normalization rules, comparison semantics.
- **Compatibility relation** — given two version vectors V1, V2, when can a node carrying V1 federate with a node carrying V2? What's the canonical answer (subset / equality / explicit-pairwise-allowlist / range)?
- **Capability negotiation handshake at federation time** — when peer A meets peer B, what version-vector exchange happens? Is it a separate handshake or part of the existing gossip protocol?
- **Behavior on incompatibility** — when V1 and V2 are incompatible, does the connection fail, degrade to a reduced surface, or quarantine?
- **Long-offline reconnect** — paper §15.2's "couch device" scenario ("offline for 3+ major versions") references "capability negotiation rejects with clear error" but the rejection logic is the gap.

#### Industry prior-art

- **Lamport vector clocks** — original Lamport / Mattern formalism for partial-ordering distributed events. The mathematical foundation, but not directly the compatibility-contract concept.
- **Paxos epoch numbers** — strict-version-monotonic across cluster; nodes reject older-epoch proposals. Closest formal analog to Sunfish's epoch-coordinated cutover.
- **gRPC API versioning** (Google's API design guide) — semantic versioning + explicit deprecation windows + compatibility classes. Pragmatic engineering analog.
- **HTTP/2 ALPN negotiation** — clients and servers exchange supported-protocol-version lists during connection setup. Closest negotiation-protocol analog.

#### Recommendation

ADR 0028 amendment A6 — **Version-vector compatibility contract**. Scope: version-vector type signature; compatibility relation (recommend: explicit allowlist with range syntax, modeled on gRPC versioning); federation-time handshake; incompatibility behavior; long-offline reconnect rejection logic. Cross-references the Mission Space Negotiation Protocol (~ADR 0063). Effort: medium-large (~12–18h authoring + council review).

---

### 5.9 — Regulatory / jurisdictional

**Coverage tag:** Gap
**Confidence:** Medium *(Pedantic-Lawyer hardening pass applied to this section)*
**Recommended next step:** New ADR ~0064 — **Runtime regulatory/jurisdictional policy evaluation**.

> **Reader caution:** This section identifies regulatory dimensions and cites primary law where the predecessor artifacts (ADRs 0057, 0060) cite primary law. Claims about whether a given Sunfish deployment is in compliance with a given regulation are *not* made here; they require general counsel review. Any downstream ADR built on this section must engage qualified legal counsel before specifying runtime evaluation behavior.
>
> **Citation caution (Pedantic-Lawyer hardening pass output, 2026-04-30):** specific statutory citations in this section have not been verified against the current Official Code text and may use practitioner shorthand; downstream ADR drafts MUST re-verify each citation against primary sources before specifying enforcement behavior. The Phase 3 review applied targeted edits to align the most-frequently-cited authorities, but per-claim verification remains the downstream ADR's responsibility.

#### Gate definition

Regulatory and jurisdictional dimensions that gate features as a matter of *legality*, not technical feasibility. Includes data residency, export-control crypto strength caps, industry-specific compliance regimes (HIPAA, FERPA, PCI-DSS, SOC 2, FHA), sanctions regimes, and tier-classified regulations like the EU AI Act.

#### Examples

- Personal data of EU data subjects is subject to GDPR; cross-border transfers of that data to third countries are governed by GDPR Chapter V (Arts. 44–50).
- A Sunfish deployment used by a HIPAA Covered Entity or Business Associate must support the Security Rule safeguards (45 CFR §164.308 administrative, §164.310 physical, §164.312 technical) for any feature processing protected health information.
- An entry-management feature must conform to per-jurisdiction notice-and-purpose rules (per ADR 0060: California Civil Code §1954 enumerates permitted entry purposes and presumes 24-hour advance notice (§1954(d)(1)); entries during "normal business hours" (§1954(c)) — exact hours not statutorily fixed. Counsel review required for any runtime enforcement specification.).
- A leasing-pipeline feature must apply criteria uniformly per Fair Housing Act (per ADR 0057).

#### Current coverage

- ADR 0057 (lines 29–40) carries the FHA-compliance framing: *"The structural defense is documentation: the exact criteria document version sent to each prospect on what date; uniform application of those criteria; audit trail showing each application was evaluated against the criteria as documented... No selective application is structurally possible."* This is **substrate** (audit + uniformity), not runtime jurisdictional evaluation.
- ADR 0060 (lines 22–27) carries the Right-of-Entry per-jurisdiction rule citations: *"California: 24h written notice; 8a–5p only; specific permitted purposes (CCP §1954). New York City: 'reasonable advance notice' (no fixed hours); narrower permitted purposes. Utah: 24h notice for non-emergency; no time-of-day restriction. Federal HUD subsidized housing: layered on top of state rules; additional disclosure requirements."* This is **domain-specific** (entry compliance for property management), not cross-cutting.
- Paper §16 covers IT governance (MDM-compatible installation, BYOD separation) but does **not** explicitly enumerate GDPR/HIPAA/FedRAMP requirements.
- Paper §20.4 (line 688) names regulatory factors as a deployment-architecture *filter*: *"Regulated data residency requirements (GDPR, HIPAA, FedRAMP, ITAR) | Local-first or on-premises."* This filters the architectural choice (Zone A vs B vs C) but does not gate features at runtime.

#### What's missing (genuine gap)

- **Runtime jurisdictional probe** — how does a deployment determine its current jurisdiction at runtime? IP-geolocation (unreliable), explicit user declaration, tenant-config (most reliable but stale on travel)?
- **Per-jurisdiction policy evaluation** — given runtime jurisdiction = J and feature = F, is F available? What's the rule engine, and what is its consistency with the FHA documentation-defense pattern (ADR 0057) and the per-jurisdiction explicit citation pattern (ADR 0060)?
- **Cross-cutting regulatory regimes** — HIPAA / FERPA / PCI-DSS / SOC 2 / EU AI Act / GDPR. Which regimes does Sunfish even acknowledge? *(ADR 0043 names public-OSS as the threat-model posture; regulated-industry posture is not specified.)*
- **Data-residency enforcement** — when a record's residency requirement conflicts with the deployment's current location, what's the runtime behavior? Read-only, hide, refuse-to-sync, hard-fail?
- **Sanctions** — features unavailable to specific jurisdictions per OFAC / EU sanctions lists. Currently unaddressed. (OFAC SDN/sectoral lists and EU consolidated sanctions list exist; applicability to a P2P open-source reference implementation is fact-specific — export-control nexus, US-person test, EAR §744 — flag for counsel.)
- **EU AI Act tier classification** — the regulation classifies AI systems into risk tiers; Sunfish features that incorporate AI/ML (none yet, but future) would need tier classification.

#### Industry prior-art

- **GDPR Articles 22 (automated individual decision-making, including profiling), 44 (general principle for transfers), 45 (transfers via adequacy decision), 46 (transfers subject to appropriate safeguards such as SCCs/BCRs)** — primary law source for EU data-protection runtime gates.
- **HIPAA Privacy Rule (45 CFR §§164.500–164.534)** — primary US health-information regulation. The Privacy Rule (Subpart E) is paired with the Security Rule (Subpart C: 45 CFR §§164.302–164.318) which defines the administrative / physical / technical safeguards triad.
- **PCI-DSS v4.0** (PCI Security Standards Council) — Payment Card Industry Data Security Standard. Note: merchant-level tiers (1–4) are defined by individual card-brand programs (Visa, Mastercard, etc.), not by PCI-DSS itself.
- **EU AI Act** (Regulation EU 2024/1689) — risk-tier classification (prohibited / high-risk / limited-risk / minimal-risk per Arts. 5–6 + Annex III) drives different runtime obligations.

#### Recommendation

New ADR ~0064 — **Runtime regulatory/jurisdictional policy evaluation**. Scope: runtime jurisdictional probe; per-jurisdiction policy evaluation rule engine; cross-cutting regulatory regime acknowledgment (which regimes Sunfish targets and which it doesn't); data-residency enforcement behavior; sanctions handling; EU AI Act tier-classification placeholder for future AI-feature gating. Cross-references ADRs 0057 (FHA) and 0060 (Right-of-Entry) as concrete domain-specific precedents. Effort: large (~18–24h authoring + extended council review including legal-perspective subagent). **Pedantic-Lawyer hardening pass executed during Phase 3 review** ensures every regulatory claim in this section is grounded; the downstream ADR must engage general counsel before specifying enforcement behavior.

---

### 5.10 — Commercial tier

**Coverage tag:** Partial
**Confidence:** Medium-High
**Recommended next step:** Reference only — ADR 0009 covers; Phase 2 module work refines.

#### Gate definition

Commercial gating *independent of hardware capability*: which features are open-source vs commercial vs trial/preview vs tier-locked (lite / standard / enterprise edition). A feature that hardware can run may still be unavailable because the deployment is on the wrong tier.

#### Examples

- A managed-relay subscription (paper §17.2) gates Bridge-hosted features; self-hosted deployments cannot use them regardless of hardware.
- An enterprise-tier audit-export feature is hardware-feasible on standard-tier deployments but commercially gated.
- A trial-mode preview limits feature use to a time window; expiry causes feature deactivation.

#### Current coverage

- ADR 0009 (lines 13–18) names the four-concept separation: technical flags / product features / entitlements / **editions** ("named product configurations (lite, standard, enterprise)"). This grounds the dimension.
- ADR 0009 (lines 71–72) names the bundle-manifest authoring source: *"Bundle manifest (ADR 0007) becomes the authoring source for entitlements — featureDefaults and editionMappings map directly onto FeatureSpec.DefaultValue and IEntitlementResolver rules."*
- ADR 0041 (lines 40–42) frames the dual-namespace pattern: *"the rich variants were authored to satisfy kitchen-sink's Telerik-verbose demo standard while the MVP variants remained as the canonical small-surface contract under the framework-agnostic taxonomy. Both are intentional; both serve different roles."* The rich-vs-MVP split is implicit commercial-vs-OSS gradient.
- Paper §17.2 (lines 531–542) frames the managed-relay sustainability model: *"a single relay node on commodity infrastructure handles approximately 500 concurrent team connections at minimal hosting cost. At a modest per-team subscription fee, the service becomes cash-flow positive."* This is the canonical Sunfish commercial-tier example.

#### What's missing

- No **edition matrix** mapping editions (lite/standard/enterprise) to their feature surfaces.
- No **trial-mode lifecycle** specification — start/expiry/conversion/post-expiry-state.
- No **cross-tier migration** rules — what happens to data created under tier T1 when the deployment downgrades to T2?

#### Recommendation

No net-new artifact at this layer. ADR 0009 covers the infrastructure; the edition matrix is product/marketing work outside ICM scope; the trial-mode lifecycle is a future product-management deliverable. Reference this dimension as "infrastructure-Specified, policy-deferred-to-product."

---

## 6. Synthesis — recommended follow-on intakes

The matrix surfaces 4 follow-on intakes, ordered by predecessor cleanliness:

### 6.1 — New ADR ~0062: Mission Space Requirements (install-UX layer)

**Predecessor:** none clean. Adjacent: paper §4 (hardware baseline), ADR 0044 (Phase 1 platform scope), ADR 0048 (Phase 2 platform scope), paper §13.2 (UX staleness thresholds).

**Why net-new:** the install-time minimum-spec UX is not addressed in any current ADR. This is a load-bearing UX deliverable for Phase 2 commercial onboarding (W#5) and Mission Space negotiation (~ADR 0063).

**Effort:** medium-large (~12–18h).

**Priority:** highest of the new ADRs because it's user-visible at first contact (install time).

### 6.2 — New ADR ~0063: Mission Space Negotiation Protocol (runtime layer)

**Predecessor:** none clean. Adjacent: paper §13.2 (AP/CP visibility), ADR 0036 (sync states), ADR 0041 (rich-vs-MVP UI degradation primitive).

**Why net-new:** the negotiation protocol is the connective tissue of all dimensional gates. Without it, every per-feature implementation re-derives "how do I check if this is available, when do I re-check, how do I tell the user if it changes" from scratch.

**Effort:** large (~16–24h).

**Priority:** highest of the new ADRs *for engineering* (every other dimension surfaces through this protocol's UX channels).

### 6.3 — New ADR ~0064: Runtime regulatory/jurisdictional policy evaluation

**Predecessor:** none clean. Adjacent: ADR 0057 (FHA documentation-defense), ADR 0060 (Right-of-Entry per-jurisdiction rules), paper §20.4 (regulatory factors as architectural filter).

**Why net-new:** runtime regulatory evaluation is unaddressed cross-cuttingly. Per-domain ADRs (0057 FHA, 0060 entry) handle their slices but don't generalize.

**Effort:** large (~18–24h + extended legal-perspective council review).

**Priority:** highest commercial priority because regulatory non-compliance is a product launch-blocker for any non-US-residential-property tenant.

### 6.4 — ADR 0028 amendment A6: Version-vector compatibility contract

**Predecessor:** ADR 0028 (CRDT engine selection) + amendments A1–A4. Clean amendment slot.

**Why amendment (not new ADR):** version-vector compatibility is intrinsically tied to the CRDT engine + cluster-membership protocol that ADR 0028 already governs. New ADR would over-fragment the contract surface.

**Effort:** medium-large (~12–18h).

**Priority:** mid — load-bearing for mixed-version cluster operability but not blocking near-term workstreams.

### 6.5 — Out-of-scope items (track-as-deferred)

- Detailed edition-matrix policy work (commercial tier, §5.10) — product/marketing
- Per-feature consent-ledger contract (identity/user, §5.4) — emerges from Phase 2 commercial work
- Device-trust scoring model (trust/security, §5.5) — emerges from W#23 iOS work
- TV / IoT form-factor specifications (§5.3) — deferred until concrete demand

---

## 7. Implementation Guidance

### 7.1 Routing recommendation

| Follow-on | Routing | Rationale |
|---|---|---|
| Mission Space Requirements | New ADR ~0062 | No clean predecessor; install-UX is its own concern |
| Mission Space Negotiation Protocol | New ADR ~0063 | Cross-cutting connective-tissue contract; doesn't fit any current ADR |
| Runtime regulatory evaluation | New ADR ~0064 | Cross-cutting; ADRs 0057 and 0060 are domain-specific |
| Version-vector compatibility | ADR 0028 amendment A6 | Tied to CRDT engine + cluster membership |
| Migration semantics | ADR 0028 amendment A5 | Adjacent to A6; same predecessor |

### 7.2 Sequencing recommendation

Phase 4 will produce intake stubs for the 4 follow-ons (counting A5 + A6 separately from the 3 new ADRs). Recommended authoring sequence:

1. **A6 first** (version-vector contract). Smallest scope; resolves the most concrete A4 spot-check finding; unblocks long-offline-reconnect logic referenced in paper §15.2.
2. **A5 second** (migration semantics). Builds on A6's compatibility relation.
3. **~ADR 0063 third** (Mission Space Negotiation Protocol). Largest scope; depends on A5 + A6 for cross-version migration handling within negotiation.
4. **~ADR 0062 fourth** (Mission Space Requirements / install UX). Depends on ~ADR 0063 to know what the install-time probe negotiates.
5. **~ADR 0064 last** (regulatory evaluation). Independent of the others architecturally but needs general-counsel engagement, which is async; can run in parallel from any point.

### 7.3 Cross-workstream impact

- **W#22 (Leasing Pipeline)** — consumes ~ADR 0064 (regulatory) + ADR 0009 commercial-tier framing. Phase 6 compliance half (currently deferred per row 22 of active-workstreams.md) flows through ~ADR 0064 work.
- **W#23 (iOS Field-Capture)** — consumes ADR 0048-A2 (form-factor gradient, §5.3 recommendation), ADR 0028-A5 (migration), ~ADR 0063 (negotiation). The W#23 substrate hand-off can proceed with current ADRs; downstream capture-flow hand-offs benefit from this matrix.
- **W#28 (Public Listings)** — consumes ~ADR 0062 (install UX) for tier-aware rendering and ADR 0009 commercial-tier surface.
- **W#31 (Foundation.Taxonomy)** — consumes ~ADR 0064 (regulatory) for jurisdiction-aware classification.

### 7.4 Council review posture

Per the cohort discipline established in `feedback_decision_discipline.md`: **pre-merge council review canonical** for all 4 follow-on ADR drafts. The cohort metric (7-of-7 substrate amendments needed council fixes; A4 pre-merge council saved zero-day W#32 build pause) applies fully here. Do not skip council on any of the 4.

For ~ADR 0064 (regulatory) specifically: in addition to the standard adversarial council, dispatch a "Pedantic Lawyer" perspective subagent to verify every regulatory claim cites primary law. The Phase-3 hardening pass on §5.9 of this matrix is a precedent for that subagent dispatch.

### 7.5 Pipeline closure

Per the gap-analysis pipeline contract (`icm/pipelines/sunfish-gap-analysis/routing.md`), this discovery doc is sufficient closure under the **"Approved Gap"** exit pattern. No Stage-02 architecture pass is required *for the matrix itself*. Each follow-on intake in §6 will run its own ICM pipeline (likely `sunfish-feature-change` for the new ADRs and `sunfish-api-change` for the amendments).

Pipeline closes when CO records a final "Approved Gap" decision in this doc's frontmatter Status field, after Phase 4 (synthesis intake stubs) and Phase 5 (handoff + active-workstreams.md ledger flip from `building` → `built`).

---

## Cross-references

- Plan: `~/.claude/plans/this-looks-pretty-comprehensive-concurrent-floyd.md`
- Methodology: `~/.claude/plans/mission-space-research-methodology.md`
- Intake: `icm/00_intake/output/2026-04-30_mission-space-intake.md`
- Active workstream: `icm/_state/active-workstreams.md` row W#33
- Project memory: `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_workstream_33_mission_space_matrix.md`
- Precedent: `icm/01_discovery/output/2026-04-30_microsoft-fabric-capability-evaluation.md`
- Pipeline: `icm/pipelines/sunfish-gap-analysis/{README,routing,deliverables}.md`
- Foundational paper: `_shared/product/local-node-architecture-paper.md`
- UPF framework: `.claude/rules/universal-planning.md`
