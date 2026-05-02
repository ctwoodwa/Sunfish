# Sunfish Architecture Snapshot — 2026-Q2

**Snapshot date:** 2026-05-02
**Source journal:** 61 ADRs in `docs/adrs/`; ledger at `icm/_state/active-workstreams.md`
**Foundational paper:** `_shared/product/local-node-architecture-paper.md` v10.0 (April 2026) — *Inverting the SaaS Paradigm*
**Authority disclaimer:** This document is a derived read-model from the ADR journal. The ADR journal is authoritative; this snapshot synthesizes the journal as of the snapshot date and may drift between snapshots. Structural changes between snapshots are visible by diffing two snapshot files. When this document conflicts with any ADR, the ADR wins.

---

## Executive summary

Sunfish is a framework-agnostic suite of building blocks — open-source core plus commercial accelerators — for scaffolding, prototyping, and shipping real applications with interchangeable UI and domain components. Its reference deployment model is the *Inverted Stack*: data and business logic live in a locally-owned node (the Anchor desktop app) or in a tenant-operated self-hosted server; a relay-capable managed bridge (Bridge) provides SaaS convenience without operator custody of plaintext. This posture is the animating idea behind every architectural decision in the journal.

As of the 2026-Q2 snapshot, the ADR journal contains 61 records: 55 Accepted, 5 Proposed, and 1 Superseded. The journal was seeded in mid-April 2026 and has grown at approximately 4–5 ADRs per day during the Business MVP Phase 1 and Phase 2 build periods. The highest-amendment ADR (0028 — CRDT Engine Selection) has accumulated 10 amendments, reflecting its role as the keystone of sync semantics. The GRAPH.md composition graph is currently sparse (two explicit dependency edges), which indicates that ADR authors have relied on narrative citations rather than structured frontmatter links; this is an identified gap in the journal's machine-readability.

The architecture is in a mature design / early implementation phase. Substrate packages across foundation, kernel, and distribution tiers are largely specified and partially built. The property-operations vertical cluster (14 workstreams) is partially built, with the most structurally important substrates (signatures, leasing, taxonomy, field-encryption, transport, versioning, migration) now shipped or nearly shipped. What remains is the consumer-wiring tier — connecting substrates to domain blocks — and the three forward-pointing design concerns: Wayfinder configuration (ADRs 0065 + pending 0066–0068), Mission Space capability negotiation (ADRs 0062–0064), and the multi-tenancy type-surface convention (design-in-flight, blocking a Tier 2 audit retrofit).

---

## 1. Architectural overview by tier

### Foundation (22 ADRs)

Foundation is the largest tier and forms the dependency floor of all other tiers. Its mandate is contracts-first, framework-agnostic primitives that every other package composes. No foundation package references a UI framework, an adapter, or a blocks-level domain model.

The core foundation surface was established in ADRs 0001–0015, which resolve the four Appendix C questions from the platform specification plus eight structural questions surfaced during the 2026-04-18 gap analysis. Key settled decisions: the schema registry uses a two-tier governance model (repo-local default, `sunfish.io/schemas/*` for cross-deployment interop) per ADR 0001; modules ship as NuGet packages per ADR 0002; multi-tenancy primitives live in `Sunfish.Foundation.MultiTenancy` with Finbuckle as the Bridge-side implementation boundary per ADR 0008; feature management uses a three-concept model (flags / entitlements / editions) with a single resolution path per ADR 0009; bundles follow strict semver with tenant-experience-differentiated upgrade paths per ADR 0011; local-first contracts are in `Sunfish.Foundation.LocalFirst` isolated from the federation packages per ADR 0012; integrations with external providers are contracts-only via `Sunfish.Foundation.Integrations` with the provider-neutrality enforcement gate (Roslyn analyzer SUNFISH_PROVNEUT_001 + RS0030) mechanically active per ADR 0013.

The later foundation ADRs (0035–0065) reflect the Phase 2 substrate wave. Notable additions: `Foundation.Recovery` for field-level AES-GCM encryption (ADR 0046); `Foundation.Integrations.Payments` for PCI SAQ-A-scoped payment flows (ADR 0051); bidirectional messaging substrate for durable email + SMS (ADR 0052); the four-layer type-customization model's dynamic-forms extension via `Foundation.DynamicForms` (ADR 0055); `Foundation.Taxonomy` for versioned product models with lineage (ADR 0056); the three-tier peer transport model (ADR 0061); and the Mission Space family (ADRs 0062–0064) covering runtime capability negotiation, install-UX requirements, and jurisdictional policy evaluation.

Open questions in the foundation tier: the `Foundation.MissionSpace` package's consumer wiring (ADR 0062 + 0063 + 0064 are in Proposed status); the DEK rotation primitive deferred from ADR 0046-A4.3 (fixed `KeyVersion=1` for Phase 1; durable rotation storage is a future amendment); the `Foundation.Wayfinder` package (ADR 0065, Proposed, consumer wiring pending).

### Kernel (6 ADRs)

The kernel tier is small by ADR count (6 ADRs) but covers the two most architecturally consequential decisions: CRDT engine selection and the runtime split.

ADR 0027 resolved the name collision between the existing type-forwarding facade (`packages/kernel/`) and the paper's runtime kernel concept. The decision: keep the facade as-is (closing gap G1 without churn), and establish `packages/kernel-runtime/` as the separate home for paper §5.1's runtime responsibilities (node lifecycle, plugin registry, sync-daemon orchestration). This split is structural; `kernel-runtime` depends on `kernel`; the facade continues to re-expose Foundation primitives under the `Sunfish.Kernel.*` namespace.

ADR 0028 (CRDT Engine Selection) is the most-amended ADR in the journal (A1–A10) and deserves explicit treatment. The base decision selected Loro (Rust-native CRDT with .NET FFI) as the primary engine on the basis of compaction quality, rich-text support, compact binary encoding, and active maintenance. The amendment chain extended this decision to cover: mobile-platform constraints for iOS (A1); post-acceptance council findings including mobile-form-factor migration (A5), version-vector compatibility contracts (A6), and schema-epoch versioning (A7, A8). The substrates specified by A5/A6/A7/A8 were built in workstreams #34 and #35 (`Foundation.Versioning` and `Foundation.Migration`), both shipped in early May 2026.

ADR 0049 established `packages/kernel-audit/` as a distinct kernel-tier package layered over the kernel `IEventLog` substrate. The audit substrate is now built and is consumed by nearly every subsequent domain substrate via the standard both-or-neither constructor pattern (audit-disabled / audit-enabled overloads).

ADR 0029 resolved the federation vs. gossip conflict: federation (`federation-*` packages) covers inter-organizational cross-jurisdictional sync; gossip (future `kernel-sync`) covers intra-team, same-trust-domain, role-attestation-driven sync. These are parallel, complementary models with distinct ADR/package homes. The gossip daemon (`kernel-sync`) is not yet built; its substrate prerequisites are now mostly in place following the W#34/W#35/W#37 transport and versioning substrates.

### UI Core (5 ADRs)

The ui-core tier defines the spec layer for Sunfish's UI component system. ADR 0017 established the four-contract model: every component has a semantic contract, an accessibility contract, a styling contract, and a motion contract, all framework-agnostic in `packages/ui-core/`. Framework-specific rendering is in adapter packages. ADR 0014 established parity as the default: every change to ui-core, every new adapter behavior, every new component must land in all first-party adapters (Blazor + React) in the same PR or register an explicit exception.

ADR 0025 established the CSS class prefix policy: consumer-facing classes use `sf-*`; internal bridge-adapter classes use `k-*` (matching Telerik's conventions for the compat surface); the `mar-*` prefix is deprecated. ADR 0024 expanded the `ButtonVariant` enum for cross-framework style parity. ADR 0023 expanded the dialog provider interface with per-slot class methods.

The ui-core tier has a notable gap: no ADR covers component state management patterns, animation tokens, or the theming system. ADR 0041 (Dual-Namespace Components) and ADR 0022 (Example Catalog) sit in the foundation tier despite being closely related to ui-core; this cross-tier classification suggests the tier boundary may need revisiting in a future amendment.

### Adapter (3 ADRs)

The adapter tier has the smallest ADR count of any active tier. ADR 0014 (parity policy) and ADR 0034 (accessibility harness per adapter) cover the cross-adapter contract. ADR 0030 covers the React adapter scaffolding. The React adapter is behind the Blazor adapter in coverage; ADR 0030 acknowledges this and treats the React adapter as a "catch-up target" rather than a co-equal first-party release.

The accessibility harness (ADR 0034) mandates WCAG 2.2 AA compliance at the adapter level: each adapter ships a dedicated accessibility test harness, contrast tests, keyboard-navigation tests, and ARIA-attribute verification. This is separately reinforced by the W#42 (Wayfinder / ADR 0065) council finding that dispatches a WCAG/a11y subagent on every UI-bearing follow-on.

### Block (5 ADRs)

Block-tier ADRs cover domain-specific composition decisions. The five block ADRs all emerged from the property-operations vertical cluster: Work Order domain model (ADR 0053), Electronic Signature capture and document binding (ADR 0054), Leasing Pipeline and Fair Housing compliance posture (ADR 0057), Vendor onboarding posture (ADR 0058), and Public Listing surface (ADR 0059).

ADR 0053 (Work Order) is the cluster keystone: eight other cluster intakes directly reference the `WorkOrder` entity shape. The work-order entity is CP-class for appointment-slot bookings (a slot can have only one appointment at a time, per paper §6.3 quorum semantics), while most of its other fields are AP-class. ADR 0057 (Leasing Pipeline) adds FHA structural prevention (non-uniform criteria application is architecturally impossible via state-machine guards) and FCRA adverse-action letter generation. ADR 0058 (Vendor Onboarding) adds magic-link onboarding via the bidirectional messaging substrate (ADR 0052), TIN field-level encryption via Foundation.Recovery (ADR 0046), and an event-sourced performance log. ADR 0059 (Public Listing) establishes a server-rendered listing surface with a capability-promotion model: anonymous visitors see block-level info; email-verified prospects see full addresses; applicants see floor plans.

This count (5) undercounts the actual blocks built. The property-operations cluster has yielded approximately 14 domain workstreams, the majority of which were built under the umbrella ADRs above or via existing foundation ADRs rather than block-tier ADRs of their own. The cluster's built modules include: blocks-properties, blocks-property-equipment (renamed from assets per UPF Rule 4), blocks-inspections, blocks-maintenance (work orders + vendor coordination), blocks-property-leasing-pipeline, blocks-leases, blocks-public-listings, and kernel-signatures. This suggests the block tier is architecturally under-documented in the ADR journal relative to the volume of built code. Future vertical clusters (healthcare, government, highway management per ADR 0056's taxonomy-bundle framing) will need block-tier ADRs from the outset if the journal is to remain an accurate read-model.

### Accelerator (7 ADRs)

The accelerator tier covers the two concrete Zone instantiations: Anchor (Zone A, local-first desktop) and Bridge (Zone C, hybrid hosted-node-as-SaaS). ADR 0006 established that Bridge is a generic SaaS shell, not a vertical application; property management is Bridge's first reference bundle. ADR 0026 (superseded) was the pre-Bridge-posture decision; ADR 0031 replaces it with the full hybrid multi-tenant model.

ADR 0031 (Bridge hybrid multi-tenant SaaS) establishes Zone C default: shared control plane, per-tenant isolated data plane, ciphertext at rest as defense-in-depth. Multi-tenant bugs that leak bytes yield undecryptable ciphertext — the "last line of defense" invariant. Option B (contractual isolated tenant deployment) is a named upgrade tier for regulated industries.

ADR 0032 (Multi-Team Anchor) establishes that Anchor uses per-team `TeamContext` scopes within a single process (Option C default), with Option B (shell + child processes) reserved as a compliance-tier opt-in. This is a resource-footprint constraint as much as a security decision: Anchor's idle RAM budget is under 1GB, and four full kernel stacks for four teams is impractical on 8GB laptops.

ADR 0033 (Browser Shell render model) chose a hybrid WASM + Server render posture: key material handling in WASM (satisfying the operator-never-holds-decryption-key invariant from paper §11.2); live UI rendering in Blazor Server (preserving developer familiarity and server-side observability). WASM-only posture was rejected due to the significant packaging and engineering overhead.

ADR 0044 and ADR 0048 address Anchor's platform strategy: Windows-only for Phase 1 (ADR 0044); multi-backend MAUI for Phase 2 (native MAUI for Win/Mac/iOS/Android, MAUI Avalonia for Linux, exploratory WASM, ADR 0048). The iOS mobile strategy is the most architecturally significant outstanding accelerator decision: workstream #23 (iOS Field-Capture App) is `ready-to-build` but not yet started; it is the first Swift code in the repository and introduces a new `accelerators/anchor-mobile-ios/` package family.

### Governance (8 ADRs)

The governance tier covers structural decisions about the repository's own process. ADR 0001 (Schema Registry), ADR 0016 (naming convention), ADR 0018 (governance model + license), ADR 0029 (federation reconciliation), and ADRs 0037–0039 (CI platform, branch protection, required-check minimalism) all sit here.

ADR 0018 establishes a BDFL governance model with explicit trigger-based evolution paths (3+ sustained external committers → maintainer tier; 10+ → TSC consideration; first production corporate adopter → SLA/SLO + license review). The repository is public, pre-LLC, pre-community. ADR 0042 (Subagent-Driven Development) is also classified governance; it establishes the high-velocity agentic session model and the kill-switch/audit-trail safety controls that ADR 0043 extends.

ADR 0043 (Unified Threat Model) is the system-level security framing that cross-cuts ADRs 0038, 0039, and 0042. It catalogs the "chain of permissiveness" — the way dispatcher authority, merge-gate bypasses, and solo-maintainer credential concentration compose into an attack surface — and names five threats (T1–T5) with current mitigations vs. gaps.

### Policy (3 ADRs)

The policy tier is thin: ADR 0043 (threat model), ADR 0060 (right-of-entry compliance), and ADR 0064 (runtime regulatory policy evaluation). ADR 0060 establishes that jurisdiction policy is data, not code: a `foundation-jurisdiction-policy` substrate resolves per-jurisdiction `EntryPolicy` values; `Sunfish.JurisdictionPolicy.Defaults@1.0.0` seeds 8 common US jurisdictions; tenant overrides are supported. ADR 0064 (Proposed) establishes the runtime regulatory / jurisdictional policy evaluation framework, which composes with ADR 0062 (Mission Space) to produce the capability-vs-regulatory-posture signal.

### Governance (8 ADRs) — development-model and branch-protection ADRs

ADR 0042 (Subagent-Driven Development) is a governance ADR that formalizes the high-velocity development model used to build the substrate cohort. The core decision: parallel background dispatch is the default for parallelizable task shapes; sequential development is reserved for tasks with genuine attention dependencies. The model rests on three execution premises: worktrees provide filesystem isolation (each subagent works on `.claude/worktrees/agent-<id>/`); auto-merge is the completion gate (CI is the only review gate); background-task notifications close the dispatch loop (controller reacts, not polls). The failure modes are named and mitigated (husky bootstrap issues, worktree-cleanup races, dedup blast-radius surprises). ADR 0043 extends ADR 0042 with the system-level threat model for this model.

ADR 0038 (Branch Protection via Rulesets) establishes that the repository uses GitHub Rulesets (not the legacy branch-protection API). The decision rests on rollback story (rulesets have a clean DELETE endpoint), canary mode support (enforcement: evaluate before enforcement: active), multi-rule layering (one policy payload rather than re-PUT-the-whole-object), and the forward direction of GitHub investment. The `apply-main-ruleset.sh` idempotent script is the canonical IaC representation. ADR 0039 (Required-Check Minimalism) adds the constraint that required checks should be only the checks that are genuinely safety-relevant; strict-status-check policy was disabled 2026-04-28 to stop auto-merge stalling on BEHIND PRs.

### Tooling (1 ADR) and Process (1 ADR)

These are the two thinnest tiers by ADR count, each with a single ADR. ADR 0010 covers the templates module boundary (Foundation.Catalog vs. blocks-templating extraction criteria): the decision is to keep templates in `Foundation.Catalog.Templates` and extract to a dedicated `blocks-templating` module only when one of three extraction criteria fires. ADR 0040 covers the AI-first translation workflow with a 3-stage validation gate: multi-engine forward translation → back-translation with a different engine → semantic-similarity drift check at 30% threshold. The rationale: full human translation across 16 locales is cost-prohibitive pre-v1; AI-first with back-translation catches the meaning-drift error class while keeping per-locale turnaround same-day.

Both tiers are structurally under-documented; the tooling tier in particular is missing ADRs for the scaffolding CLI, the Roslyn analyzer enforcement gate (built under W#14 as `SUNFISH_PROVNEUT_001` + `RS0030` BannedApiAnalyzers, but not its own ADR), the ADR projection tooling itself (built under the Stage 5 ADR portfolio foundation work), and the `act` local CI runner decision. The process tier is missing ADRs for the ICM pipeline structure itself, the commit-message conventions, and the package pre-release-latest-first policy. These gaps are low-risk for current operations (conventions are documented in CLAUDE.md and memory files) but would become high-risk if a second contributor joined without access to those context files.

---

## 2. Cross-cutting concerns

### Security

Security in Sunfish is layered across multiple ADRs and is the concern category with the most cross-tier participation (7 ADRs in the security concern projection). The core posture: Ed25519 signatures with algorithm-agility tagging as a v1 requirement (ADR 0004); post-quantum readiness via dual-sign transition window (ADR 0004); AES-256-GCM field-level encryption with tenant-key provider delegation (ADR 0046); 3-of-5 social recovery with timed grace period and paper-key fallback (ADR 0046); a unified threat model for the public-OSS / solo-maintainer chain of permissiveness (ADR 0043); HMAC-based key management via `ITenantKeyProvider` abstraction; PCI SAQ-A scope enforcement via structural prevention of PAN/CVV from entering Sunfish contract surfaces (ADR 0051).

The operator-never-holds-decryption-key invariant (paper §11.2 / §17.2) appears explicitly in ADRs 0031, 0032, and 0033, shaping the Bridge multi-tenancy model, Anchor's team isolation, and the browser-shell render-mode respectively. This invariant is the single most load-bearing security principle; every subsequent distribution design must not violate it.

The historical-keys projection (ADR 0046-A1, Proposed) addresses signature survival under operator-key rotation — the concern that re-keying an operator's signing keypair would invalidate previously signed documents. This ADR remains in Proposed status; its Stage 06 build is not yet scheduled.

Gap: key-version rotation (deferred from ADR 0046-A4.3; Phase 1 ships fixed `KeyVersion=1`); institutional custodian and biometric-derived recovery (deferred from ADR 0046 to post-MVP).

### Persistence

Persistence crosses five ADRs in the concern projection. The schema registry (ADR 0001) governs schema lifecycle. Bundle manifest schema (ADR 0007) defines the declarative composition surface. Module-entity registration (ADR 0015) specifies how block EF Core entity configurations compose into the single `SunfishBridgeDbContext` via `ISunfishEntityModule`. CRDT storage (ADR 0028) is the operational backing for AP-class data. Audit storage (ADR 0049) is a distinct kernel-tier package layered over `IEventLog`.

The persistence model separates concerns sharply: relational persistence for entities (EF Core, Bridge-hosted); CRDT-backed document storage for AP-class data (Loro, local-first); event log for audit and sync delta streams; blob storage for content-addressed artifacts. These four persistence modalities coexist; they are not unified.

### Distribution

Distribution is the most densely connected concern (9 ADRs). The key architectural shape: a four-tier delivery stack — local CRDT document store, outbound sync queue, peer gossip (kernel-sync, not yet built), and managed relay (Bridge). The three-tier transport model (ADR 0061) handles peer connectivity: Tier 1 mDNS for LAN, Tier 2 mesh VPN (Headscale-first, vendor-neutral adapters), Tier 3 Bridge relay as fallback. The `Foundation.Transport` substrate plus the `providers-mesh-headscale` adapter were built in workstream #30.

Gossip semantics (intra-team, leaderless, role-attestation-driven, paper §6.1) are fully specified (ADR 0029 + ADR 0028) but the `kernel-sync` gossip daemon is not yet built. The `SyncState` multimodal encoding contract (ADR 0036) — five states × five channels (color, icon, short label, long label, ARIA) — has a built `foundation-ui-syncstate` package (W#37).

The Mission Space negotiation protocol (ADR 0062) composes CRDT (ADR 0028) and audit (ADR 0049) to implement a 10-dimension capability envelope. The coordinator and probes are built (W#40).

### Audit

Audit has 6 ADRs spanning the concern projection. The `kernel-audit` substrate (ADR 0049) is the canonical home for all audit events. Its both-or-neither constructor pattern — `IAuditTrail`-taking overloads alongside audit-disabled overloads, with DI registration enforcing the pair — has been applied consistently across: field encryption (W#32), transport selection (W#30), version vector incompatibility (W#34), form-factor migration (W#35), mission space negotiation (W#40), regulatory policy evaluation (W#39), and leasing pipeline (W#22). This pattern has emerged as the de facto audit integration convention (see Section 8).

Signature audit specifically spans ADR 0054 (electronic signatures), ADR 0046 (recovery events), and ADR 0062 (mission space events). The cryptographically-signed audit trail for recovery events (ADR 0046) — where recovery audit records are encrypted, signed by attesting trustees, and timestamped — is a uniquely strong posture vs. the standard audit pattern.

### Regulatory

Regulatory compliance has 3 ADRs: Fair Housing Act + FCRA compliance in the leasing pipeline (ADR 0057), right-of-entry compliance framework (ADR 0060), and runtime jurisdictional policy evaluation (ADR 0064, Proposed). The current posture is structural prevention: fair-housing compliance is built into the `blocks-property-leasing-pipeline` state machine (preventing non-uniform criteria application); FCRA workflows are structurally enforced (adverse-action letter generation, consent flags, timelines); right-of-entry policy is data-driven via the jurisdiction policy registry.

The runtime regulatory framework (ADR 0064) is the most ambitious regulatory piece: it generalizes the per-domain compliance approach into a substrate-level probe that composites per-source confidence signals and drives capability gating. General-counsel content (per-jurisdiction rule content) ships in subsequent phases as legal sign-off completes.

Gap: ADR 0064 is Proposed, not Accepted; this concern is the one most likely to require substantive revision before v1.

### Configuration

Configuration has 2 ADRs in the concern projection (ADR 0009 and ADR 0055), but the concern is significantly deeper in practice. ADR 0009 (FeatureManagement) establishes three distinct concepts with a single resolution path: feature flags (binary on/off at runtime), entitlements (capabilities attached to a subscription tier), and editions (named capability bundles mapped in the bundle manifest). The resolution path composes all three at runtime via `IFeatureResolver`; bundle manifests are the authoring source for entitlements. ADR 0055 (Dynamic Forms) extends this with schema-registry-driven admin-defined types: a four-layer composition (schema definition → rule engine → section-permission surface → CRDT-synced instance tree) that lets non-developer operators define forms without engineering intervention.

The Wayfinder system (ADR 0065, Proposed) significantly deepens this concern by adding auditable, CRDT-backed configuration primitives (Standing Orders) as a layer below feature flags. When built (W#42, `ready-to-build`), Wayfinder will provide the foundation for operator-defined system behavior — not user-visible flags but structural decisions like "which payment provider is active for this tenant" or "what is the minimum notice period for vendor entry in this jurisdiction." See Section 4 for a full treatment.

The ADR 0009 amendment (W#43, `design-in-flight`) will add Standing Order as a fifth concept alongside flags/entitlements/editions — Wayfinder-defined configuration values surfaced through the existing FeatureManagement resolution path. This creates a clean layering: feature flags (human-authored per deploy), entitlements (subscription-tier-gated), editions (bundle-defined), and standing orders (operator-defined CRDT-backed policy) all resolve through one `IFeatureResolver`.

### Mission Space

Mission Space has 2 ADRs in the current concern projection (ADRs 0062 and 0063). As a concern, it covers the question: "what is this Sunfish deployment capable of, and what are its minimum system requirements?" ADR 0062 defines the runtime 10-dimension envelope and the coordinator/probe pattern. ADR 0063 defines the install-UX requirements layer (bundle manifest baseline `MinimumSpec` plus per-feature overrides). ADR 0064's jurisdictional regulatory evaluation is closely related. All three are built (W#40, W#41, W#39) even though 0063 and 0064 remain in Proposed status.

### Multi-Tenancy

Multi-tenancy has 2 ADRs: ADR 0008 (Foundation.MultiTenancy primitives + Finbuckle boundary) and ADR 0031 (Bridge hybrid multi-tenant deployment model). ADR 0008 separates three concerns previously conflated in `ITenantContext`: tenant identifier (`TenantId`), caller identity (`UserId` / `Roles`), and authorization (`HasPermission`). The new package `Sunfish.Foundation.MultiTenancy` holds `TenantId`, `ITenantContext`, and `ICurrentTenant`; Finbuckle's per-tenant resolution is scoped to Bridge's WebApplication host and does not leak into Foundation packages. An in-memory implementation ships alongside for tests and lite-mode deployments.

The concern is structurally important because multi-tenancy isolation failures in a system using ciphertext-at-rest yield undecryptable ciphertext — this is ADR 0031's core defense-in-depth argument. The operative question for an attacker who has breached the data plane is "do I also have the tenant's decryption key?" The invariant says no: operators cannot hold the decryption key for tenant data. The type-surface convention for multi-tenant query values (specifically, the `TenantSelection` value object needed for `AuditQuery`) is `design-in-flight` (workstream #1), meaning the Tier 2 audit retrofit (`AuditQuery.TenantId → TenantSelection`) is blocked. This also means that cross-tenant audit queries (required for Bridge platform-admin support tooling) are incomplete until workstream #1's M2 milestone delivers.

### Accessibility

Accessibility has 2 ADRs in the current concern projection: ADR 0032 (Anchor multi-team workspace switching) and ADR 0034 (accessibility harness per adapter). ADR 0032's team-isolation model choice was partially accessibility-driven: Option B (shell + child processes) would have required per-team navigation and loading states that ADR 0032 characterizes as a cognitive-load violation of paper §13.1 (complexity-hiding standard). ADR 0034 mandates WCAG 2.2 AA conformance at the adapter level — each adapter ships a dedicated accessibility test harness, contrast tests, keyboard-navigation tests, and ARIA-attribute verification. The harness is per-adapter so that Blazor and React adapter accessibility can be independently verified.

The Wayfinder ADR 0065 has raised the accessibility posture significantly beyond what ADR 0034 alone requires. Specific WCAG 2.2 criteria are now called out by Success Criterion number in the ADR: SC 3.3.7 (Accessible Authentication — cognitive function tests forbidden in MFA UX), SC 3.3.8 (Redundant Entry — error prevention via reversible/checked/confirmed for legal/financial commitments). EN 301 549 procurement compliance is cited as a Bridge tenant requirement in EU jurisdictions. The practical implication: every UI-bearing follow-on workstream now dispatches a dedicated WCAG/a11y subagent as part of the council review chain. This is a significant process change that will affect every block-tier ADR in the W#42 and subsequent workstream chain.

---

## 3. The substrate cohort (W#33 follow-on chain)

Starting in late April 2026, a cluster of workstreams shipped substrate packages that form the implementation backbone of the architecture's most complex zones: CRDT schema migration, form-factor adaptation, transport, versioning, and mission space. The cohort spans workstreams #30–#41, with some individual workstreams shipping 5–8 phases across multiple PRs each.

**What shipped in this cohort:**

- `Foundation.Transport` + `providers-mesh-headscale` + `BridgeRelayPeerTransport` (W#30, 87 tests) — the three-tier IPeerTransport model from ADR 0061, including a Headscale-backed Tier 2 mesh adapter and a Bridge WebSocket relay fallback.
- `Foundation.Taxonomy` Phase 1 (W#31) — versioned product model with lineage for taxonomy classification, composition by ADR 0054/0056 consumers.
- `Foundation.Recovery` field-encryption substrate (W#32, 93 tests) — AES-GCM field-level encryption with audit emission, per ADR 0046-A2/A3/A4/A5. Unblocked W#18 Vendors Phase 4, W#22 leasing FCRA SSN, W#23 offline PII, ADR 0051 card-on-file.
- `Foundation.Versioning` Phase 1 (W#34, 59 tests) — the version-vector compatibility contract from ADR 0028-A6+A7; 6-rule `DefaultCompatibilityRelation` engine; two-phase `InMemoryVersionVectorExchange` with both-or-neither teardown.
- `Foundation.Migration` Phase 1 (W#35, 78 tests) — form-factor migration from ADR 0028-A5+A8; 8-form-factor migration table; `InMemorySequestrationStore`; 10 audit event types.
- `Foundation.UI.SyncState` (W#37, 20 tests) — the 5-value `SyncState` enum with canonical lowercase wire-form round-trip helpers, per ADR 0036-A1.
- `Foundation.MissionSpace` Phase 1 (W#40) — 10-dimension coordinator + observer fanout + 10 default `IDimensionProbe<T>` implementations, per ADR 0062.
- `Foundation.MissionSpace.Regulatory` Phase 1 (W#39) — composite jurisdictional probe + rule engine + sanctions + ADR 0064-scoped policy surface.
- `Foundation.MissionSpace.Requirements` Phase 1 (W#41) — `MinimumSpec` + `IMinimumSpecResolver` + per-platform composition + force-install + operator override, per ADR 0063.

**Cohort patterns that emerged (see also Section 8):** every substrate in this cohort follows the same structural template — two-overload constructor (audit-disabled / audit-enabled, `both-or-neither`), DI extension with `AddInMemory*()` + tenant-keyed overload, `apps/docs/foundation/*/overview.md` documentation page, alphabetized audit payload factories, `JsonStringEnumConverter` + `JsonNamingPolicy.CamelCase` for all enums, canonical-JSON round-trip verification.

**What unblocked:** the cohort collectively unblocked the iOS Field-Capture App (W#23, `ready-to-build`), the Wayfinder system build (W#42, `ready-to-build`), the messaging substrate build (W#20, `building`), and the leasing-pipeline's remaining Phase 6 compliance half (pending ADR 0060 Stage 06).

---

## 4. Wayfinder configuration model (W#34 → W#42 → W#43)

The Wayfinder system is the configuration layer for Sunfish. It emerged from the W#33 mission-space research as one of the most significant architectural gaps: Sunfish had feature flags (ADR 0009) but no principled model for auditable, CRDT-backed, operator-configurable system behavior. W#34 produced the discovery document; ADR 0065 was accepted 2026-05-02.

The model has four layers:

1. **Standing Order** — the atomic configuration primitive. A `StandingOrder` record carries a `TenantId`, an `ActorId`, an `Instant`, a `StandingOrderScope` (path-based, hierarchical), a list of `StandingOrderTriple` (subject/predicate/object RDF-style triples), a `Rationale` string, and an optional `ApprovalChain`. Standing Orders are CRDT-backed for concurrent-issuance convergence; every Standing Order emits an audit event at issuance time (audit-by-construction). The `foundation-wayfinder` package owns this type.

2. **Atlas** — the searchable projection of all Standing Orders. The Atlas index is schema-driven (following JetBrains' pattern of describing every settable surface via schema); search latency target is P95 ≤ 100ms over a 10K-setting catalog. The Atlas exposes both a form view and a JSON view as dual projections of the same Standing Order log (following VS Code's settings.json / Settings UI pattern). The Atlas's build is part of W#42 (ADR 0065 Stage 06 hand-off authored 2026-05-02).

3. **Helm** (pending ~ADR 0066) — identity and trust configuration surface. Helm governs how actors are identified and what trust relationships exist between them. The ADR is queued as a design-in-flight follow-on; its scope is cross-cutting and touches the multi-tenancy type surface convention (workstream #1).

4. **Standing Order integration-config surface** (pending ~ADR 0067) — per-tenant integration provider configuration. Where ADR 0013 defines the contracts, ~ADR 0067 defines how operators configure which providers are active and with what credentials, expressed as Standing Orders with well-known paths.

**ADR 0009 amendment (W#43):** the council review of ADR 0065 produced a finding (F4) that `Foundation.FeatureManagement` should consume Wayfinder's Standing Order contract as a fifth concept alongside flags/entitlements/editions. W#43 is `design-in-flight`; the amendment is gated on W#42 reaching Status: Accepted.

**Current status:** W#42 is `ready-to-build` (ADR 0065 Accepted; Stage 06 hand-off authored). W#43 is `design-in-flight`. ADRs ~0066–~0068 are queued. The `foundation-wayfinder` package does not yet exist; its scaffold is Phase 1 of W#42.

---

## 5. Mission Space framework (W#33 → ADR 0062/0063/0064)

The Mission Space framework answers the question: "given this device's hardware, network posture, regulatory jurisdiction, and operator policy, what is this Sunfish deployment capable of?" It is the runtime capability envelope that every feature gate consults before deciding whether a capability is available.

The framework was specified in the W#33 Mission Space Matrix research (10-dimension model, 7-section discovery document, Pedantic-Lawyer-hardened §5.9). The 10 dimensions are: hardware tier, form factor, network posture, sensor availability, display class, power profile, regulatory jurisdiction, operator policy, user consent, and version compatibility. Coverage at the time of W#33: 1 dimension Specified (hardware tier), 5 Partial, 4 Gap.

**Three ADRs define the framework:**

ADR 0062 (Mission Space Negotiation Protocol, Accepted) defines the runtime layer: `IMissionEnvelopeProvider` as the central coordinator holding all 10 dimensions; `ICapabilityGate<TCapability>` as the per-feature consumption surface; `IDimensionProbe<T>` as the per-dimension measurement primitive. The coordinator coalesces probe updates with 100ms debouncing; the 100-pending overflow guard prevents unbounded queuing. 10 default probes are shipped; bespoke probes are the feature author's responsibility.

ADR 0063 (Mission Space Requirements, Proposed) defines the install-UX layer: `MinimumSpec` declared in the bundle manifest establishes the baseline; per-feature `MinimumSpec` overrides allow fine-grained install-time checking. At install time, the bundle baseline is checked; at runtime, per-feature gates use per-feature spec for `FeatureVerdict.UserMessage` content. This is the "user-facing capability explanation" layer.

ADR 0064 (Runtime Regulatory / Jurisdictional Policy Evaluation, Proposed) defines a composite probe that aggregates per-source confidence signals (jurisdiction detection, sanctions screening, operator policy assertions) and drives capability gating. Content for specific jurisdictions ships in subsequent phases as general-counsel review completes; the substrate framework ships Phase 1.

**All three substrates are built** (W#40, W#41, W#39). The ADRs for 0063 and 0064 remain Proposed pending formal CO sign-off on the "Approved Gap" status fields from the W#33 discovery document. The gap closure approval is the pipeline closure step.

---

## 6. Open architectural questions

The following questions are distilled from open-question sections and explicitly-deferred items across ADRs. Each represents an unresolved design decision that may require a future amendment or new ADR.

1. **CRDT gossip daemon (`kernel-sync`) design.** ADR 0028 and ADR 0029 together specify the gossip protocol semantics (leaderless, 30s tick, HELLO/CAPABILITY_NEG/DELTA_STREAM/GOSSIP_PING, role-attestation-driven), and ADR 0061 specifies the transport. The `kernel-sync` package itself has not been scaffolded. The paper §6.1–6.2 protocol is specified but not implemented; this is the single largest gap between paper spec and code. (Citations: ADR 0027, ADR 0028, ADR 0029, ADR 0061)

2. **DEK key-version rotation.** ADR 0046-A4.3 deliberately ships `KeyVersion=1` for Phase 1 with rotation deferred. A future amendment needs durable key-version storage and ciphertext re-encryption machinery. Blocker: the rotation primitive does not exist; a follow-up ADR is required before v1. (Citation: ADR 0046, amendment A4.3)

3. **Multi-tenancy type surface — `TenantSelection` value object.** Workstream #1 is `design-in-flight` and blocks the Tier 2 audit retrofit (`AuditQuery.TenantId → TenantSelection`). Until workstream #1's M2 delivers the `TenantSelection` type, multi-tenant audit queries remain incomplete. (Citation: ADR 0008, ledger workstream #1)

4. **Historical-keys projection (ADR 0046-A1).** This Proposed ADR covers the case where an operator rotates the signing keypair and previously-signed documents must remain verifiable. The ADR is accepted but the Stage 06 build is not scheduled. Without this, key rotation at scale risks signature verification failures on historical documents. (Citation: ADR 0046-A1)

5. **iOS native PencilKit/CryptoKit integration.** W#21 (kernel-signatures) explicitly deferred the iOS native signature capture path to W#23. W#23 is `ready-to-build` but not started. This is the first Swift code in the repository and the first `accelerators/anchor-mobile-ios/` package. (Citation: ADR 0054, ADR 0048, workstream #23)

6. **Wayfinder cross-tenant Atlas visibility.** ADR 0065 open question 2: Bridge platform-admin needs read access to tenant Standing Orders for support/compliance audits, gated on `Capability.PlatformAdmin`. This capability is not yet defined; its definition belongs to ~ADR 0067 / W#43. Without resolution, platform-admin support tooling is incomplete. (Citation: ADR 0065, open question 2)

7. **Standing Order amendment semantics.** ADR 0065 open question 3: the Standing Order contract has `RescindAsync` but no `AmendAsync`. The recommended resolution (model as `AmendAsync` with linked audit records) is deferred to the scaffolding stage of W#42. (Citation: ADR 0065, open question 3)

8. **ADR dependency graph sparsity.** The GRAPH.md composition graph has only 2 explicit edges despite 61 ADRs with many documented narrative dependencies. The structural citation gaps (where an ADR claims a symbol or type exists in another ADR but the frontmatter `composes:` field is empty) are a known ADR-quality gap. The council review process has caught several structural-citation failures (e.g., ADR 0028-A6.2 citing `required: true` on `ModuleManifest` when the field was on `ProviderRequirement`); pre-merge council review is now canonical but retrospective cleanup of existing ADRs is incomplete. (Citation: GRAPH.md, feedback_council_can_miss_spot_check_negative_existence.md)

9. **Dual-namespace component dedup risk.** ADR 0041 documents that `SunfishGantt`, `SunfishScheduler`, `SunfishSpreadsheet`, and `SunfishPdfViewer` each exist in two namespace folders by design (rich vs. MVP). Any session that discovers apparent duplication and removes one half without reading ADR 0041 would break a consumer path. This is a code-discoverability risk that is mitigated by the ADR but not by any automated guard. (Citation: ADR 0041)

10. **Messaging substrate consumer wiring.** W#20 (Bidirectional Messaging Substrate) is `building` but has 8 open questions (OQ-M1 through OQ-M8) in ADR 0052 that require Stage 02 design decisions, including TCPA/CAN-SPAM consent surface (OQ-M7) and per-tenant custom domain (OQ-M8). These questions are not blocked — they are stage-sequenced — but they represent unresolved detail that will shape the Stage 06 implementation. (Citation: ADR 0052)

---

## 7. Forward-looking — what's queued

**Immediately actionable:**

- **W#42 — Wayfinder System** (`ready-to-build`). ADR 0065 Accepted 2026-05-02; Stage 06 hand-off authored. `foundation-wayfinder` package scaffold is Phase 1. The build will be the first exercise of the Standing Order / Atlas / diff-preview patterns in production code.
- **W#23 — iOS Field-Capture App** (`ready-to-build`). First Swift code in the repository; new `accelerators/anchor-mobile-ios/` package family. Substrate hand-off authored 2026-04-30. This workstream consumes the largest number of existing substrate packages (kernel-signatures, Foundation.Transport, Foundation.Recovery, Foundation.Migration, Foundation.Versioning) and is the forcing function for several deferred decisions (ADR 0032-A1 QR-pairing schema, Keychain access policy per ADR 0028-A9).
- **W#20 — Bidirectional Messaging Substrate** (`building`). Phases 0/2.1/3 shipped; remaining phases queued. Per ADR 0052 the substrate is the dependency for vendor coordination (W#18), work orders (W#19), leasing pipeline (W#22), public listings (W#28), and Phase 2 outbound statements (W#5).

**Design-in-flight (XO authoring backlog ~9 ADRs):**

- **ADR 0062/0063/0064 pipeline closure** — W#33 "Approved Gap" sign-off from CO; enables formal Accepted status for the three Proposed Mission Space ADRs.
- **~ADR 0066 (Helm + identity Atlas)** — W#34 Wayfinder follow-on; cross-cutting identity and trust configuration.
- **~ADR 0067 (integration-config)** — per-tenant integration provider configuration surface as Standing Orders.
- **~ADR 0068 (tenant security policy)** — tenant-level security policy configuration surface.
- **ADR 0009 amendment (W#43)** — FeatureManagement as a Standing Order consumer; `design-in-flight`, gated on W#42 Accepted.
- **W#1 (multi-tenancy type surface)** — `design-in-flight`; blocks Tier 2 audit retrofit.
- **W#29 (Owner Web Cockpit)** — `design-in-flight`; Anchor + Bridge cockpit views; resolves cluster OQ1 (multi-actor permissions matrix).

**Medium-term horizon:**

- The compat-vendor adapter expansion (Syncfusion, DevExpress, Infragistics following the compat-telerik pattern) is queued; 4 intake decisions pending; starts after current style-parity work lands.
- The Global Domain Types wave (ADR 0035 — PersonalName, Money, Address) is a separate wave with its own ADR and timeline; it is a v1 requirement but has not entered Stage 01 discovery.
- The gossip daemon (`kernel-sync`) has no workstream opened yet; it depends on the peer transport substrate (W#30, built) and the version-vector substrate (W#34, built) as prerequisites.
- The Business MVP Phase 1 G7 conformance scan (workstream #7) is blocked on G6 trustee orchestration completion (workstream #8, `building`).

---

## 8. Discipline patterns (cohort lessons)

The following patterns emerged from the substrate cohort built in late April–early May 2026. They are applied consistently across the codebase but are not yet the subject of dedicated ADRs. Each is a candidate for formalization.

**Both-or-neither audit constructor pattern.** Every substrate that integrates with `kernel-audit` ships two constructor overloads: one without `IAuditTrail` (audit-disabled, for tests and low-privilege deployments) and one with `IAuditTrail` + `TenantId` (audit-enabled). DI extensions enforce the pair at registration time. Neither overload silently omits audit emission; the disabled form simply routes to a no-op. This pattern appears in W#32, W#34, W#35, W#37, W#39, W#40, W#41, and has been retroactively applied in several PRs. It is the canonical Sunfish audit-integration pattern. Candidate ADR: a foundation-tier ADR on audit-integration conventions.

**Pre-merge council canonical.** Following several pre-ADR-0028 incidents (false-negative council findings, false-positive symbol citations, structural-citation failures where a council subagent cited a field on the wrong type), the practice of running a parallel council review before merging every substrate or governance ADR became mandatory. The council pattern dispatches 3–5 Opus 4.7 subagents in parallel with adversarial review perspectives (Skeptical Implementer, Outside Observer, Pedantic Lawyer, Security Auditor, Accessibility Reviewer); findings are incorporated or explicitly deferred before merge. The pattern now captures a XO self-audit checklist (§A0) on every ADR. Candidate ADR: a governance-tier ADR formalizing the council process.

**§A0 self-audit checklist.** Before dispatching council subagents, the XO session now runs a self-audit against the ADR's own open-questions section, the GRAPH.md edges, and a structural-citation check that verifies every cited symbol against the codebase before declaring the ADR council-ready. This was not a practice at ADR 0028's first council review and would have caught at least two of the retracted amendments (A2.4, A2.10). Candidate ADR: a companion to the council ADR.

**Three-direction structural-citation check.** Council subagents can miss in three directions: (1) false-negative — failing to flag a real problem; (2) false-positive — flagging a non-problem as a blocker; (3) structural — citing a symbol or field that exists on a different type than claimed. The XO now spot-checks all three directions independently, with reading the ADR's actual schema (not grep alone) as the verification method for structural citations.

**Substrate-vs-consumer separation.** Every substrate in the cohort ships with a `Substrate-only` note in its hand-off: consumer wiring (the code that calls the substrate from a domain block or application) is a separate workstream. This discipline prevents scope creep in substrate PRs and ensures substrate APIs stabilize before consumers lock in. The iOS Field-Capture App (W#23), the gossip daemon, and the leasing-pipeline compliance half are all examples of consumer wiring deferred as separate workstreams.

---

## Cross-references

- **ADR journal (Layer 1):** `docs/adrs/` — 61 ADRs as of snapshot date; authoritative for all decisions
- **Status projection (Layer 2):** `docs/adrs/STATUS.md` — machine-generated from frontmatter
- **Index projection (Layer 2):** `docs/adrs/INDEX.md` — by tier × concern
- **Dependency graph (Layer 2):** `docs/adrs/GRAPH.md` — explicit ADR composition edges
- **Active workstreams ledger:** `icm/_state/active-workstreams.md` — per-workstream state machine
- **Foundational paper (Layer 4):** `_shared/product/local-node-architecture-paper.md` — v10.0, April 2026 — canonical source of architectural intent
- **ADR frontmatter schema:** `docs/adrs/_FRONTMATTER.md` — four-layer model definition and field specification
- **MASTER-PLAN.md:** `icm/_state/MASTER-PLAN.md` — stable goals, done-conditions, velocity baseline
