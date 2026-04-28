# Master Plan — Sunfish + The Inverted Stack

**Last updated:** 2026-04-28
**Maintained by:** research session (cross-project PM)
**Cadence:** updated when goal definition, milestone, or velocity baseline materially changes; not on every PR. The dynamic state lives in `active-workstreams.md`; this file is the stable "where we're going."

---

## The three goals

This effort has **three concurrent goals**, ranked by user priority:

| # | Goal | Repo | Definition of done | Strategic role |
|---|---|---|---|---|
| **G-1** | **Business MVP** | Sunfish | BDFL's property management business runs entirely on Sunfish, replacing Wave Accounting + Rentler + bank shared-access. 6 tenants (4 LLCs + holding co + mgmt co), spouse co-ownership active, full monthly cycle (rent → invoice → bank reconciliation → statements → vendor payments) running in production for the BDFL's actual operation. | **Primary.** Proves the local-first paradigm with a real commercial workload. |
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

### Phase 2 (commercial scope) — ~10% done

Per `phase-2-commercial-mvp-intake-2026-04-27.md`, 8 workstreams (A-H):

| WS | Title | Status | Estimated PRs |
|---|---|---|---|
| **A** | Anchor multi-team setup for 6 entities (ADR 0032) | Not started; reuses existing TeamContext | 3-5 |
| **B** | Wave Accounting one-shot migration tool | Not started | 4-7 |
| **C** | Bank ingest (`providers-plaid` + reconciliation) | Not started; **blocks on ADR 0013 enforcement gate (C-4)** | 6-10 |
| **D** | Payments (`providers-stripe` + ADR 0051 contract package) | Not started; **blocks on ADR 0051 drafting** | 8-12 |
| **E** | Outbound messaging (`providers-sendgrid` + ADR 0052) | Not started; **blocks on ADR 0052 drafting** | 4-6 |
| **F** | Audit trail (kernel-audit + Tier 1 retrofit) | **scaffold merged 2026-04-28**; Tier 1 retrofit ready-to-build | 1-2 |
| **G** | Statement template + monthly job | Not started; deferred to ADR 0053 (background jobs); use Quartz shim for now | 3-5 |
| **H** | Spouse co-ownership + recovery (config on ADR 0046 primitives) | Not started; gated on `Foundation.Recovery` scaffolding | 4-6 |

**Phase 2 completion estimate:** ~33-53 PRs remaining.

### G-1 done conditions (concrete)

- [ ] Phase 1 G6 host integration + Razor UI shipped (Anchor on Windows VM)
- [ ] Phase 1 G7 conformance baseline scan committed under `icm/01_discovery/output/`
- [ ] Phase 2 ADR 0051 (Payments) accepted
- [ ] Phase 2 ADR 0052 (Outbound messaging) accepted
- [ ] All 8 Phase 2 workstreams (A-H) shipped
- [ ] BDFL imports first month of real Wave data; reconciliation matches
- [ ] BDFL processes first rent collection through `blocks-rent-collection`
- [ ] BDFL sends first statement via `providers-sendgrid` outbound
- [ ] Spouse logs in to her own Anchor install with co-owner capabilities
- [ ] Recovery flow exercised end-to-end (real trustees, real grace period)
- [ ] Annual cycle dry-run: tax-prep export to advisor via outbound

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

## Velocity baseline (calculated 2026-04-28)

### Sunfish PR throughput

| Date | PRs merged |
|---|---|
| 2026-04-28 | 11 (so far) |
| 2026-04-27 | 22 |
| 2026-04-26 | 17 |
| 3-day average | ~17 PRs/day |

This is **bursty** — a high day driven by overnight subagent dispatches + parallel sessions. Sustainable pace is probably **5-10 substantive PRs/day** when actively working; **0-3 PRs/day** on light days.

### Book throughput

26 book-update-loop iterations since 2026-04-15 (13 days) = **~2 chapter-stage-advancements per day**. With 8 ICM stages per chapter and 22 chapters × 8 stages = ~176 stage transitions to a finished book; subtract chapters already past outline (~14 chapters × ~3 stages average past = ~42 done) and add 2 fresh chapters (Ch10, Ch16) starting at outline = **~134 stage transitions + 16 (two new chapters from outline) = ~150 stage transitions remaining at ~2/day = ~75 working days = ~3-4 months**.

### Token-budget reality check

User on Pro Max ($200/mo). Recent overnight automation run consumed ~830K tokens total across 13 subagents + orchestration. That's a **roughly half-day burn at full intensity**. Repeating that pattern daily would consume the budget faster than necessary; **~2-3 such bursts per week** is sustainable while leaving headroom for normal work.

---

## Estimated MVP date — user-business-MVP (G-1)

Based on:
- Phase 1 remaining: ~3-5 PRs (~1 week)
- Phase 2 remaining: ~33-53 PRs at 5-10 PRs/day = ~5-10 working days
- Plus: ADR 0051 + 0052 drafting (research session, ~2 sessions)
- Plus: real-world data migration + reconciliation testing (BDFL-time-bound)

**Estimated G-1 MVP-ready: 4-8 weeks** (early-to-mid June 2026), assuming:
- No major architectural surprises (likely; ADRs are mostly accepted)
- BDFL allocates time for migration testing + first-month parallel run
- sunfish-PM session runs ~3-5 days/week

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
