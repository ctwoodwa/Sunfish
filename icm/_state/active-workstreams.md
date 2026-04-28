# Active Workstreams Ledger

Canonical "what's in flight, who owns it, what state it's in" for cross-session coordination between the **research session** (ADRs, intakes, design decisions), **sunfish-PM session** (production code, PRs), and **book-writing session** (the-inverted-stack manuscript).

**All sessions read this at session start. Update on state change. Do not implement anything not listed as `ready-to-build`.**

---

## Status vocabulary

| Status | Meaning |
|---|---|
| `design-in-flight` | Research session is still working on the spec. **sunfish-PM: do not implement.** |
| `ready-to-build` | Spec is final; sunfish-PM may implement. A hand-off file in `handoffs/` describes the work. |
| `building` | sunfish-PM is implementing. Other sessions: do not open parallel PRs on the same scope. |
| `built` | Implementation complete (committed/merged). Watch for follow-up retrofits. |
| `held` | Paused pending external decision (user, third-party, or another workstream). |
| `blocked` | Depends on a workstream not yet resolved (link the dependency). |
| `superseded` | Replaced by another workstream (link the replacement). |

---

## Current state (last updated 2026-04-28)

| # | Workstream | Status | Owner (current phase) | Reference | Notes |
|---|---|---|---|---|---|
| 1 | Multi-tenancy type surface convention | `design-in-flight` | research | `icm/00_intake/output/tenant-id-sentinel-pattern-intake-2026-04-28.md` | Stage 00 widened intake; Stage 01 Discovery next. **Blocks Tier 2 retrofit (AuditQuery → TenantSelection) on the now-merged kernel-audit package.** |
| 2 | Kernel-audit Tier 1 retrofit | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/198 (merged 2026-04-28 16:30Z) | AttestingSignature pair shape + IAuditTrail XML doc fix shipped. Tier 2 (`AuditQuery.TenantId → TenantSelection`) remains blocked on workstream #1's M2. |
| 3 | Kernel-audit scaffold (PR #190) | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/190 (merged 2026-04-28 08:43Z) | Tier 1 drift now resolved by workstream #2 (PR #198). Only Tier 2 (`AuditQuery → TenantSelection`) remains, blocked on workstream #1's M2. |
| 4 | Kernel-event-bus test-flake fix (PR #191) | `built` (merged) | research session (exception turn) | https://github.com/ctwoodwa/Sunfish/pull/191 (merged 2026-04-28 08:26Z) | InMemoryEventBus + InMemoryEventBusTests deterministic readiness wait. Closes bug-268 in `.wolf/buglog.json`. |
| 5 | Phase 2 commercial MVP | `design-in-flight` | research | `icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md` + `docs/adrs/0051-foundation-integrations-payments.md` + `docs/adrs/0052-bidirectional-messaging-substrate.md` | Stage 00 intake. ADR 0049 accepted (built); **ADR 0051 (Payments) drafted 2026-04-28 (Proposed; awaiting council review + acceptance — PR #202); ADR 0052 reframed bidirectional 2026-04-28 (Proposed — PR #201)**. |
| 6 | Foundation-audit / kernel-audit relationship | `held` (Stage 00 stub) | research | `icm/00_intake/output/foundation-audit-vs-kernel-audit-relationship-intake-2026-04-28.md` | Held until a revisit trigger fires (see intake's "Revisit triggers" section). |
| 7 | Phase 1 G7 conformance scan | `blocked` | research (when G6 closes) | `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_business_mvp_phase_1_progress.md` | Blocked on G6 trustee orchestration completion. |
| 8 | Phase 1 G6 trustee orchestration | `building` (sunfish-PM) | sunfish-PM | PRs #178, #185 (merged); follow-up unspecified | Per Phase 1 progress memory; verify state via `gh pr list`. |
| 9 | ADR template self-audit checklist (Phase 1 of audit work) | `built` (PR #193, auto-merging) | research session (exception turn) | PR #193 — branch `chore/adr-template-self-audit` | Adds `docs/adrs/_template.md` with 5-min pre-acceptance self-audit. Forward-looking; lands when CI greens. |
| 10 | ADR Tier 2 anti-pattern sweep (15 ADRs) | `built` | research session (5 sonnet subagents) | `icm/07_review/output/adr-audits/anti-pattern-sweep-batch-{1..5}.md` | All 5 batches complete. 3 needs-amendment (0009, 0017, 0029); 12 annotation-only. Consolidated findings in `CONSOLIDATED-HUMAN-REVIEW.md`. |
| 11 | ADR Tier 1 full UPF audit (8 ADRs) | `built` | research session (8 opus subagents) | `icm/07_review/output/adr-audits/<NNNN>-upf-audit.md` (8 files) | All 8 audits complete. 1 A- (0043), 6 B/B-, 0 critical-fail. **4 critical-severity findings** identified (0008, 0013, 0028, 0046) — see `CONSOLIDATED-HUMAN-REVIEW.md` §1. |
| 12 | ADR audit consolidation + commit | `built` (PR #194, auto-merging) | research session | PR #194 — `chore/adr-audit-overnight-results` | Audit findings + ledger update bundled. Lands when CI greens. |
| 13 | Platform features brainstorm (layered startup / visibility modes / support delegation / sensitivity classification) | `brainstorm` | research session | `icm/00_intake/output/platform-features-brainstorm-2026-04-28.md` | 4-idea cluster captured. Held at Stage 00 until a forcing function promotes one to Stage 01 Discovery. Sequencing: 4 (sensitivity classification) is the foundation; 1 (layered startup) independent; 2+3 build on 4. |
| 14 | ADR 0013 provider-neutrality enforcement gate (Roslyn analyzer + BannedSymbols) | `built` (merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/196 (merged 2026-04-28 14:35Z) | Resolves audit finding C-1. Phase-2 `providers-*` scaffolds can now proceed with mechanical vendor-isolation gate active. SUNFISH_PROVNEUT_001 + RS0030 (BannedApiAnalyzers) auto-attached. |
| 15 | Foundation.Recovery package split (ADR 0046 + 0049 reconciliation) | `building` (Phase 1 ACCEPTED; Phase 2/3 unblocked) | sunfish-PM | `icm/_state/handoffs/adr-0046-recovery-package-split-INVENTORY.md` (PR #202 merged 2026-04-28 19:40Z) | **Phase 1 inventory shipped + research session ACCEPTED 2026-04-28** (PR #202 review comment, ID 4338508191). All 4 OQs answered cleanly: (1) no-substrate finding accepted; (2) IDisputerValidator moves with rest; (3) RecoveryEvent moves to foundation-recovery; (4) BIP-39 LogicalName updates to `Sunfish.Foundation.Recovery.bip39-english.txt`. **Phase 2 (scaffold foundation-recovery) + Phase 3 (file moves) UNBLOCKED.** UPF-graded A; api-change pipeline. |
| 16 | Property-operations vertical cluster (umbrella) | `design-in-flight` | research | `icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md` | 14 per-domain Stage 00 intakes drafted from multi-turn conversation 2026-04-28. Phase 2 deepening covering iOS field-capture, leasing pipeline, vendor coordination, signatures, asset/receipt/inspection/lease/mileage modules. **8 new ADRs + 4 amendments** queued. Awaiting user review. |
| 17 | Properties domain (cluster #1 spine) | `built` (first-slice merged) | sunfish-PM | https://github.com/ctwoodwa/Sunfish/pull/210 (merged 2026-04-28 21:05Z) | First-slice shipped: `Sunfish.Blocks.Properties` package — `Property` entity (`IMustHaveTenant`) + `PropertyId` + `PropertyKind` + `PostalAddress` value object + `IPropertyRepository` + `InMemoryPropertyRepository` + `PropertiesEntityModule` (ADR 0015) + `PropertyEntityConfiguration` (OwnsOne PostalAddress) + `AddInMemoryProperties()` DI + 25 tests + `apps/docs/blocks/properties/overview.md`. **Unblocks workstream #24 (Assets first-slice)** — but see project memory `project_workstream_24_assets_handoff_collision` for hand-off ambiguity that halts #24 pending research-session decision on package-name collision (`packages/blocks-assets/` already exists as UI-only catalog). PropertyUnit + ownership log queued as separate follow-up hand-offs. Hand-off OQ #2 (Money type) + OQ #3 (kitchen-sink seed pattern) flagged as deferred — see PR #210 description for resolution detail. |
| 18 | Vendors domain (cluster #2 spine) | `design-in-flight` | research | `icm/00_intake/output/property-vendors-intake-2026-04-28.md` | New "vendor onboarding posture" ADR; lightweight magic-link onboarding by default. Blocked by ADR 0049 (✓ accepted), workstream #15 (recovery split). |
| 19 | Work Orders coordination spine (cluster #3 spine) | `design-in-flight` (ADR 0053 drafted, in review) | research | `icm/00_intake/output/property-work-orders-intake-2026-04-28.md` + `docs/adrs/0053-work-order-domain-model.md` | Architectural keystone of the cluster. **ADR 0053 (work-order domain model) drafted 2026-04-28 (Proposed; awaiting council review + acceptance).** Right-of-entry compliance ADR queued separately (cluster INDEX #7). CP-class appointment slot per paper §6.3. |
| 20 | Bidirectional Messaging Substrate (cluster #4 spine) | `design-in-flight` (ADR drafted, in review) | research | `icm/00_intake/output/property-messaging-substrate-intake-2026-04-28.md` + `docs/adrs/0052-bidirectional-messaging-substrate.md` | Reframes ADR 0052 from outbound-only to bidirectional. First major exercise of providers-* pattern post-enforcement-gate (workstream #14 ✓ built — unblocked). **ADR 0052 drafted 2026-04-28 (Proposed); awaiting council review + user acceptance.** |
| 21 | Signatures + Document Binding (cluster cross-cutting) | `design-in-flight` (ADR 0054 drafted, in review) | research | `icm/00_intake/output/property-signatures-intake-2026-04-28.md` + `docs/adrs/0054-electronic-signature-capture-and-document-binding.md` | **ADR 0054 (electronic signature capture & document binding) drafted 2026-04-28 (Proposed; awaiting council review + acceptance).** kernel-signatures substrate (sibling to kernel-audit, kernel-security); UETA/E-SIGN compliant; PencilKit + CryptoKit + content-hash binding; ADR 0046 amendment for signature survival under key rotation; 5 new audit record types per ADR 0049. Load-bearing for Leases, Work Orders, Inspections, Leasing Pipeline, iOS App. |
| 22 | Leasing Pipeline + Fair Housing (cluster cross-cutting) | `design-in-flight` | research | `icm/00_intake/output/property-leasing-pipeline-intake-2026-04-28.md` | New leasing/FHA ADR + ADR 0043 addendum. Public-input boundary, capability promotion, jurisdiction-policy framework. |
| 23 | iOS Field-Capture App (cluster cross-cutting) | `design-in-flight` | research | `icm/00_intake/output/property-ios-field-app-intake-2026-04-28.md` | SwiftUI native (not MAUI). ADR 0028 mobile amendment + ADR 0048 amendment. New `accelerators/anchor-mobile-ios/`. |
| 24 | Assets domain (cluster module) | `ready-to-build` (first-slice; gated on #17) | sunfish-PM (next after #17) | `icm/_state/handoffs/property-assets-stage06-handoff.md` | First-slice scope: `Asset` entity + `AssetClass` enum + `AssetLifecycleEvent` append-only log + CRUD + tests + kitchen-sink seed + apps/docs page. Vehicle subtype + Trip events + AssetConditionAssessment integration + OCR ingest deferred to follow-up hand-offs. ~4–6 hours. No Proposed-ADR dependencies (ADRs 0008, 0015, 0049 all Accepted). **Blocked on workstream #17 (Properties first-slice) merging — Asset.Property FK target.** |
| 25 | Inspections domain (cluster module) | `design-in-flight` | research | `icm/00_intake/output/property-inspections-intake-2026-04-28.md` | Annual + move-in/out triggers + AssetConditionAssessment children. iOS walkthrough wizard. |
| 26 | Receipts domain (cluster module) | `ready-to-build` (first-slice; gated on #17 + #24) | sunfish-PM (after #17, #24) | `icm/_state/handoffs/property-receipts-stage06-handoff.md` | First-slice scope: kernel-only persistence — `Receipt` entity + `ReceiptCategory` enum + `ReceiptLineItem` + `ReceiptSource` + `ReconciliationStatus` + CRUD + audit emission + tests + kitchen-sink seed + apps/docs page. iOS Vision OCR + email-attachment ingestion + Money struct migration + typed FK conversions deferred to follow-up hand-offs. ~3–5 hours. No Proposed-ADR dependencies (uses `decimal Amount` + `string CurrencyCode` placeholders until ADR 0051 acceptance; opaque-string FKs to Vendor/WorkOrder/Payment until those modules ship). **Blocked on workstreams #17 (Properties) + #24 (Assets) first-slices merging — Receipt FK targets.** |
| 27 | Leases domain (cluster module) | `design-in-flight` | research | `icm/00_intake/output/property-leases-intake-2026-04-28.md` | LeaseDocumentVersion versioning + signature binding; lifecycle events; Phase 3 portal explicitly out of scope. |
| 28 | Public Listings surface (cluster module) | `design-in-flight` | research | `icm/00_intake/output/property-public-listings-intake-2026-04-28.md` | New "Public listing surface" ADR + ADR 0043 addendum. Bridge SSR; SEO; CAPTCHA-gated inquiry form. |
| 29 | Owner Web Cockpit (cluster module) | `design-in-flight` | research | `icm/00_intake/output/property-owner-cockpit-intake-2026-04-28.md` | Anchor + Bridge cockpit views consuming all cluster modules. Multi-actor permissions matrix resolves cluster OQ1. |
| 30 | Mesh VPN / Cross-Network Transport (adjacent, not in cluster) | `design-in-flight` | research | `icm/00_intake/output/mesh-vpn-cross-network-transport-intake-2026-04-28.md` | New "Three-tier peer transport" ADR. Headscale + WireGuard recommendation. Provides paper §6.1 Tier 2 (mesh VPN) — currently only in paper, no ADR/code. Phase 2.3 enabler for iOS direct-to-Anchor sync. |

---

## How to use this ledger

### research session (this session)

- Updates rows when designs progress, freeze, or unfreeze.
- Authoritative author of `design-in-flight` ↔ `ready-to-build` transitions.
- Writes the hand-off file in `handoffs/` when transitioning a row to `ready-to-build`.
- Updates the "Last updated" timestamp + signs off ("research session — YYYY-MM-DD").

### sunfish-PM session

- **Before any code change beyond a one-line fix:** verify the relevant row says `ready-to-build` AND a hand-off file exists in `handoffs/`.
- During implementation: update row to `building` (with PR link if available).
- After completion: update row to `built` with the merged PR / commit reference.
- If unsure whether work is wanted: write a memory note asking the research session, do not proceed.

### book-writing session

- When a book chapter cross-references a Sunfish ADR or intake, check this ledger for that workstream's status.
- If `design-in-flight`: mark the cross-reference as draft / pending in the manuscript.
- If `built`: cross-reference is stable.

### Maintenance

- Stale `built` rows can be pruned in a periodic housekeeping pass (>7 days old, no follow-ups).
- Rows superseded by a different workstream link the replacement; keep the row at `superseded` for one cycle so anyone mid-work sees the redirect.

---

## Last updated

**2026-04-28** — research session: ledger created (coordination protocol bootstrap)
**2026-04-28 (later)** — research session: workstreams #3 (PR #190) and #4 (PR #191) flipped to `built` (merged); workstream #2 hand-off branch strategy updated to "follow-up PR from main" since PR #190 merged without the retrofit.
**2026-04-28 (later still)** — sunfish-PM session: workstream #14 flipped to `built` — PR #196 merged 14:35Z. ADR 0013 audit finding C-1 in `CONSOLIDATED-HUMAN-REVIEW.md` § 1 is resolved.
**2026-04-28 (later still still)** — sunfish-PM session: workstream #2 flipped to `built` — PR #198 merged 16:30Z. Workstream #3 row updated to note Tier 1 drift resolved; only Tier 2 remains blocked on workstream #1's M2.
**2026-04-28 (evening)** — research session: workstreams #16–#30 added — property-operations vertical cluster (14 per-domain intakes + INDEX) + adjacent mesh-VPN transport intake drafted from multi-turn conversation. All `design-in-flight`. Awaiting user review of intake cluster. Workstream #20 note updated to reflect #14 enforcement gate now built (unblocked).
**2026-04-28 (later evening)** — sunfish-PM session: workstream #15 Phase 1 inventory shipped (PR #202 merged 19:40Z); row flipped to `building` pending research-session review of the 4 open questions. Component-library subagent dispatch for style-audit Packets 1A/1B/1C was a **no-op**: all three packets had already been delivered by PRs #151 (themes 3-4), #156 (themes 1-2), and commit `809f060` (Phase 1+2+3 CSS polish wave) — see `icm/07_review/output/style-audits/TIER-4-RE-AUDIT.md` for the post-fix audit confirming Phase 1 is at 86.5% completion (45/52 P0 resolved, 5 partial, 2 unresolved). No new rows added to the ledger; this dispatch is recorded here for traceability. **Lesson:** check `TIER-4-RE-AUDIT.md` (or any post-fix re-audit document) before dispatching subagents against a synthesis older than the most-recent style-remediation PR.
**2026-04-28 (night)** — research session: drafted ADRs 0051 (Payments — PR #203), 0052 (Bidirectional Messaging Substrate — PR #201), 0053 (Work-Order Domain Model — PR #205). All three Proposed; awaiting council review + user acceptance. Posted research-session sign-off on PR #202 review (comment 4338508191) accepting workstream #15 Phase 1 inventory; 4 OQs answered; Phase 2/3 unblocked. Workstream #15 row updated to reflect acceptance. **Workstream #17 (Properties domain) flipped to `ready-to-build` with first-slice hand-off** at `icm/_state/handoffs/property-properties-stage06-handoff.md` so sunfish-PM has parallel work alongside #15 Phase 2/3.
**2026-04-28 (late night)** — research session: codified **fallback work order** for sunfish-PM in CLAUDE.md § Multi-Session Coordination + `icm/_state/session-startup-prompts/sunfish-pm.md`. When priority queue is dry, sunfish-PM falls through 6-rung ladder (dependabot → build hygiene → style-audit P0 → coverage gap-fill → docs → sleep) instead of idling. **Workstream #24 (Assets domain) flipped to `ready-to-build` with first-slice hand-off** at `icm/_state/handoffs/property-assets-stage06-handoff.md` (gated on #17 first-slice merging — Asset.Property FK target). Queue depth now: #15 Phase 2/3 (in flight), #17 Properties first-slice (ready), #24 Assets first-slice (ready, blocked on #17).
**2026-04-28 (later late night)** — research session: **Workstream #26 (Receipts domain) flipped to `ready-to-build` with first-slice hand-off** at `icm/_state/handoffs/property-receipts-stage06-handoff.md`. Kernel-only persistence first-slice; iOS Vision OCR + email-attachment ingestion + Money struct migration + typed FK conversions deferred to follow-up hand-offs. Gated on workstreams #17 + #24 (Properties + Assets first-slices) merging — Receipt FK targets. Queue depth now: 4 (#15 in flight, #17 ready, #24 gated on #17, #26 gated on #17+#24). Per CLAUDE.md research-session commitment, queue depth is healthy.
**2026-04-28 (night, sunfish-PM follow-up)** — sunfish-PM session: workstream **#17 flipped to `built`** — PR #210 merged 21:05Z. `Sunfish.Blocks.Properties` package shipped end-to-end (entity + repo + entity-module + 25 tests + docs). Hand-off OQ #2 (Money type) + OQ #3 (kitchen-sink seed pattern) flagged as deferred — see PR #210 description. **Workstream #24 (Assets first-slice) HALTED** before Phase 1 — `packages/blocks-assets/` already exists as a UI-only catalog package (`AssetCatalogBlock.razor`); the #24 hand-off treats the csproj as **NEW** without acknowledging the collision. See project memory `project_workstream_24_assets_handoff_collision` for three options + sunfish-PM recommendation. **Possibly the same oversight applies to Receipts/Inspections/Leases hand-offs** — research should verify each domain module's package name is genuinely available before flipping to `ready-to-build`. Queue depth now: #15 Phase 2/3 (unblocked, deferred to focused future session), #24 (halted on collision), #26 (gated on #24).
