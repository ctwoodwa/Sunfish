# Council Review — ADRs 0037 through 0042

**Date:** 2026-04-26
**Reviewer:** subagent council pass (5-seat adversarial)
**Scope:** ADR 0037 (CI platform), ADR 0038 (Branch Rulesets), ADR 0039 (Required-check minimalism), ADR 0040 (AI-first translation), ADR 0041 (Dual-namespace components), ADR 0042 (Subagent-driven development)
**Method:** Each ADR read end-to-end; 5 council seats independently raised concerns; each seat scored PUBLISH / POLISH / REVISE / REWORK; aggregated into action items + proposed amendment text.
**Charter:** Default 5-seat council (no `kleppmann_council_review.md` charter found in repo).
**Status:** read-only review — no ADR edits in this PR. Maintainer decides which proposed amendments to accept.

---

## Council seats

1. **Technical Correctness** — Are the technical claims accurate? Edge cases? Race conditions? Incorrect citations?
2. **Security** — Threat-model coverage. Privilege escalation paths. Secret handling. Account-compromise scenarios.
3. **Operations / Enterprise** — Maintainability. Runtime behavior under failure. SLO impact. Multi-maintainer / scale-out.
4. **Product / Commercial** — Market positioning. Competitor signaling. Future-customer optionality preserved.
5. **End-User / Practitioner** — Developer experience. Discoverability. Onboarding cost.

**Verdict scale:**
- **PUBLISH** — ADR is sound; seat has no blocking concerns.
- **POLISH** — Sound but small clarifications would harden it; not blocking.
- **REVISE** — Material gap; the ADR is mostly right but a section needs rewriting.
- **REWORK** — Foundational concern; the ADR's framing or decision needs to be re-examined.

---

## Executive summary

| ADR | Verdict (worst seat) | Distribution | Top concern |
|---|---|---|---|
| 0037 — CI platform | **POLISH** | 4×PUBLISH, 1×POLISH (Product) | Cost/optionality calculus when repo flips private under LLC governance is asymmetrically discussed; iOS/MacCatalyst gap is named but not budgeted. |
| 0038 — Branch Rulesets | **REVISE** | 1×REVISE (Security), 4×PUBLISH | `bypass_actors` audit-trail gap; no rule for behavior when maintainer's GitHub account is compromised; Settings-UI drift is "social mitigation" only. |
| 0039 — Required-check minimalism | **REVISE** | 1×REVISE (Operations), 1×POLISH (Security), 3×PUBLISH | Multi-maintainer flip-back path is named as a Revisit trigger but not concretely scoped; "session-end review" mitigation depends on a single-human habit that doesn't survive the second contributor. |
| 0040 — AI translation 3-stage gate | **REVISE** | 2×REVISE (Product, Practitioner), 1×POLISH (Security), 2×PUBLISH | Liability for AI-translated strings in a product surface is unaddressed; the "marketing/legal/critical errors deferred" carve-out is the largest commercial risk and the ADR explicitly leaves it open. Threshold heuristics (30% drift; 0.7 cosine) are unjustified. |
| 0041 — Dual-namespace components | **POLISH** | 1×POLISH (Operations), 1×POLISH (Practitioner), 3×PUBLISH | Maintenance tax (bug-fix-touches-two-files) is acknowledged but not measured; XML-doc-comments-name-the-sibling is "informal and easy to miss" with no enforcement; no analyzer enforces the convention. |
| 0042 — Subagent-driven development | **REVISE** | 1×REVISE (Security), 1×REVISE (Operations), 1×POLISH (Technical), 2×PUBLISH | Auto-merge of subagent PRs is the single biggest unbounded-blast-radius concern; "merged-but-wrong" risk is named but not bounded; no kill-switch defined; no audit trail for which subagent shipped which PR; degenerate cron loops + parallel subagents pattern have no defined back-pressure. |

**Headline:** All six ADRs are PUBLISH-or-better at the technical-correctness level. The cluster of REVISE verdicts concentrates on three themes:

1. **Account compromise + bypass model** (ADRs 0038, 0042): both depend on the maintainer's GitHub identity being trustworthy. Neither ADR addresses what happens when it isn't.
2. **Solo-maintainer mitigations that don't survive a second contributor** (ADRs 0039, 0042): "session-end review by the maintainer" and "controller dispatches subagents" both assume one human in a known role. The Revisit triggers name "second contributor joins" but don't pre-design the response.
3. **Commercial liability for AI-generated artifacts shipping to customers** (ADR 0040): the ADR is honest that legal/marketing/critical-error strings are deferred but that carve-out IS the commercial-risk surface; deferring it without naming a deadline accumulates risk.

The recommendation is **publish 0037 and 0041 as-is**; **publish 0038, 0039, 0042 with the proposed amendments below before they ossify**; **revise 0040 before relying on it for any customer-facing string surface**.

---

## Per-ADR per-seat detailed concerns

### ADR 0037 — CI Platform Decision (Stay on GitHub Actions, Adopt `act` for Local)

#### Seat 1 — Technical Correctness: **PUBLISH**

Concerns:
- Option E (Dagger) "Watch" disposition is correctly framed; the "YAML brittleness vs. local-vs-CI divergence" framing is accurate.
- `act` coverage claim ("~80% of GHA workflow semantics") is honest about the gap. Spot-check passes.
- The `runs-on: ubuntu-latest` portability hedge for Forgejo Actions is technically correct — Forgejo Actions runners do consume the same image/runner conventions.

Action items: none.

#### Seat 2 — Security: **PUBLISH**

Concerns:
- Workflow-injection defense (banning `pull_request_target` per PR #129) is correctly cited.
- CodeQL vendor-lock is named as a Forgejo migration cost (Forgejo doesn't run CodeQL). Accurate.
- No discussion of supply-chain risk in `act`'s container images (medium runner image is a third-party container that runs locally with Docker). Minor — `act` runs in dev only, not in CI.

Action items: none.

#### Seat 3 — Operations / Enterprise: **PUBLISH**

Concerns:
- The five Revisit triggers are well-scoped and each has a concrete activation criterion. Good operational hygiene.
- "$600 Mac mini under a desk wins quickly if more than ~50 macOS-job-minutes/month" is a quantified threshold — exactly the right shape for a Revisit trigger.
- The license-gated runner deferral correctly identifies that adding a self-hosted runner is "GHA-vs-GHA configuration change," not a platform migration.

Action items: none.

#### Seat 4 — Product / Commercial: **POLISH**

Concerns:
- The decision rests heavily on "GitHub network effects" for the OSS reference-implementation positioning. This is correct **for the public phase**, but the user's `project_sunfish_private_until_llc` memory says the repo is going **private** before LLC formation. Once private, the GitHub-discoverability premise inverts: at that point, "stay on GHA" is justified by sunk hardening cost and minutes economics, NOT by network effects. The ADR mentions the private flip in Revisit trigger #3 (cost of metered minutes) but does not revisit the network-effects argument.
- Optionality: the "GHA-compatible YAML as a portability hedge" framing is sound. But the "pre-release + breaking-changes-approved" disposition is named once and not threaded through the consequences. A future commercial buyer asking "can we self-host CI?" would want a concrete answer; the ADR's answer ("Forgejo Actions can run the same YAML") is implicit rather than explicit.
- The iOS/MacCatalyst gap is named in Negative consequences but with no budget line. For a commercial accelerator (Anchor) that markets cross-platform reach, "iOS not in CI" is a real product gap. Worth a sharper budget commitment.

Action items:
- **(POLISH)** Add a one-paragraph clause to "Decision drivers" or "Consequences" explicitly addressing the **public→private flip** scenario: which Decision drivers change, which stay, what gets re-examined. Today's Revisit trigger #3 implies it but doesn't enumerate.
- **(POLISH)** Add an "Enterprise/commercial-buyer optionality" note: "Forgejo Actions YAML compatibility is the explicit hedge for any future enterprise customer who requires self-hosted CI."

#### Seat 5 — End-User / Practitioner: **PUBLISH**

Concerns:
- `act` setup pointer (`docs/runbooks/mac-claude-session-setup.md`) is a concrete actionable step.
- Decision drivers are framed in maintainer-experience terms ("solo-maintainer ops budget") which makes the ADR self-explanatory for the audience reading it.
- The "pushed → wait 12 min → fail → fix → push" pain narrative grounds the Decision in lived experience.

Action items: none.

**ADR 0037 overall: PUBLISH with two suggested polishes around the public→private flip and enterprise-buyer optionality.**

---

### ADR 0038 — Branch Protection via GitHub Rulesets

#### Seat 1 — Technical Correctness: **PUBLISH**

Concerns:
- Distinction between legacy branch-protection and Rulesets API is technically accurate (endpoints, semantics, payload shape).
- The Enterprise-only `evaluate` mode caveat on github.com is correctly called out. Free/Pro plans cannot use canary mode; the canary-branch-test substitute is a real workaround.
- Comment-stripping convention (`_comment_*` keys recursively stripped) is a sensible engineering pattern; correctly identifies that the GitHub API rejects unknown top-level fields.
- The distinction "ADR 0038 is *how*; ADR 0039 is *what to require*" is clean separation of concerns.

Action items: none.

#### Seat 2 — Security: **REVISE**

Concerns:
- **`bypass_actors` is the security-critical clause and the ADR does not enumerate or audit it.** The ADR says "the bypass-actor list is explicit and auditable; 'who can bypass main protection' is a grep against a JSON file" — but does not list which actors are currently in the file, nor does it specify a review cadence for the list, nor does it constrain the actor types allowed (account vs. team vs. integration vs. role).
- **No threat model for compromised maintainer account.** If the solo maintainer's GitHub account is compromised, the bypass actor (likely the maintainer's account or a personal-access integration) is the entire blast radius. Compromised account = bypass-merge to main = ruleset is theater. The ADR does not name this risk or its mitigations (hardware key, branch-protection that excludes the maintainer account from bypass, GitHub Advanced Security alerts).
- **No 2FA/hardware-key requirement is stated** in the bypass-actor model. Per GitHub's security best practice, accounts with bypass authority should be hardware-key gated. The ADR's silence here is a defensible choice (out of scope; relies on GitHub account-level security) but should be explicit so a future auditor knows it was considered.
- **Settings-UI drift mitigation is "social, not technical."** The ADR acknowledges this; but no detection mechanism is proposed (e.g., a CI workflow that diffs the live ruleset against the JSON nightly and opens an issue on drift). This is the kind of automated guardrail that prevents a malicious or accidental UI edit from going unnoticed.
- The `_comment_*` stripping convention is technically a Sunfish-specific pattern that an auditor unfamiliar with the script could miss. Mild documentation gap.

Action items:
- **(REVISE)** Add a `bypass_actors` enumeration + audit-cadence subsection. List exactly who/what is in the bypass list today, what actor type, and what `bypass_mode`. Specify a review cadence (e.g., "audited at every Revisit-trigger review and at minimum every 6 months").
- **(REVISE)** Add a "compromised-account threat model" paragraph: enumerate what protection survives a compromised maintainer account, what doesn't, and what compensating controls exist (or are explicitly accepted as out of scope).
- **(POLISH)** Recommend a CI workflow that diffs the live ruleset against `main-ruleset.json` periodically and surfaces drift. Could be a future ADR or a simple add to the runbook.

#### Seat 3 — Operations / Enterprise: **PUBLISH**

Concerns:
- The reproducible-apply contract (idempotent POST/PATCH, comment-stripping, dry-run/evaluate/delete flags) is operationally sound.
- Two-week verification window before retiring legacy script is a reasonable hold-back period.
- The Org-level Rulesets Revisit trigger pre-positions the migration path for if/when Sunfish becomes an Org. Forward-thinking.

Action items: none.

#### Seat 4 — Product / Commercial: **PUBLISH**

Concerns:
- "Branch protection is now declared in version-controlled JSON with inline rationale" is auditable IaC — exactly what enterprise buyers want to see for a vendor's security posture.
- The Org-level migration path is named as a Revisit trigger.

Action items: none.

#### Seat 5 — End-User / Practitioner: **PUBLISH**

Concerns:
- Three-flag script (`--dry-run`, `--evaluate`, `--delete`) is a clean operator surface.
- README pointer in `infra/github/` is named.
- The "ADR 0038 is *how*, ADR 0039 is *what*" framing helps a new contributor pick which ADR answers their question.

Action items: none.

**ADR 0038 overall: REVISE — security seat needs the bypass-actor and compromised-account model addressed before this ADR ossifies. Operations and product seats are sound.**

---

### ADR 0039 — Required-Check Minimalism on Public OSS Repos

#### Seat 1 — Technical Correctness: **PUBLISH**

Concerns:
- The strict-mode + missing-required-check = permanent-block analysis is correct. This is GitHub's documented behavior for both legacy and Rulesets paths.
- Option C's analysis (restructure workflows to fire-on-every-PR with in-job scope detection) is honestly framed as "right answer if Option B's advisory experience proves insufficient" — appropriate technical humility.
- The three-condition test for promoting a check from advisory to required (fires-every-PR, fast, low-flake-history) is a clean rule.

Action items: none.

#### Seat 2 — Security: **POLISH**

Concerns:
- The four required checks (Lint, Analyze csharp, CodeQL, semgrep) are the right minimum security/correctness floor for an OSS repo. Correct selection.
- "A failed advisory check does not block merge" is named honestly. The mitigation ("session-end review of `main`'s post-merge runs") relies on a single-human habit. As a security control, this is weak; as a contribution-flow tradeoff, defensible at the current stage.
- **Bypass-actor merge of an a11y-failing PR is currently allowed without any audit signal.** The ADR doesn't propose any post-hoc detection (e.g., a workflow that scans recent merges for "advisory check failed at merge time" and surfaces them).

Action items:
- **(POLISH)** Add a sentence in Consequences or Revisit-triggers naming the "advisory-check-failed-but-merged" detection gap and pointing at a possible mitigation (e.g., a daily summary workflow that lists merges where any advisory check failed at merge time).

#### Seat 3 — Operations / Enterprise: **REVISE**

Concerns:
- **Multi-maintainer scale-out is named as Revisit trigger #4 ("Second contributor joins") but the response is under-scoped.** What changes when the second contributor lands? "Required-check list policy doesn't change but the surrounding posture does" is hand-wave. Concretely: with two contributors, `required_approving_review_count` flips from 0 to 1; `Build & Test` arguably should flip back to required (since manual session-end review no longer scales); the bypass-actor list needs a re-audit. None of this is pre-staged.
- **The "session-end review" mitigation is a single-human SPOF disguised as a process.** It works because the maintainer is one person who reads `main`'s recent runs. As soon as there are two contributors, "whose session, and end of which session" becomes ambiguous. The ADR should pre-design the multi-contributor response.
- **The "first v1 release ships" Revisit trigger #3 is correct in principle but undated.** No interim checkpoint forces a re-evaluation; the trigger fires only when v1 ships. Suggest an interim quarterly or semi-annual review checkpoint regardless of v1 status.
- The "Cost of over-requiring is asymmetric" framing is correct for current posture but the framing "the latter is recoverable" understates the cost when an a11y regression ships in a release that goes to a customer.

Action items:
- **(REVISE)** Concretize Revisit trigger #4: enumerate which configuration changes happen when a second contributor joins. At minimum: `required_approving_review_count` ≥ 1; `Build & Test` evaluated for promotion to required; bypass-actor re-audit; "session-end review" replaced with a CI-side detection signal.
- **(POLISH)** Add an interim "review at minimum every 6 months regardless of triggers" floor so this ADR doesn't ossify just because none of the triggers have fired.

#### Seat 4 — Product / Commercial: **PUBLISH**

Concerns:
- "Public-repo + docs-friendly contribution flow" is a clear commercial-positioning argument. Aligns with the "reference implementation alongside the book" strategy.
- The private-flip Revisit trigger #1 correctly identifies that posture inverts when external contribution is no longer a concern.

Action items: none.

#### Seat 5 — End-User / Practitioner: **PUBLISH**

Concerns:
- The grid of "checks fires on every PR vs. conditional" is exactly the kind of explanation a new contributor needs.
- The three-condition promotion rule for adding checks back to required is unambiguous.
- The "External contributors might mistake required-checks-green for fully-validated" gap is named with a concrete proposed mitigation (PR template update) — explicitly deferred but tracked.

Action items: none.

**ADR 0039 overall: REVISE — operations seat needs the multi-maintainer flip-back response pre-designed; security seat asks for an advisory-failed-but-merged audit signal.**

---

### ADR 0040 — AI-First Translation Workflow with 3-Stage Validation Gate

#### Seat 1 — Technical Correctness: **PUBLISH**

Concerns:
- Stage 2 (back-translation with a different engine) is correctly identified as the highest-yield validator. Sound technical claim — back-translation specifically catches meaning-drift, untranslated brand terms, and grammatical errors.
- The 0.7 cosine / "30% drift" threshold is described as heuristic and "open to tuning" — appropriate humility but the threshold itself is unjustified by data; the ADR doesn't say which embedding model or which similarity metric was used to calibrate it. Fine for v1 of a heuristic; should be documented for reproducibility.
- Cross-engine cross-check (Stage 3) using Azure Translator F0 and NLLB-200 is technically sound; both are publicly available.
- The "we ship validation-flags.md as a paper trail" pattern is correct — auditable artifact in version control.

Action items:
- **(POLISH)** Document the embedding model and similarity metric used for the 0.7 cosine threshold. Reproducibility matters for a heuristic that gates customer-facing strings.

#### Seat 2 — Security: **POLISH**

Concerns:
- AI translation engines have access to source strings. For UI strings this is benign (strings are public after release anyway). For any future translation of strings that include placeholder syntax, error codes, or secrets-adjacent content, this becomes a data-handling concern. The ADR doesn't define a "do not send to external engines" string-class allowlist.
- API-key handling for the engines (Claude, GPT-4, DeepL, Azure Translator, NLLB-200) is unaddressed — where do the keys live, who has access, what's the rotation cadence?
- A poisoned engine response (LLM prompt-injection via a crafted source string) is theoretical but worth naming for an auditor.

Action items:
- **(POLISH)** Add a "what NOT to translate via this workflow" subsection: strings containing secrets, customer data, or any non-public content. State explicitly that all source strings sent through Stages 1-3 are public-by-design (UI labels).
- **(POLISH)** Cross-reference where engine API keys are managed (presumably the maintainer's local secrets / GitHub Actions secrets); add a rotation cadence note.

#### Seat 3 — Operations / Enterprise: **PUBLISH**

Concerns:
- The cost analysis ($77K full pass commercial vs. ~$15-50/locale flagged-strings review) is concrete and persuasive.
- Same-day per-locale turnaround is a real operational improvement.
- `validation-flags.md` as a per-PR artifact is reviewable infrastructure.
- The subagent-cascade-brief pattern (PR #144) makes Stage 2 a REQUIRED step in the brief template — process-side enforcement.

Action items: none.

#### Seat 4 — Product / Commercial: **REVISE**

Concerns:
- **Liability for AI-translated strings is the elephant in the room.** The ADR explicitly defers "marketing copy, legal/ToS strings, top-N production error messages, brand-name-adjacent strings" — but those four classes are precisely where commercial liability lives. A machine-translated error message that says "press X to delete" instead of "press X to confirm" in a banking customer's locale is a real-money incident, regardless of whether the string was technically a UI label or a legal notice.
- The carve-out is honest but **open-ended**. There is no deadline for enumerating the high-stakes string surface. Without a deadline, the carve-out drifts and AI-translated strings keep landing in surfaces that should have been Option-E-treated.
- **The Anchor and Bridge accelerators are not just demo OSS — they are the commercial product surfaces** (Bridge is the Zone-C SaaS shell per ADR 0031). AI-translated UI strings shipping in commercial accelerators carry vendor-liability implications the ADR doesn't address. "Pre-v1 OSS reference implementation" framing is true but incomplete: Bridge in particular is positioned as a hosted-SaaS product, and customers of that product expect translated strings to be human-validated to a commercial standard.
- Future v1 readiness: regulated-industry buyers will ask "how were translations validated?" The 3-stage gate is a reasonable answer for commodity UI strings but **insufficient** for any string that influences user money, safety, health, or legal status. The ADR mentions this but doesn't gate Bridge's commercial launch on it.
- The "AI translation quality improves materially" Revisit trigger #5 is a one-way ratchet — the trigger fires only on improvement. There's no symmetric trigger for "AI translation introduces a regression-class error in production" beyond #1 (incident-driven).

Action items:
- **(REVISE)** Set a concrete **deadline** for enumerating the high-stakes string surface (marketing/legal/critical errors/brand-adjacent). Suggest: before Bridge's first commercial customer goes live, OR by 2026-Q3, whichever is earlier.
- **(REVISE)** Add a **per-accelerator clause**: Anchor (Zone A, local-first) can ship AI-first translation as the steady state for all UI strings. Bridge (Zone C, multi-tenant SaaS) requires the Option-E mixed-approach for any string class shown to the tenant's end-users (not just the tenant admin). Rationale: Bridge's per-tenant data-plane isolation is for data; localized strings shown to end-users are still vendor-responsibility content.
- **(REVISE)** Add a **liability disclaimer** for the v1 release notes that explicitly names "translations validated via 3-stage AI gate; report errors to the maintainer; not warranted for use in regulated contexts (medical, legal, financial advice) without human translator validation."

#### Seat 5 — End-User / Practitioner: **REVISE**

Concerns:
- For a developer-practitioner consuming Sunfish strings, the workflow is well-documented and reproducible.
- For an **end-user** of an Anchor or Bridge deployment in a non-English locale: a register-mismatched honorific in Japanese, a misplaced RTL marker in Hebrew, or a politeness-form error in German is **noticeable and undermines product trust**. The ADR's "users in those locales will occasionally see register-mismatched strings and report them" is a defensible statement for OSS but not for a commercial accelerator targeting that locale's market.
- The per-locale "quality notes" pattern is good but the notes are stored where? `docs/runbooks/i18n-translation-validation.md`? The notes need a stable, discoverable location and a versioning model so locale-specific known-issues compound across cascades.
- "External contributors might mistake required-checks-green for fully-validated" — same kind of trust-gap exists here. End-users in a target locale don't know that their bundle was AI-translated. Consider a small in-app affordance: "this translation was machine-validated; report errors here." This is a UX choice but the ADR doesn't even raise it.

Action items:
- **(REVISE)** Add an end-user-facing translation-feedback mechanism to the ADR's scope (or explicitly defer it with an owner): a way for end-users in a locale to flag a bad translation back to the maintainer / community translator.
- **(POLISH)** Pin the per-locale quality-notes file format and location explicitly. Don't leave it as "see the runbook" — show the file path and the versioning convention.

**ADR 0040 overall: REVISE — the largest finding in this council pass. The technical workflow is sound but commercial and end-user implications are under-scoped, and the explicit deferral of high-stakes strings is open-ended.**

---

### ADR 0041 — Dual-Namespace Components by Design (Rich vs. MVP)

#### Seat 1 — Technical Correctness: **PUBLISH**

Concerns:
- The four-pair table is accurate and exhaustive against current state.
- The "type-name reuse across namespaces is intentional" claim is technically valid in C# — namespace + type-name is the qualified identifier; reuse is permitted and supported.
- Cross-references to ADR 0022 (rich-vs-MVP catalog) and ADR 0014 (adapter parity) are accurate and bidirectional.
- The api-change pipeline routing for any genuine consolidation is the correct process.

Action items: none.

#### Seat 2 — Security: **PUBLISH**

Concerns:
- Dual-namespace pattern has no direct security implications. Both halves are equally subject to the same code review and analyzer surface.
- The "code-quality scanners and batch tools that flag these as duplicates are wrong" rule is a process control that matches what subagent `aa304586` did when it correctly aborted destructive action.

Action items: none.

#### Seat 3 — Operations / Enterprise: **POLISH**

Concerns:
- **Maintenance tax is acknowledged but not measured.** "A bug fix often needs two PRs or one PR touching two locations" — how often, in practice, has this happened? Without a baseline, the cost-of-pattern is unknown when the team next debates whether the pattern still pays off.
- The "evaluate sibling for the same fix" convention is informal. No analyzer enforces it. A future regression where one half is fixed and the other isn't will be caught only at code review or at user report. An analyzer that detects "type X exists in two namespaces; commit touches one but not the other" would mechanize the convention.
- Code-quality-tool allowlist maintenance ("when a new dedup scanner ships, add the four pairs to its allowlist") is described but not centralized. A single allowlist file referenced by all tools would be lower-maintenance than per-tool allowlists.
- Future fifth/sixth pair joining the family is named in Revisit triggers but no entry-criteria are defined. When SHOULD a new component join the family vs. stay single-namespace?

Action items:
- **(POLISH)** Add a measurable cost-tracking note: count, over the next two quarters, how often a bug fix requires touching both halves of a pair. This becomes the data input for any future "is this pattern still worth it?" review.
- **(POLISH)** Recommend (not commit to) a Roslyn analyzer that enforces "if you touch one half of a dual-namespace pair, the other half MUST be touched in the same PR or have an inline justification." Or a Husky-side check.
- **(POLISH)** Add entry-criteria for joining the family: when does a new component get a rich-and-MVP variant vs. a single namespace? Without criteria, the family can grow accidentally.

#### Seat 4 — Product / Commercial: **PUBLISH**

Concerns:
- The dual-namespace pattern serves real downstream-consumer needs (kitchen-sink demos vs. small-stable surface). Commercial-product positioning is intact.
- The "may not be deduped without a formal sunfish-api-change ICM pipeline" is a defensible governance gate that protects the public API contract.

Action items: none.

#### Seat 5 — End-User / Practitioner: **POLISH**

Concerns:
- "Why are there two `SunfishGantt`s?" is named as a first-time question. The ADR + XML doc comments are the answer but **the question fires before the contributor finds the ADR**. A `README.md` in `packages/ui-adapters-blazor/Components/` naming the four pairs and pointing at ADR 0041 would short-circuit the question at the directory-listing level.
- The XML doc comments naming the sibling are easy to miss. Bringing them out into a per-folder README is a low-cost discoverability win.
- The pattern is now the canonical answer to dedup-flag scanners — but a new contributor scanning the codebase still sees what looks like duplication. Discoverability of the ADR is the binding constraint.

Action items:
- **(POLISH)** Add (or commit to adding) a `packages/ui-adapters-blazor/Components/README.md` that names the four pairs and links to ADR 0041. Discoverability at the directory-listing level.

**ADR 0041 overall: PUBLISH with discoverability and enforcement polishes.**

---

### ADR 0042 — Subagent-Driven Development for High-Velocity Sessions

#### Seat 1 — Technical Correctness: **POLISH**

Concerns:
- The throughput claim (~30 PRs in 8-10 hours; ~6-10× sequential) is plausible and is grounded in the actual session.
- "Worktree isolation prevents cross-task interference. Conflicts surface at merge time" is technically accurate for the `git worktree` mechanism.
- The failure-mode table is honest and each entry has a fix or reference. Good engineering hygiene.
- "Subagent fails silently" mitigation = "brief template requires a final report; absent report = treat as failure" — this is a process mitigation, not a technical one. Technical mitigation would be a harness-level timeout that surfaces "agent X has been quiet for N minutes." Worth naming.
- "Two subagents touching adjacent files" mitigation = "Resolved at merge time; cheap when PRs are small" — this is true on average but the worst case (two subagents both edit the same workflow YAML or the same `.resx`) is not cheap. No back-pressure on dispatch is named.

Action items:
- **(POLISH)** Add a timeout/heartbeat mechanism note: subagents that haven't reported in N minutes should be flagged for controller attention regardless of the brief's "final report" convention. Process mitigation alone is fragile.
- **(POLISH)** Document a back-pressure rule: when dispatching N parallel subagents, if K subagents are touching files in the same directory subtree, dispatch should serialize within that subtree.

#### Seat 2 — Security: **REVISE**

Concerns:
- **Auto-merge of subagent-produced PRs is the single largest blast-radius surface in the entire ADR cluster.** The contract is: subagent writes brief → agent edits → CI runs → green CI auto-merges. The CI gate is the only thing between "subagent did something wrong" and "main branch is broken." Per ADR 0039, the required-check list is intentionally minimal (4 checks). Per ADR 0042's contract, the subagent has full repo write access during its execution. The gap is real.
- **No audit trail links a merged PR to the specific subagent that produced it.** PR titles and commit messages don't carry "produced by subagent agent-XYZ on brief brief-name." Forensics for a bad merge ("which brief and which subagent shipped this?") is not mechanized.
- **No kill-switch is defined.** If a subagent goes haywire (e.g., starts deleting files based on a hallucinated requirement), the ADR doesn't define how the controller halts it mid-flight. "Subagent restart is cheap" is named, but "subagent kill" isn't.
- **Subagent-driven dispatch + auto-merge creates an attacker pathway.** If the maintainer's session is compromised (malicious extension, screen-share viewer, or compromised LLM endpoint), a malicious actor with controller-level access can dispatch subagents that legitimately edit, commit, and auto-merge to main. This is a variant of the ADR-0038 compromised-account threat. The combination of ADR 0038 (bypass-actors don't need code review) + ADR 0039 (minimal required checks) + ADR 0042 (auto-merge of subagent PRs) is **strictly more permissive than any of them alone**. The cluster needs a unified threat model.
- **Briefs themselves are not version-controlled or reviewed.** A brief is the load-bearing input that determines what the subagent does. If briefs are crafted in-session and not committed, there's no audit trail of "what was the subagent told to do?"
- Per the user's `feedback_pr_push_authorization` memory ("CI is the gate; direct push to main forbidden"), subagent dispatch correctly routes through PR. Good. But the "auto-merge enabled by the subagent" vs. "auto-merge enabled by the controller" distinction matters for accountability and the ADR doesn't pin it.

Action items:
- **(REVISE)** Add a "compromised-controller threat model" section: enumerate what protects against a malicious actor who gains controller-level access and dispatches subagents. Cross-reference ADR 0038's bypass-actor model.
- **(REVISE)** Require an audit-trail clause: every subagent-produced PR title or body MUST identify the dispatching subagent ID and the brief used. PR-template-level enforcement.
- **(REVISE)** Define a kill-switch: how does the controller stop a subagent mid-flight? At minimum, name the operational primitive (e.g., "delete the worktree, the subagent process must stop on next checkpoint").
- **(REVISE)** Decide whether briefs themselves should be version-controlled. If reusable templates live at `_shared/engineering/subagent-briefs/`, what about per-task briefs? Recommend either committing them as PR-side artifacts or capturing them in PR descriptions.

#### Seat 3 — Operations / Enterprise: **REVISE**

Concerns:
- **The pattern's failure mode under degenerate conditions is unbounded.** "Cron loops + parallel subagents" (named in the user's task description) can degenerate into: a cron triggers a controller that dispatches subagents that produce PRs that fail and trigger more cron-driven retries. Without a defined failure-budget and circuit-breaker, the pattern can produce thousands of failed PRs and still claim "we shipped a lot." The ADR names "subagent restart is cheap" but does not name "stop dispatching after N failures in M minutes."
- **Quality regression is not measured.** The ADR claims "throughput drops with no quality drop" implicitly — but the only quality signal cited is "CI green = correct." That's a low bar. Code quality, design quality, ADR-alignment quality, and accumulated tech-debt-per-PR are all unmeasured.
- **The brief-template library will outgrow the directory model.** Revisit trigger #2 ("library grows past ~10 distinct shapes") names a meta-organization need but doesn't pre-design the response. By the time the library has 10 templates and three subagents each have a different mental model of which template applies, the search-cost dominates.
- **Multi-contributor scale-out is named (Revisit trigger #3) but under-scoped.** Two humans dispatching subagents in parallel = N×2 worktrees, divergent brief styles, ambiguous PR-review ownership, contention for `main`'s CI minutes. The ADR doesn't pre-design any of this.
- **The "auto-merge as completion gate" assumes `main` is stable.** In a session where many subagent PRs auto-merge in rapid succession, `main` can pick up a regression that isn't visible until much later (e.g., two PRs both pass CI individually but interact badly post-merge). The ADR names "git revert" as recovery but a multi-PR revert chain is not always graph-clean.
- **Sustainability over time:** the pattern demands constant brief-quality, constant subagent grounding, constant maintainer attention to dispatch. A long-running session "needs hard breaks" (named in Negative consequences). What happens to in-flight subagents during the break? Are they paused, killed, allowed to complete?

Action items:
- **(REVISE)** Define a circuit-breaker: stop dispatching new subagents after K failures in N minutes (or after K%-failure-rate over the last M PRs). Make this explicit in the contract.
- **(REVISE)** Define a quality-regression detection signal beyond CI-green: e.g., per-week LoC-merged trend, per-week revert-rate, per-week PR-rework rate. If any spikes, slow down dispatch.
- **(POLISH)** Pre-design the multi-contributor response (Revisit trigger #3): brief-ownership rules, PR-review ownership, dispatch-coordination protocol.
- **(POLISH)** Pre-design the brief-library scale-out (Revisit trigger #2): when 10+ templates exist, what's the navigation model?
- **(POLISH)** Define what happens to in-flight subagents during a controller break: paused, killed, allowed to complete? Without this, "the controller takes a break" is unsafe.

#### Seat 4 — Product / Commercial: **PUBLISH**

Concerns:
- The throughput improvement is real and supports the project's "ship the reference implementation alongside the book" cadence.
- The pattern is genuinely novel (or at least uncommon) and could become a Sunfish positioning differentiator (the project that ships at 6-10× because it dispatches subagents responsibly). This is positive but not yet packaged.
- Pre-release posture is correctly identified as well-matched. Post-v1 the pattern's appropriateness shifts.

Action items: none.

#### Seat 5 — End-User / Practitioner: **PUBLISH**

Concerns:
- For a maintainer-practitioner reading this ADR, the contract is unambiguous (six conditions; six failure modes; six revisit triggers).
- The brief-template library is named and pointed-at concretely.
- The "subagent dispatch is INAPPROPRIATE for..." enumeration is honest about the pattern's limits.
- Future contributors arriving fresh see a pattern that is documented, has a buglog, and has a brief library. Onboarding cost is real but bounded.

Action items: none.

**ADR 0042 overall: REVISE — security and operations seats raise concerns about auto-merge-blast-radius, audit trail, kill-switch, circuit-breaker, and multi-contributor pre-design. The pattern's productivity is real but the safety net is currently "CI green" + "controller intuition," and that pair is fragile.**

---

## Aggregated action items (prioritized)

### Priority 1 — Should land before next high-velocity session

These items address blast-radius and audit-trail concerns where deferral compounds risk per session.

1. **ADR 0042 — Compromised-controller threat model + audit-trail clause + kill-switch + circuit-breaker.** (Security + Operations seats.) Add the section before the next subagent-heavy session; add `agent-XYZ` identifier to every subagent-produced PR title or body; document how to halt a runaway subagent.
2. **ADR 0038 — `bypass_actors` enumeration + audit cadence + compromised-account threat model.** (Security seat.) The current ADR says the bypass list "is auditable" but doesn't audit it. Enumerate, name a review cadence, name the compensating controls.
3. **ADR 0040 — Per-accelerator clause: Bridge SaaS strings get Option-E treatment for end-user-facing surfaces.** (Product seat.) The ADR's deferred carve-outs (marketing/legal/critical errors) line up exactly with Bridge's commercial-customer surface. Pin the policy now before a Bridge customer goes live.

### Priority 2 — Should land before next ADR rev or before the named Revisit trigger fires

These items add structure that prevents the ADRs from drifting under their own deferred risks.

4. **ADR 0040 — Set a deadline for high-stakes-string enumeration.** Suggest 2026-Q3 or before Bridge's first commercial launch.
5. **ADR 0040 — Document the embedding model + similarity metric used for the 0.7 cosine threshold.** Reproducibility for the gate.
6. **ADR 0039 — Concretize the multi-maintainer flip-back response.** When a second contributor joins, what changes (review-count, required-check list, bypass-actor audit, session-end-review replacement).
7. **ADR 0042 — Pre-design the multi-contributor response and the brief-library scale-out response.** Don't wait for the trigger to fire to start designing.

### Priority 3 — Discoverability + low-cost polish

These items improve onboarding and reduce per-question maintainer burden.

8. **ADR 0037 — Add public→private flip clause + enterprise-buyer optionality note.**
9. **ADR 0038 — Recommend a CI workflow that nightly diffs the live ruleset against the JSON.**
10. **ADR 0039 — Add an "advisory-failed-but-merged" detection signal (daily summary).**
11. **ADR 0039 — Add 6-month minimum review floor.**
12. **ADR 0040 — Add "what NOT to translate" subsection + API-key handling reference + end-user feedback mechanism + per-locale quality-notes file format/location.**
13. **ADR 0041 — Add `packages/ui-adapters-blazor/Components/README.md` naming the four pairs + ADR 0041 link.**
14. **ADR 0041 — Recommend a Roslyn analyzer (or Husky check) that enforces sibling-touch.**
15. **ADR 0041 — Add entry-criteria for new pairs joining the family.**
16. **ADR 0041 — Track bug-fix-touches-two-files frequency over next two quarters.**
17. **ADR 0042 — Add timeout/heartbeat mechanism + back-pressure rule for subagents touching same subtree + brief-version-control decision + in-flight-during-break behavior + quality-regression detection signal.**

### Cross-ADR theme — unified threat model

A standalone follow-up ADR or governance document should consolidate the **maintainer-account-compromise + minimal-required-checks + auto-merge + subagent-bypass** chain into a single unified threat model. ADRs 0038, 0039, and 0042 each address a piece; the chain is more permissive than any one ADR makes visible. This is the highest-priority cross-cutting recommendation.

---

## Recommended ADR amendments (proposed edit text)

These are draft edit blocks the maintainer can apply, modify, or reject. The PR carrying this council review does NOT modify the ADRs.

### Amendment for ADR 0037 — public→private flip + enterprise optionality

Add to **Decision drivers** (after "Pre-release + breaking-changes-approved posture..."):

```markdown
- **Public→private flip readiness.** Per the project's `private until LLC`
  governance posture, the repo will flip to private before commercial release.
  When that happens: the "GitHub network effects" decision driver inverts
  (no public discoverability premise), the "free GHA minutes" driver gates
  on the chosen plan's metered budget, and the iOS/MacCatalyst gap becomes
  customer-visible. None of these change the GHA-vs-alternative answer
  immediately, but each Revisit trigger becomes more pointed. This ADR's
  enterprise-buyer optionality is preserved by writing GHA-compatible YAML
  (Forgejo Actions, self-hosted GHA runners, and Dagger-on-anything are all
  reachable from the current state).
```

### Amendment for ADR 0038 — bypass_actors audit + threat model

Add a new section before **Consequences**:

```markdown
### Bypass-actor audit and compromised-account threat model

The `bypass_actors` field of `main-ruleset.json` is the security-critical
clause of this ADR. As of 2026-04-26 the bypass list contains:

| Actor | Type | Bypass mode | Justification |
|---|---|---|---|
| <fill in> | <Account/Team/Integration/Role> | <always/pull_request> | <why this bypass exists> |

**Audit cadence:** the bypass list is reviewed at every Revisit-trigger
review of this ADR AND at minimum every 6 months. Removal of an entry is
a one-PR change to `main-ruleset.json` + `apply-main-ruleset.sh` re-run.

**Compromised-account threat model:** if a bypass-actor account is compromised,
the attacker can merge to `main` regardless of the required checks. Mitigations:
(a) all bypass-actor accounts MUST have hardware-key 2FA enabled at the
GitHub-account level (out-of-scope for this ADR but assumed); (b) GitHub
Advanced Security alerts on the repo surface anomalous merges; (c) `main`
branch history is preserved (no force-push allowed) so any malicious merge
can be reverted and audited post-hoc. Compensating controls do NOT eliminate
the risk; the bypass list is intentionally minimal as the primary mitigation.

**Drift detection:** a separate workflow (deferred — tracked as a follow-up)
will diff the live ruleset against `main-ruleset.json` nightly and open an
issue on drift. This catches Settings-UI edits that bypass the IaC source
of truth.
```

### Amendment for ADR 0039 — multi-maintainer flip-back response + advisory-failed audit

Replace **Revisit trigger #4** with:

```markdown
4. **Second contributor joins.** Specific configuration changes that fire on
   this trigger:
   - `required_approving_review_count` flips from `0` to `1`.
   - `Build & Test` is evaluated for promotion to required (apply Option-C
     workflow restructure if necessary).
   - Bypass-actor list is re-audited (per ADR 0038's audit cadence).
   - The "session-end review of post-merge runs" mitigation is replaced with
     a CI-side detection signal (e.g., a daily summary workflow that lists
     merges where any advisory check failed at merge time).
   - A follow-up ADR documents the multi-maintainer policy explicitly.
```

Add a new **Revisit trigger #6**:

```markdown
6. **Minimum 6-month interim review.** Regardless of whether any other
   trigger has fired, this ADR is reviewed at minimum every 6 months. The
   review confirms the four required checks remain the right always-on set
   and the advisory list reflects current workflow shape.
```

### Amendment for ADR 0040 — per-accelerator clause + deadline + threshold provenance

Add to **Decision** (after the 3-stage gate description):

```markdown
### Per-accelerator policy

- **Anchor (Zone A, local-first desktop accelerator):** AI-first translation
  per the 3-stage gate is the steady-state for ALL UI strings. Anchor's local
  deployment posture means strings are visible only to the local user; the
  vendor-liability surface is bounded.
- **Bridge (Zone C, multi-tenant SaaS accelerator):** AI-first translation
  per the 3-stage gate is the default for tenant-admin-facing strings.
  Strings shown to a tenant's END-USERS (the SaaS customers' customers)
  require Option-E mixed treatment: AI-first for routine UI, commercial
  human translation for any string that influences end-user money, safety,
  health, or legal status. Bridge's per-tenant data-plane isolation
  (per ADR 0031) governs DATA isolation; it does NOT cover vendor-supplied
  string content.

### High-stakes-string enumeration deadline

The "marketing copy / legal / top-N production error messages /
brand-name-adjacent strings" carve-out (Option E mixed approach) MUST be
enumerated and policy-pinned before the earlier of:

- Bridge's first commercial customer go-live, OR
- 2026-Q3 (2026-09-30).

Until enumerated, those string classes are AI-first translated under
explicit acknowledgment of vendor-liability risk; the v1 release notes
include a translation-validation disclaimer (see Negative consequences).

### Validation threshold provenance

The 0.7 cosine similarity threshold (Stage 2) is calibrated using
<embedding model name + version> and the cosine distance metric on
sentence-level embeddings. The threshold is heuristic; it is reviewed
when Revisit trigger #1 (production translation incident) fires.
```

### Amendment for ADR 0041 — discoverability + entry criteria

Add to **Rules** (after rule 5):

```markdown
6. **A directory-level README is the discoverability anchor.**
   `packages/ui-adapters-blazor/Components/README.md` lists the four pairs
   in this ADR's table and links to ADR 0041. New components should not
   join the family without being added to that README.

### Entry criteria for joining the dual-namespace family

A new component pair is added to this ADR's table only when ALL of the
following hold:

1. **Two genuinely distinct consumer paths exist** (e.g., kitchen-sink
   demo target AND framework-agnostic catalog leaf) with materially
   different API surfaces.
2. **The richer variant cannot be a strict superset** of the simpler one
   without imposing demo-specific dependencies on catalog consumers.
3. **The maintainer commits to the maintenance tax** (bug fixes routinely
   touching both halves).

Single-namespace components are the default; dual-namespace is the exception
and requires this ADR's table to be updated in the same PR.
```

### Amendment for ADR 0042 — threat model + kill-switch + audit trail + circuit-breaker

Add a new section after **Contract for using the pattern**:

```markdown
### Compromised-controller threat model

If the controller (the foreground Claude session + the human's terminal)
is compromised — malicious extension, screen-share viewer, malicious LLM
endpoint, etc. — the attacker inherits the controller's ability to dispatch
subagents. Subagent-produced PRs would auto-merge once CI is green (per
ADR 0039's required-check list). Compensating controls:

- The four required checks (Lint, CodeQL, semgrep, Analyze csharp) are the
  only mechanical gate. CodeQL + semgrep would catch many code-injection
  classes; they would NOT catch a subtle logic regression introduced via
  a plausibly-worded brief.
- ADR 0038's bypass-actor audit governs who can merge without those checks.
- `main` history is preserved; any malicious merge is revertable post-hoc.

Risk acceptance: the pattern is appropriate for the current single-maintainer
threshold. A second contributor joining MUST trigger a re-evaluation of
auto-merge defaults (cross-references ADR 0039 Revisit trigger #4).

### Audit trail requirement

Every subagent-produced PR MUST identify in its title or body:
- The dispatching subagent ID (e.g., `agent-a08ac0f4dc22b2a42`).
- The brief used (either the template name from `_shared/engineering/subagent-briefs/`
  OR an inline summary of the brief's key clauses if the brief was bespoke).

PR-template-level enforcement is preferred over honor-system. Forensics for
"which brief shipped this regression" requires the link.

### Kill-switch

To halt a subagent mid-flight: delete the worktree at
`.claude/worktrees/agent-<id>/`. The subagent's next checkpoint write fails
and the agent process should terminate. If the agent has already opened a PR
with auto-merge enabled, the controller MUST disable auto-merge on that PR
(`gh pr merge --disable-auto`) BEFORE the worktree deletion to prevent the
PR from merging during the worktree-deletion race.

### Circuit-breaker

Subagent dispatch MUST stop after either:
- 3 consecutive subagent failures (CI red, brief misexecution, or silent
  timeout), OR
- 50% subagent-failure rate over the last 6 dispatches.

When tripped, the controller pauses dispatch and surfaces the failure pattern
to the human for diagnosis. Resume requires explicit human action.

### Brief version control

Reusable brief templates live at `_shared/engineering/subagent-briefs/`. Per-task
briefs (the actual brief sent to one specific subagent) SHOULD be either:
(a) committed as PR-side artifacts in the same PR the subagent produces, OR
(b) captured in the PR description's body.

Without one of these, "what was the subagent told to do?" is unrecoverable
forensics.

### In-flight subagents during controller break

When the controller takes a hard break (per Negative consequences), in-flight
subagents are allowed to complete to PR-with-auto-merge state. The controller
SHOULD NOT take a hard break while subagents are mid-execution unless:
(a) the circuit-breaker has tripped, or
(b) the controller explicitly disables auto-merge on all in-flight PRs first.

### Quality-regression detection signal beyond CI-green

Throughput-with-quality-drop is an anti-pattern; CI-green alone is not the
quality signal. Track per-week:
- LoC merged per week.
- Revert rate (PRs reverted within 7 days of merge).
- Rework rate (follow-up PRs that fix issues introduced by a recent merge).

If revert rate or rework rate exceeds a baseline (TBD; first quarter of
data establishes it), slow dispatch.

### Heartbeat mechanism

Subagents that have not reported progress in N minutes (suggested: 30) are
flagged for controller attention regardless of the brief's "final report"
convention. The harness-level mechanism for this is currently the user's
notification-on-completion stream; longer-running quiet subagents need an
explicit heartbeat. Tracked as a follow-up.

### Back-pressure for adjacent-file collisions

When dispatching N parallel subagents, dispatch SHOULD serialize within any
directory subtree where K subagents are already in-flight (suggested K=2).
Cross-tree parallelism is unrestricted. Manual coordination is acceptable
in lieu of automation; the rule exists so the practice is named.
```

---

## Cross-ADR recommendation — unified threat model

The amendments above are per-ADR. They do not address the **chain of permissiveness** formed by:

- ADR 0038: bypass-actors can merge to `main` without checks.
- ADR 0039: required-checks deliberately minimal (4 of ~9).
- ADR 0042: subagents have repo write access and produce auto-merging PRs.

Combined: a compromised maintainer account can dispatch a subagent that produces a PR that bypasses required checks via the bypass-actor list, and the merge is logged but not gated. This chain is **more permissive than the worst case of any individual ADR** and is invisible from any one ADR.

**Recommendation:** open a follow-up governance document (or a new ADR — provisionally "ADR 0043 — Unified threat model: bypass-actor + required-check + auto-merge + subagent chain") that draws the chain explicitly and names the controls that mitigate the combined surface. This is the single highest-priority cross-cutting recommendation from this council pass.

---

## Verdict tally

| ADR | T-Correct | Security | Ops | Product | Practitioner | Worst |
|---|---|---|---|---|---|---|
| 0037 | PUBLISH | PUBLISH | PUBLISH | POLISH | PUBLISH | POLISH |
| 0038 | PUBLISH | **REVISE** | PUBLISH | PUBLISH | PUBLISH | REVISE |
| 0039 | PUBLISH | POLISH | **REVISE** | PUBLISH | PUBLISH | REVISE |
| 0040 | PUBLISH | POLISH | PUBLISH | **REVISE** | **REVISE** | REVISE |
| 0041 | PUBLISH | PUBLISH | POLISH | PUBLISH | POLISH | POLISH |
| 0042 | POLISH | **REVISE** | **REVISE** | PUBLISH | PUBLISH | REVISE |

30 seat-verdicts total. 16 PUBLISH, 8 POLISH, 6 REVISE, 0 REWORK.

**No ADR requires REWORK.** The cluster is structurally sound; the REVISE verdicts concentrate on (a) compromised-account / chain-of-permissiveness, (b) commercial-liability for AI-translated strings, and (c) multi-contributor pre-design.

---

## Out of scope for this council pass

- Verifying that `infra/github/main-ruleset.json` and `apply-main-ruleset.sh`
  match the contract described in ADR 0038.
- Verifying that `docs/runbooks/i18n-translation-validation.md` and
  `_shared/engineering/subagent-briefs/i18n-cascade-brief.md` exist and
  match the contract described in ADR 0040.
- Verifying that the four dual-namespace component pairs in ADR 0041 actually
  exist at the cited paths.
- Auditing the bypass-actor list directly (the file is in repo; the council
  recommends the maintainer enumerate it as the first action item from this
  review).
- Cost-benefit on the proposed cross-cutting "ADR 0043 unified threat model."

These are recommended follow-ups. None block adoption of this council review.
