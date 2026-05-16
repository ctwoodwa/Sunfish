# Master Plan — Sunfish + The Inverted Stack

**Last updated:** 2026-05-16 (W#60 ERPNext pivot impact on G-1; velocity baseline refresh)
**Maintained by:** research session (cross-project PM)
**Cadence:** updated when goal definition, milestone, or velocity baseline materially changes; not on every PR. The dynamic state lives in `active-workstreams.md`; this file is the stable "where we're going."

---

## The three goals

This effort has **three concurrent goals**, ranked by user priority:

| # | Goal | Repo | Definition of done | Strategic role |
|---|---|---|---|---|
| **G-1** | **Business MVP** | Sunfish | BDFL's property management business runs on Sunfish + ERPNext (composition model per W#60 UPF plan approved 2026-05-11). ERPNext (self-hosted, GPLv3) is the accounting + property engine; Sunfish is the local-first sync + offline + React UI + tenant comms layer. 6 tenants (4 LLCs + holding co + mgmt co), spouse co-ownership active, full monthly cycle (rent → invoice → bank reconciliation → statements → vendor payments) running in production. Accountant peer node (Headscale) and CPA read-only access via Bridge. | **Primary.** Proves the local-first paradigm with a real commercial workload. |
| **G-2** | **Component library** | Sunfish | Dual-namespace components (Rich vs MVP per ADR 0041) shipped with parity tests passing; style audit synthesis findings (248 findings, 10 themes — `project_style_audit_synthesis_2026_04`) remediated; compat-package expansion (Telerik / Syncfusion / DevExpress / Infragistics — `project_compat_expansion_workstream`) wave landed. | Secondary. Funds + de-risks future commercial customers; book Part IV implementation playbooks pull from this. |
| **G-3** | **Book — *The Inverted Stack*** | the-inverted-stack | All 20 chapters + preface + epilogue + 4 appendices through `icm/approved` → `icm/assembled`; audiobook pipeline through ACX submission target; published. | Secondary but parallel. Drives architectural rigor (the book commits Sunfish to specific package contracts via `inverted-stack-package-roadmap.md`); commercial-positioning of Sunfish per `project_sunfish_reference_implementation`. |

---

## G-1: Business MVP — current state + path to done

### Phase 1 (foundational primitives) — ~95% done

Per `project_business_mvp_phase_1_progress` memory, G1-G6 substrate is **all merged**. Remaining:

- **G6 host integration** — wire `RecoveryCompleted → SqlCipher rekey + persist to kernel-audit` in Anchor. Stage 06. Not yet started. Buildable on Win + Mac (per ADR 0044 amendment 2026-04-28 + ADR 0048 multi-backend MAUI; user's Mac is now updated to latest OS + Xcode).
- **G6 Razor UI** — `TrusteeSetup / InitiateRecovery / ApproveRecoveryRequest / PaperKey` pages. Stage 06. Not yet started. Same multi-platform build envelope.
- **G7 conformance baseline scan** — unblocked on substrate; gated on G6 host integration.

**Phase 1 completion estimate:** ~3-5 PRs remaining; ~1 week of focused sunfish-PM work.

### Phase 2 (commercial scope) — ~15% done

**⚠ W#60 pivot (2026-05-11) materially changes workstreams A–G.** ERPNext now owns accounting, invoicing, bank reconciliation, and statements. Sunfish wraps it. Original workstream scopes below have been annotated with pivot impact.

| WS | Title | Status | Pivot impact | Estimated PRs |
|---|---|---|---|---|
| **A** | Anchor team-context binding for 6 entities | Not started; ERPNext manages multi-entity natively; Sunfish needs team-switch UX wiring to ERPNext entity IDs (ADR 0032) | **Scope reduced** — ERPNext carries the data model; Sunfish adds the UI + team-context selection | 2-3 |
| **B** | Wave Accounting migration | **SUPERSEDED** — W#60 Phase 1 PASS (CO entered data directly into ERPNext 2026-05-12); no Wave import tool needed | — | 0 |
| **C** | Bank ingest + reconciliation | **LIKELY SUPERSEDED at Sunfish layer** — ERPNext bank reconciliation + Plaid integration operate at the ERPNext layer (via Frappe connectors). Sunfish React UI displays reconciled data from ERPNext API. Remaining Sunfish scope: offline cache of reconciled statement data in the Tauri SQLite store (W#60 P3). | **Decision needed:** Does CO want a dedicated Sunfish Plaid integration, or use ERPNext's native bank feed? XO recommends deferring until W#60 P3 ships and CO sees the offline experience. | 0-2 |
| **D** | Payments (Stripe + ADR 0051) | **LIKELY SUPERSEDED at Sunfish layer** — ERPNext has native Stripe integration (Frappe Payment Gateway). Stripe webhooks go to ERPNext directly; Sunfish React UI reads payment status from ERPNext API. No separate Sunfish Bridge Stripe integration needed. | **Decision needed:** ADR 0051 "outbound-payment event forwarding" may be a no-op given ERPNext native handling. XO recommends treating WS-D as done via ERPNext unless CO identifies a gap. | 0-1 |
| **E** | Outbound messaging (SendGrid + ADR 0052) | Not started; **blocks on ADR 0052 drafting**; scope unchanged (ERPNext has no chat/messaging) | **Unchanged** — `blocks-crew-comms` is Sunfish's primary differentiator over ERPNext | 4-6 |
| **F** | Audit trail (kernel-audit + Tier 1 retrofit) | **scaffold merged 2026-04-28**; Tier 1 retrofit ready-to-build | Unchanged | 1-2 |
| **G** | Statement template + monthly job | **SUPERSEDED** — ERPNext generates invoices/statements natively; Sunfish role is React UI display + offline cache | — | 0 |
| **H** | Spouse co-ownership + recovery (ADR 0046 primitives) | Not started; gated on `Foundation.Recovery` scaffolding | **Unchanged** — cryptographic recovery is Sunfish-layer, not ERPNext | 4-6 |

**W#60 phase workstreams (new, 2026-05-11+):**

| WS | Title | Status | Estimated PRs |
|---|---|---|---|
| **W#60 P2** | React UI skin (6 screens + @sunfish/ui-react) | **BUILT 2026-05-13** (PRs #731+#732+#751+#752+#757+#758) | done |
| **W#60 P3** | Tauri v2 offline shell (Surface Pro) | **Ready-to-build** — gated on ADR 0086 Accepted (PR #737 Proposed) | 3-4 |
| **W#60 P4** | Collaboration (accountant peer + CPA + tenant portal + bank CSV) | **Hand-off authored 2026-05-16** — gated on P3 PASS (CO Surface Pro acceptance) | 5-6 |
| **W#60 P5** | @sunfish/contracts + rent roll + P&L + Schedule-E | **Hand-off authored 2026-05-16** — PR 1 (@sunfish/contracts) immediately buildable; PR 2+ gated on P2 React UI (done) | 4-5 |

**Phase 2 completion estimate (revised):** ~15-30 PRs remaining (if WS-C and WS-D are confirmed superseded by ERPNext native integrations, down from 27-41). CO decision on C+D needed after W#60 P3 ships.

### G-1 done conditions (concrete, revised 2026-05-16)

**Phase 1:**
- [x] `Foundation.Recovery` package split built (W#15 + W#32 both `built`)
- [ ] G6 host integration + Razor UI shipped (W#63 hand-off authored 2026-05-16 — immediately buildable)
- [ ] G7 conformance baseline scan committed under `icm/01_discovery/output/`

**ERPNext layer (W#60):**
- [x] W#60 P1 PASS — ERPNext self-hosted on CO machine; lease + rent payment + ledger confirmed (2026-05-12)
- [x] W#60 P2 BUILT — React UI (6 screens + @sunfish/ui-react) on main (2026-05-13)
- [ ] W#60 P3 PASS — CO works offline on Surface Pro 30 min; reconnects; changes appear in ERPNext (gates P4)
- [ ] W#60 P4 PASS — Accountant peer node syncing; CPA can view year-end data; tenant portal works via magic-link
- [ ] W#60 P5 PASS — @sunfish/contracts published; rent roll + P&L + Schedule-E accessible

**Phase 2 Sunfish-layer workstreams:**
- [x] ADR 0051 (Payments) Accepted 2026-04-28
- [x] ADR 0052 (Outbound messaging) Accepted 2026-04-28
- [ ] WS-E built (**W#20 phases 4-9 ARE WS-E** — providers-postmark adapter + inbound webhook + audit + docs; hand-off at `icm/_state/handoffs/property-messaging-substrate-stage06-handoff.md`; COB to continue from Phase 4)
- [ ] WS-D: **CO decision needed** — ERPNext native Stripe may make this a no-op; defer until W#60 P3 ships
- [ ] WS-A built (Anchor team-context bound to ERPNext entities for 6-entity setup)
- [ ] WS-C: **CO decision needed** — ERPNext native bank feed vs. dedicated Plaid integration; defer until W#60 P3
- [ ] WS-H built (spouse co-ownership + recovery)

**Business validation:**
- [ ] BDFL processes first rent collection cycle end-to-end (React UI → ERPNext → bank statement)
- [ ] BDFL sends first tenant communication through blocks-crew-comms
- [ ] Accountant performs bank reconciliation from their own Anchor node
- [ ] CPA accesses year-end Schedule-E data via Bridge read-only session
- [ ] Spouse logs into her own Anchor install with co-owner capabilities
- [ ] Recovery flow exercised end-to-end (real trustees, real grace period)
- [ ] Annual cycle dry-run: tax-prep export matches accountant's records

---

## G-2: Component library — current state + path to done

### Active workstreams (from existing memory)

- **Style audit remediation** — 248 findings, 10 systemic themes; 3-phase remediation in flight per `project_style_audit_synthesis_2026_04`. Synthesis at `icm/07_review/output/style-audits/SYNTHESIS.md`.
- **Compat package expansion** — Telerik (existing) + Syncfusion + DevExpress + Infragistics. 4 intake decisions pending per `project_compat_expansion_workstream`. Queued behind current style-parity work.
- **Dual-namespace components** — Rich vs MVP per ADR 0041. SunfishGantt / Scheduler / Spreadsheet / PdfViewer. Both folders intentional per memory.
- **Adapter parity** — Blazor ↔ React per ADR 0014. Parity matrix maintained; CI gate planned for P6 (per ADR 0014 audit, this is partially honor-system today).

### G-2 done conditions (synthesized; needs user confirmation)

- [ ] Style audit remediation Phase 3 closed
- [ ] Compat-package expansion: 4 vendors complete (Telerik already shipped; Syncfusion / DevExpress / Infragistics to add)
- [ ] Adapter parity matrix at 100% across declared components; CI gate live
- [ ] Web Components track (ADR 0017) — M5 fan-out across 3 tracks complete (or formally deferred per the ADR 0017 audit recommendation)
- [ ] kitchen-sink demo covers every shipped component in every adapter

**G-2 completion estimate:** unclear without explicit done definition; placeholder ~30-50 PRs spread across multiple component waves.

---

## G-3: Book — current state + path to done

### Chapter inventory (file-system count, 2026-04-28)

| Part | Chapters in `book-structure.md` | Files exist | Likely status |
|---|---|---|---|
| Front matter | preface | (preface dir) | Drafting / late |
| Part I — Thesis & Pain | Ch01-04 | 4/4 .md files | All 4 issues at `icm/outline` per gh issue list |
| Part II — Council Reads the Paper | Ch05-09 | 5/5 .md files | Files present; ICM stages not surfaced via open issues — likely past outline |
| Part II — Council Reads the Paper | Ch05-10 | 5/6 .md files; **Ch10 (Synthesis) scheduled-pending per user 2026-04-28** | Synthesis closer; depends on Ch05-09 maturity |
| Part III — Reference Architecture | Ch11-16 | 5/6 .md files; **Ch16 (Persistence Beyond the Node) scheduled-pending per user 2026-04-28** | Ch15 most active (recent #46/#47 iterations); Ch16 consolidated from original Storage/Backup + Relay/Federation |
| Part IV — Implementation Playbooks | Ch17-20 | 4/4 .md files | Files present |
| Part V — Operational Concerns | Ch21+ | Ch21 only | Earliest part by file presence |
| Appendices | A-D | (appendices dir) | Unknown |
| Epilogue | (epilogue dir) | Present | Unknown |
| Audiobook | — | `build/` pipeline | Active recent investment (kokoro/higgs/ACX) |

**Total chapters in scope: 22** (Ch01-21 + the renumbered Part II Ch10).

### G-3 open questions for user

- **Part V scope** — only Ch21 file present; is the rest of Part V planned?
- **Audiobook publishing target** — ACX submission counts as "published" for MVP, or wait for paperback?
- **Final-pass word-count trimming policy** (per user 2026-04-28): include all content first; final pre-publish pass strips word-count if needed.

### G-3 done conditions (synthesized)

- [ ] All chapters at `icm/approved` per book CLAUDE.md ICM pipeline
- [ ] All chapters at `icm/assembled` (added to `ASSEMBLY.md`)
- [ ] Foreword written + secured
- [ ] Final manuscript pandoc-assembled
- [ ] Audiobook ACX submission accepted

**G-3 completion estimate:** ~3-4 months at current velocity (see velocity baseline below).

---

## Velocity baseline (updated 2026-05-16)

### Sunfish PR throughput

**2026-04-28 baseline:** ~17 PRs/day (3-day average; bursty).

**2026-05-16 refresh:** W#23 + W#29 + W#44–W#62 cluster shipped across 2026-05-04–2026-05-16. Approximate total: 60–80 PRs merged in 18 days → **~4-5 substantive PRs/day sustained**. This is lower than the April burst but more consistent — property-ops cluster (19 workstreams × 3-7 PRs each) plus iOS substrate drove the bulk of the volume.

Velocity is **bursty** — active research+build days hit 8-12 PRs; quiet days hit 0-2. Sustainable pace: **4-6 substantive PRs/day** when actively working.

### Book throughput

26 book-update-loop iterations since 2026-04-15 (13 days) = **~2 chapter-stage-advancements per day**. With 8 ICM stages per chapter and 22 chapters × 8 stages = ~176 stage transitions to a finished book; subtract chapters already past outline (~14 chapters × ~3 stages average past = ~42 done) and add 2 fresh chapters (Ch10, Ch16) starting at outline = **~134 stage transitions + 16 (two new chapters from outline) = ~150 stage transitions remaining at ~2/day = ~75 working days = ~3-4 months**.

### Token-budget reality check

User on Pro Max ($200/mo). Recent overnight automation run consumed ~830K tokens total across 13 subagents + orchestration. That's a **roughly half-day burn at full intensity**. Repeating that pattern daily would consume the budget faster than necessary; **~2-3 such bursts per week** is sustainable while leaving headroom for normal work.

---

## Estimated MVP date — user-business-MVP (G-1)

**Revised 2026-05-16** (post-W#60 pivot):

| Track | Remaining work | Time estimate |
|---|---|---|
| Phase 1 G6 (Recovery host integration + UI) | **W#63 hand-off authored 2026-05-16; immediately buildable**; 3 PRs | 1-2 weeks |
| W#60 P3 (Tauri offline shell) | gated on ADR 0086 Accepted (CO action) | 1-2 weeks after CO flips |
| W#60 P4 (Collaboration — accountant peer + CPA + tenant) | gated on P3 PASS | 2-3 weeks after P3 |
| W#60 P5 (@sunfish/contracts + reporting) | PR 1 buildable now; PR 2+ gated on P2 (done) | 1-2 weeks |
| Phase 2 Sunfish-layer (WS-A, C, D, E, H) | ~18-28 PRs; WS-E = W#20 phases 4-9 (hand-off exists); WS-H gated on W#63 + W#A | 3-5 weeks |
| ~~ADR 0051 + 0052 drafting~~ | ~~XO research~~ | ✅ Both Accepted 2026-04-28 |
| Business validation cycle | BDFL-time-bound; first real rent-collection cycle | 2-4 weeks real-world |

**Estimated G-1 MVP-ready: 8-14 weeks from now** (mid-July to late-August 2026), assuming:
- ADR 0086 accepted + CO confirms Surface Pro P3 test within 1-2 weeks
- ADR 0052 drafted within the next XO cycle
- sunfish-PM session runs ~3-5 days/week
- No major blocking surprises in Tauri/Headscale peer connectivity

**The ERPNext pivot accelerated the accounting/invoicing/reconciliation track** (B + G superseded; C + D scopes halved), but adds 3-5 weeks for the W#60 P3/P4 collaboration layer that didn't exist in the original Phase 2 scope. Net effect: roughly timeline-neutral but with a much lower implementation risk profile.

---

## Update protocol

This file is updated when:
- A goal's done conditions change (user decision)
- A major workstream is added or removed
- Velocity baseline materially shifts (e.g., new automation tier, BDFL availability changes)
- Estimated MVP date changes by more than 1 week

**Day-to-day status lives in `active-workstreams.md`. This file does not duplicate that.**

The user receives an **executive summary** on demand, synthesized from this file + active-workstreams + recent gh data — see the "Status format" section in `CLAUDE.md` § Multi-Session Coordination.

---

## Reference docs

- `icm/_state/active-workstreams.md` — dynamic workstream ledger
- `icm/_state/handoffs/` — research-to-sunfish-PM hand-off specs
- `icm/00_intake/output/phase-2-commercial-mvp-intake-2026-04-27.md` — Phase 2 scope
- `icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md` — Phase 1 plan
- `icm/07_review/output/adr-audits/CONSOLIDATED-HUMAN-REVIEW.md` — pending ADR amendment decisions
- `docs/specifications/inverted-stack-package-roadmap.md` — Sunfish-side roadmap (mirror of book-side authoritative)
- `/Users/christopherwood/Projects/the-inverted-stack/inverted-stack-book-plan.md` — book writing plan
- `/Users/christopherwood/Projects/the-inverted-stack/book-structure.md` — chapter targets
