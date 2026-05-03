# ADR Audit — Consolidated Human Review

**Generated:** 2026-04-28 (overnight automation run, all 13 subagents complete)
**Scope:** 8 Tier-1 full UPF audits + 5 Tier-2 anti-pattern sweeps (15 ADRs) = 23 ADRs covered
**This file:** action-oriented punch list. Individual audit files in this directory have full reasoning.

---

## TL;DR

- **No ADRs failed.** All 8 Tier-1 ADRs grade B- or better; one (0043 — threat model) earns A-. Most are solid position/decision documents that need form-tightening rather than substantive rework.
- **4 critical-severity findings** require ADR amendments; **3 of 4 are documentation/code drift** that future contributors will trip over if not fixed.
- **3 Tier-2 ADRs need amendment** (0009 FeatureManagement, 0017 Web Components/Lit, 0029 Federation/Gossip); the rest are annotation-only.
- **Three cross-ADR meta-patterns** worth deciding policy on (see §Cross-ADR meta-patterns).
- **Phase 1 (forward-looking template + 5-min self-audit)** has shipped via PR #193 with auto-merge; the template now enforces these checks at authoring time so future ADRs don't re-incur these gaps.

---

## §1 — Critical-severity findings (review and decide first)

These are not stylistic — each is **ADR-vs-reality drift** or **enforcement-gap-with-real-blast-radius** that gets worse if ignored.

### C-1 — ADR 0028 (CRDT Engine Selection): says "Adopt Loro" but YDotNet shipped

The ADR's Status header still reads "Accepted (2026-04-22) — Adopt Option B (Loro)" while production runs YDotNet (Option A) per `packages/kernel-crdt/SPIKE-OUTCOME.md`. The substrate-impl insulation pattern (the load-bearing `ICrdtDocument` contract that ADR 0049 cites as precedent) **was validated** by exactly this Loro→YDotNet substitution — but the ADR itself was not amended after the spike. Fresh contributor reading ADR 0028 today would assume Loro is in production.

**Recommended amendment:** Update Status block + Decision section to "Superseded-in-implementation per SPIKE-OUTCOME.md (2026-04-22); Loro deferred, YDotNet shipped." Keep Loro as the long-term target per the spike's revisit triggers. (See `0028-upf-audit.md` for full amendment list.)

### C-2 — ADR 0046 (Key-Loss Recovery): package-tier drift

ADR 0046 (and downstream ADR 0049) name `Sunfish.Foundation.Recovery` / `foundation-recovery`, but the shipped Phase-1 code lives at `packages/kernel-security/Recovery/` (`Sunfish.Kernel.Security.Recovery` namespace). **No `foundation-recovery` package exists.** This is a kernel-tier-vs-foundation-tier mismatch on a security-critical primitive — the paper's tier model treats this distinction as load-bearing.

**Recommended amendment:** Either (a) add a "Package placement" section to ADR 0046 ratifying the kernel-security home **and patch ADR 0049's `Compatibility plan` + `Implementation checklist` references** to match, or (b) open an api-change-pipeline migration ticket to relocate the package. **Don't leave docs and code disagreeing.** (See `0046-upf-audit.md`.)

### C-3 — ADR 0008 (Foundation.MultiTenancy): adoption status now stale

`IMayHaveTenant` was added with **zero downstream consumers** (verified by the 2026-04-28 multi-tenancy convention intake). This is classic AP-15 (premature precision) and AP-20 (discovery amnesia — `TenantId.Default` sentinel pattern existed in code at the time but is unmentioned). The intake's amendment is now the corrective; ADR 0008 should reflect that it's actively under revision.

**Recommended amendment:** Add an "Adoption status (2026-04-28)" addendum citing the intake's findings (`IMayHaveTenant` zero adoption; `IMustHaveTenant` 7 records; `TenantId.Default` 16+ sites). Mark `IMayHaveTenant` `@deprecated-pending` in the API table. Cross-link to `tenant-id-sentinel-pattern-intake-2026-04-28.md`. (See `0008-upf-audit.md`.)

### C-4 — ADR 0013 (Foundation.Integrations / Provider-Neutrality): no mechanical enforcement

Provider-neutrality is declared as "reviewers reject violations in PRs" — i.e., socially enforced. Phase 2 commercial scope is about to start landing real provider adapters (Plaid for banking, Stripe for payments, SendGrid for outbound, possibly Twilio for SMS). At the next staffing change or attention lapse, "reviewers enforce" degrades silently. This is the **highest-urgency critical** because the failure mode lands as soon as the first `using Stripe;` slips into a `blocks-*` package.

**Recommended amendment:** Add an **Enforcement** section: Roslyn analyzer or `BannedSymbols.txt` rejecting vendor SDK namespaces in `blocks-*` and `foundation-*` (only `providers-*` / `Sunfish.Providers.*` may reference vendor SDKs); architecture-test asserting this at build time. Should land before the first `providers-*` package is scaffolded for Phase 2. (See `0013-upf-audit.md`.)

---

## §2 — Tier-2 needs-amendment findings (3 ADRs)

| ADR | Title | Issue | Severity | Recommended action |
|---|---|---|---|---|
| **0009** | Foundation.FeatureManagement | `NoOpEntitlementResolver` ships P1; bundle-backed resolver lands P2. Silent fail-open/fail-closed risk if Bridge wires `IFeatureEvaluator` before P2 — features are silently granted or denied via `FeatureSpec.DefaultValue`. | major | Add explicit kill-switch / blocking criterion: P1 Bridge integration must run with all-features-open OR all-features-closed default until P2 lands. (See `anti-pattern-sweep-batch-2.md`.) |
| **0017** | Web Components / Lit (UI Technical Basis) | M0 proof-point covers only simple leaf components (`SunfishButton` / `SunfishSearchBox`); the four-contract shape may not fit complex generic-slot components like DataGrid. WC track (`ui-components-web`) has no kill/defer condition if M3 parity harness proves flaky. | major | Broaden M0 to include ≥1 complex component; add explicit kill condition for WC track if harness coverage stalls by named milestone. (See `anti-pattern-sweep-batch-3.md`.) |
| **0029** | Federation vs. Gossip Reconciliation | Dual-track decision (federation packages + new `kernel-sync` for intra-team gossip) rests on a tier-boundary structural assumption — yet `docs/specifications/sync-architecture.md` that would prove the boundary is a deferred checklist item, not a pre-decision artifact. AP-1 + AP-13 stacked. | major | Either produce the boundary spec before treating the decision as settled, OR explicitly relabel as open assumption with a replanning trigger if Wave 2 cross-tests reveal overlap. (See `anti-pattern-sweep-batch-4.md`.) |

---

## §3 — Cross-ADR meta-patterns (decide policy)

These patterns recur across multiple audits; worth deciding policy rather than fixing one-by-one.

### M-1 — Stale §Decision text after amendment

**Hits:** ADR 0028 (Loro→YDotNet, never updated), ADR 0044 (MacCatalyst relaxation, original §Decision still reads "Windows-only"). Both ADRs were amended honestly via appended sections — but the load-bearing §Decision text was never updated. Fresh readers of §Decision alone get stale answers (Cold Start partial-fail in both cases).

**Suggested policy:** When amending an ADR, the **§Decision section is the load-bearing surface and must be updated**, not just supplemented with an appendage. Add to ADR template (forward-looking) and as a meta-amendment recommendation across the existing corpus.

### M-2 — Missing UPF-form Assumptions tables

**Hits:** Virtually every Tier-1 ADR. ADRs use prose ("we assume...") rather than the structured `Assumption → VALIDATE BY → IMPACT IF WRONG` form. Catches latent risk only via close-reading. Already partially addressed by the new ADR template's pre-acceptance audit (PR #193) which mandates assumptions structure for new ADRs.

**Suggested policy:** Convert prose assumptions to UPF-form on the next amendment of each Tier-1 ADR. Don't rewrite all eight at once — fold into whatever amendment lands next (e.g., if 0028's Loro→YDotNet update happens, add the assumption table at the same time).

### M-3 — Missing FAILED conditions / kill triggers

**Hits:** Most ADRs have Revisit Triggers (good) but no Kill Triggers (bad). Without kill triggers, ADRs become zombie projects (AP-11) — a decision that no condition can void will eventually drift from reality without a forcing function for re-evaluation.

**Suggested policy:** Forward — already in the new template's pre-acceptance audit checklist. Backward — convert existing Revisit Triggers to dual-purpose (revisit-OR-kill) on next amendment cycle.

---

## §4 — Tier 1 grade table

| ADR | Title | Grade | Amendments | Critical? |
|---|---|---|---|---|
| 0043 | Unified Threat Model | **A-** | 2 maj / 5 min | — |
| 0004 | Post-Quantum Signature Migration | B | 2 maj / 5 min | — |
| 0021 | Document/Report Generation | B | 4 maj / 4 min | — |
| 0028 | CRDT Engine Selection | B | **1 crit** / 3 maj / 3 min | **C-1** |
| 0044 | Anchor Windows-only Phase 1 | B | 2 maj / 4 min | — |
| 0046 | Key-Loss Recovery Phase 1 | B | **1 crit** / 5 maj / 2 min | **C-2** |
| 0008 | Foundation.MultiTenancy | B- | **1 crit** / 3 maj / 3 min | **C-3** |
| 0013 | Foundation.Integrations | B- | **1 crit** / 3 maj / 3 min | **C-4** |

---

## §5 — Recommended sunfish-PM hand-off sequencing

Order by urgency × independence:

1. **C-4 (0013 mechanical enforcement)** — *Land first.* Phase 2 provider work hasn't started yet; this gate is highest-leverage to set up before any `providers-*` package is scaffolded. Concrete: Roslyn analyzer or `BannedSymbols.txt` + arch-test. Estimate: ~1 hour of hand-off-to-implementation.
2. **C-2 (0046 + 0049 package-name reconciliation)** — *Land second.* Two-ADR coordinated edit. If keeping the kernel-security home (likely): patch ADR 0046 + ADR 0049 to match shipped reality. ~30 min.
3. **C-1 (0028 Loro→YDotNet)** — *Land third.* Pure ADR text edit. ~15 min.
4. **C-3 (0008 adoption-status addendum)** — *Land fourth.* Vindicates the multi-tenancy convention intake (workstream #1 in `icm/_state/active-workstreams.md`). ~20 min.
5. **Tier-2 needs-amendment trio (0009, 0017, 0029)** — *Land in next batch.* Each needs a hand-off with specific amendment text. None block Phase 2.

Each item gets its own hand-off file in `icm/_state/handoffs/` per the multi-session coordination protocol from `CLAUDE.md`. **None require the research session to draft new design** — all are amendments to existing decisions.

---

## §6 — What did NOT need amendment

12 of the 15 Tier-2 ADRs grade `annotation-only` — small inline comments would tighten them but no decision is wrong. Specifically clean: 0001, 0002, 0003, 0007, 0011, 0012, 0014, 0027, 0031, 0032, 0042, 0048. The Tier-3 ADRs (0005, 0006, 0010, 0016, 0018, 0022–0025, 0030, 0033–0041, 0045, 0047, 0049) were skipped per triage and remain `review-on-demand`.

---

## §7 — Where to look for details

- Per-ADR Tier 1 audit: `icm/07_review/output/adr-audits/<NNNN>-upf-audit.md`
- Tier 2 sweep batches: `icm/07_review/output/adr-audits/anti-pattern-sweep-batch-{1..5}.md`
- Forward-looking template (Phase 1): merged via PR #193 — `docs/adrs/_template.md`
- Coordination protocol: `CLAUDE.md` § "Multi-Session Coordination" + `icm/_state/active-workstreams.md`

---

## §8 — Approve / next-action checklist for the user

When you read this, confirm or adjust:

- [ ] Approve C-1 amendment (0028 Loro→YDotNet) → research drafts hand-off
- [ ] Approve C-2 reconciliation approach (kernel-security home vs. migration) → research drafts hand-off
- [ ] Approve C-3 amendment (0008 adoption-status) → research drafts hand-off
- [ ] Approve C-4 enforcement gate (0013 analyzer/arch-test) → research drafts hand-off **(highest urgency — pre-Phase-2)**
- [ ] Approve M-1 meta-policy (amend §Decision, not just append)
- [ ] Approve Tier-2 amendments (0009 / 0017 / 0029) → research drafts 3 hand-offs
- [ ] Skip / defer any of the above and note why

Once approved items are flagged, research session writes hand-off files and updates the active-workstreams ledger; sunfish-PM executes per the standard pre-build checklist.
