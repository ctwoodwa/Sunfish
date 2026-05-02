---
id: 43
title: 'Unified Threat Model: The Chain of Permissiveness in Sunfish''s Public-OSS Posture'
status: Accepted
date: 2026-04-26
tier: policy
concern:
  - security
  - threat-model
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0043 — Unified Threat Model: The Chain of Permissiveness in Sunfish's Public-OSS Posture

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A cross-cutting concern raised by the 2026-04-26 council review of ADRs 0037-0042 (`waves/governance/2026-04-26-council-review-adrs-0037-0042.md`, "Cross-ADR recommendation — unified threat model"). The council's per-ADR REVISE verdicts each named a local concern (bypass-actor audit on 0038, multi-maintainer flip-back on 0039, kill-switch + audit-trail + circuit-breaker on 0042). Read together, those concerns describe the same attack surface viewed from different angles: ADRs 0038 + 0039 + 0042 form a **chain of permissiveness** that is more permissive than any one ADR makes visible. This ADR draws the chain explicitly, catalogs the threats that exploit it, and prioritizes the controls that close the gaps.

---

## Context

Sunfish is a public OSS repository operated by a solo maintainer with a high-velocity dispatch model. Three ADRs define the merge-path posture:

- **[ADR 0038](./0038-branch-protection-via-rulesets.md)** — branch protection on `main` is applied as a GitHub Ruleset. The Ruleset includes a `bypass_actors` list. Whoever is in that list can merge to `main` regardless of required checks; the maintainer (or a personal-access integration acting as the maintainer) is the realistic occupant of that list today.
- **[ADR 0039](./0039-required-check-minimalism-public-oss.md)** — to keep auto-merge working for docs-only and path-conditional PRs, the required-status-check list on `main` is intentionally minimal: exactly four always-on checks (Lint PR commits, Analyze csharp, CodeQL, semgrep-cloud-platform/scan). All other gates (Build & Test, A11y Storybook, CSS audit, Locale completeness, Global-UX Gate) are advisory — they run when their `paths:` filter matches, they show on the PR, but they do not block merge by *not running* and they do not block merge if their conclusion is failure when the maintainer ignores it.
- **[ADR 0042](./0042-subagent-driven-development-for-high-velocity.md)** — parallel background subagents dispatched by the controller open PRs and enable `gh pr merge --auto --squash`. Subagent completion = PR open + auto-merge enabled = lands when CI is green.

Each ADR is locally defensible. Read in order, however, they compose a chain:

```
[Subagent dispatched]                   ← ADR 0042
       │
       │ produces a PR with auto-merge enabled
       ▼
[CI runs the 4 minimal required checks] ← ADR 0039
       │
       │ if all 4 pass → auto-merge fires
       ▼
[Merge to main happens]                 ← (no human review required)
       │
       │ OR: bypass_actors entry overrides the gate entirely
       ▼
[bypass merge to main]                  ← ADR 0038
```

The chain is **strictly more permissive than the worst case of any individual ADR**. ADR 0038 read alone says "bypass exists for emergencies." ADR 0039 read alone says "the four checks we require are the right floor." ADR 0042 read alone says "auto-merge with CI as the gate." Composed, the system permits: a compromised dispatcher → a subagent-authored PR → 4 mechanical checks → automatic merge with no human in the loop AND a parallel out-of-band path where a compromised maintainer account uses bypass to land code that fails ANY of the 4 checks.

This ADR names that surface and prioritizes the controls that close it.

---

## Decision drivers

- **Defense-in-depth requires a system-level threat model, not per-ADR threat models.** Each ADR's local mitigations are sound. The gap is at the seams: no single ADR is responsible for "what happens when the dispatcher, the merge gate, AND the bypass path are stacked."
- **Solo-maintainer posture amplifies the cost of a single compromised credential.** With one maintainer, the maintainer's GitHub session, `gh` CLI token, browser cookies, and dispatcher process are a single trust boundary. A compromise at any one point inherits the authority of the whole stack.
- **Subagent throughput is the velocity premise; a kill-switch is the price of throughput.** ADR 0042 is honest that "subagent failures don't land" because CI is the gate. That is true for honest failures (broken builds, syntax errors). It is NOT true for subtle logic regressions, prompt-injected bad code, or an attacker who controls the dispatcher.
- **The cost of getting this wrong is asymmetric and public.** A regression that lands quietly on a private repo is a follow-up PR. A regression that lands on the public OSS reference implementation associated with the book is a citable embarrassment AND a supply-chain incident for any future consumer who pinned a tag.
- **Pre-release, pre-LLC, single-maintainer is the most permissive lifecycle phase.** Every Revisit trigger across ADRs 0038-0042 names "second contributor joins" and "first v1 release ships" as posture-flip points. This ADR pre-stages those flips so the response isn't ad-hoc when they fire.
- **The controls below are cheap relative to the surface they close.** A kill-switch is a shell function. An audit-trail is a PR-template field. SHA-pinning is a one-time workflow rewrite. None of these require new infrastructure; the gap is policy + tooling, not platform.

---

## Considered options

### Option A — Per-ADR amendments only (what the council proposed at the per-ADR level)

Apply the council's per-ADR amendment text to ADRs 0038, 0039, and 0042. No new ADR; no system-level frame.

- **Pro:** Lowest documentation overhead. Each ADR holds its own complete picture.
- **Pro:** Future ADR-grep-by-topic finds the right ADR for the right concern.
- **Con:** The chain remains invisible. A future maintainer reading ADR 0039 still doesn't see that the 4-check minimum interacts with subagent auto-merge to permit unreviewed merges.
- **Con:** Cross-cutting controls (kill-switch, circuit-breaker, audit-trail) are operational primitives that span ADRs; they belong to the system, not to one ADR.
- **Rejected** for the cross-cutting concern. Per-ADR amendments are still recommended (and tracked separately); this ADR is the system-level layer ABOVE them.

### Option B — Single layered "unified threat model" ADR (this ADR)

Write one ADR that names the chain, catalogs threats T1-T5, and prioritizes the cross-cutting controls. ADRs 0038/0039/0042 remain unchanged as descriptions of their local decisions. ADR 0043 is the system-level frame.

- **Pro:** The chain is named once, in one place, where any future maintainer doing a security review will find it.
- **Pro:** The controls (HIGH/MEDIUM/LOW) are prioritized at the system level, not duplicated into each ADR.
- **Pro:** The Revisit triggers — LLC formation, second contributor, first incident, GitHub feature changes — are named at the system level so they fire on the right ADR cluster, not just one ADR.
- **Con:** Adds a layer of indirection. A reader of ADR 0039 needs to also read 0043 to understand the full picture.
- **Adopted.**

### Option C — Restructure ADRs 0038/0039/0042 to make the chain visible inside each

Edit each ADR to include a "this composes with ADRs X and Y to form the following surface" subsection.

- **Pro:** Maximum local readability — each ADR carries the system frame.
- **Con:** Three ADRs to edit, three places to keep the system frame in sync, three places where the frame can drift. The whole point of a layered ADR is to centralize the cross-cutting view.
- **Con:** Contradicts the council's recommendation to keep the chain visible as its own document so it can be revisited as a unit.
- **Rejected.**

### Option D — Defer the unified threat model until a real incident motivates it

Don't write the ADR; trust that the per-ADR amendments + general security hygiene cover the surface. Re-open if anything bad happens.

- **Pro:** Zero immediate work.
- **Con:** Optimizes for the wrong cost. Writing the ADR is hours; recovering from a chain-exploit incident on a public repo is days plus reputational cost. The ADR is cheap insurance.
- **Con:** "Trust that nothing bad happens" is not a security control.
- **Rejected.**

---

## Decision

**Adopt Option B: this ADR is the system-level threat model that frames the cross-cutting attack surface formed by ADRs 0038 + 0039 + 0042. It catalogs five threats (T1-T5), names what is mitigated TODAY vs what is a GAP, and prioritizes the controls to close the gaps as HIGH / MEDIUM / LOW. Per-ADR amendments from the council review remain recommended in their own right; this ADR sits above them.**

### The chain, drawn explicitly

The merge-path surface composes as follows:

| Layer | Source ADR | What it permits | What it constrains |
|---|---|---|---|
| Subagent dispatches a PR | 0042 | Anyone with controller access opens PRs | None at this layer |
| PR runs CI | 0039 | Auto-merge fires when 4 always-on checks pass | Other ~5 checks may fail or be skipped |
| Auto-merge merges to `main` | 0042 | No human review required | CI must be green |
| Bypass-actor merge | 0038 | Bypass list members merge regardless of CI | Audit log entry exists post-hoc |

**The cross-product:** the surface is `(subagent OR human) × (auto-merge OR bypass) × (good actor OR compromised credential) × (in-scope code OR out-of-scope-by-paths-filter code)`. The honest cells (good actor + auto-merge + green CI) are the productive throughput case. The dangerous cells (compromised credential + bypass; or compromised dispatcher + auto-merge through 4-check minimum) are the surface this ADR names.

### Threat catalog

#### T1 — Compromised maintainer account (gh CLI token leak, GitHub session hijack, social engineering)

**Attack:** The maintainer's GitHub credentials are compromised. The attacker uses `gh pr merge --admin` (or the equivalent UI flow) and merges arbitrary code to `main`, bypassing all required checks via the `bypass_actors` clause of the Ruleset.

- **TODAY:** Secret scanning + push protection are ON per PR #130 audit. Hardware-key 2FA is assumed at the GitHub-account level (out of scope for any ADR but operationally relied upon).
- **GAP:** No second-factor on admin merges. GitHub does not natively require step-up auth for `--admin` merges; once the session is hot, the attacker can merge freely. The `bypass_actors` list is not enumerated in `main-ruleset.json` with audit cadence (council REVISE on 0038). No drift-detection workflow watches for live-Ruleset edits via the Settings UI.
- **GAP:** No alerting on bypass-merge events. A bypass merge logs in GitHub's audit log but does not page the maintainer.

#### T2 — Compromised dependency (transitive npm / NuGet supply chain, malicious typosquat)

**Attack:** A transitive dependency in `package.json`, `*.csproj`, a dotnet workload, or a GitHub Actions step runs in CI under `GITHUB_TOKEN` and exfiltrates secrets, modifies workspace state, or opens malicious PRs from inside the build.

- **TODAY:** Dependabot security updates are ON. CodeQL + semgrep run on every PR. Workflows ban `pull_request_target` (per ADR 0037 / PR #129) so untrusted PR code does not run with privileged tokens.
- **GAP:** No SBOM is generated or verified at release. No transitive-dependency scanning beyond what semgrep + CodeQL catch (which is mostly source-code patterns, not dep-graph anomalies). No pin to known-good lockfiles enforced in CI for runtime dep changes.
- **GAP:** Dependabot proposes upgrades; once accepted, the new version runs on the next CI cycle. No "soak period" gate for new dependency versions.

#### T3 — Compromised subagent / hallucinated bad code

**Attack:** The dispatcher (or an attacker who has compromised the dispatcher) sends a brief that produces code which passes the 4 minimal required checks but is wrong: a subtle logic regression, a prompt-injected backdoor, or simply hallucinated nonsense that compiles. Auto-merge fires, the code lands, no human reviewed it.

- **TODAY:** The 4 required checks (Lint, Analyze csharp, CodeQL, semgrep) catch syntactic errors, common security patterns, and code-quality regressions. Worktree isolation per ADR 0042 prevents a runaway subagent from corrupting other in-flight work.
- **GAP:** Heavy gates (Build & Test, A11y Storybook, Global-UX Gate) are advisory per ADR 0039. A subagent-authored PR that fails Build & Test still auto-merges if Lint/CodeQL/semgrep/Analyze are green. The session-end-review mitigation (ADR 0039 Negative consequences) is a single-human habit that does not survive scale.
- **GAP:** No audit trail. Subagent-produced PRs do not currently carry a "this PR was authored by subagent agent-XYZ from brief BRIEF-NAME" marker. Forensics for "which dispatch produced this regression" is unrecoverable.
- **GAP:** No kill-switch. To halt subagent dispatch mid-flight requires manually disabling auto-merge on each in-flight PR and deleting each worktree. There is no single command that says "stop accepting subagent merges for the next N minutes."
- **GAP:** No circuit-breaker. Subagents that fail in a row do not back off; the dispatcher will keep dispatching even if (e.g.) every dispatch in the last hour has produced a CI-red PR.

#### T4 — Compromised CI infrastructure (GitHub Actions runner image, action version pinned to wrong SHA)

**Attack:** A third-party action used in a workflow (`actions/checkout`, `actions/setup-dotnet`, `actions/setup-node`, semgrep, CodeQL action, etc.) is compromised at the version we depend on. Workflows pinned to semver ranges (`@v5`, `@v6`) automatically pick up the malicious release on the next workflow run.

- **TODAY:** Workflows use major-version semver pins (`actions/checkout@v6`, `actions/setup-dotnet@v5`). This is GitHub's documented best-practice baseline. Permissions blocks per PR #132 minimize the `GITHUB_TOKEN` scope each workflow can use.
- **GAP:** Semver pinning is not SHA pinning. A compromised v5 release lands automatically. SHA-pinning every action (`actions/checkout@a1b2c3d4...`) is the GitHub-recommended hardening for high-integrity workflows; Sunfish does not do this today.
- **GAP:** No allowlist of permitted actions at the repo or org level. Anyone with workflow-edit permissions can introduce a new action; nothing audits the action choice.

#### T5 — Insider threat / disgruntled future contributor (post-LLC + multi-maintainer scenario)

**Attack:** A second (or Nth) contributor with merge authority on `main` opens a PR that introduces a backdoor, exfiltrates customer data via a deployed accelerator, or sabotages release artifacts. With current `required_approving_review_count: 0`, a bad-actor contributor with merge authority can self-merge.

- **TODAY:** Single-maintainer; T5 is not exploitable because no other contributor exists.
- **GAP (activates when contributor #2 joins):** `required_approving_review_count: 0` accommodates the single-maintainer case. With a second contributor, this floor MUST rise to ≥ 1, and the `pull_request` Ruleset rule should require code-owner approval on security-sensitive paths (CI workflows, ADRs, Ruleset JSON itself). None of this is pre-staged today.
- **GAP:** No off-boarding runbook for removing a contributor's bypass-actor entry, revoking PATs, rotating shared secrets. ADR 0038's bypass-actor audit cadence (recommended in the council review) would catch a stale entry; the runbook for the active removal is missing.

### Controls — prioritized

#### HIGH (do before next high-velocity session)

**H1. Kill-switch for autonomous subagent dispatch.** A single command (e.g., `infra/dispatch/halt-subagent-merges.sh [--minutes N]`) that:

1. Disables `gh pr merge --auto` on all currently-open subagent-authored PRs (identified by the audit-trail marker H2 below).
2. Sets a flag the dispatcher checks before opening the next subagent PR; flag expires after N minutes (default 30).
3. Logs the halt event to `.wolf/buglog.json` or a dedicated dispatch-events log.

**Rationale:** Closes the T3 gap "no kill-switch." Cheap to build (shell script + flag file). Without this, halting a runaway dispatch requires N manual `gh pr merge --disable-auto` calls + N worktree deletions, which is too slow under stress.

**H2. Audit-trail for subagent-authored PRs.** Every subagent PR body MUST include a structured marker:

```
<!-- subagent-dispatch
agent-id: agent-<id>
brief: <_shared/engineering/subagent-briefs/foo.md OR inline-summary>
dispatched-at: <ISO-8601 timestamp>
controller-session: <session-id-if-available>
-->
```

Enforcement: PR template + commitlint check (or a separate workflow) that fails when a PR appears to come from a worktree but lacks the marker. Heuristic for "appears to come from a worktree": branch name pattern `worktree-agent-*`.

**Rationale:** Closes the T3 gap "no audit trail." Without this, "which brief produced this regression?" is unrecoverable forensics. The marker also makes T1 (compromised dispatcher) detectable post-hoc — a flood of subagent-authored merges is a signal even if individually each looked fine.

**H3. Circuit-breaker on consecutive subagent failures.** The dispatcher tracks the outcome of recently-dispatched subagents and HALTS dispatch when EITHER:

1. Three consecutive subagent dispatches end in CI-red, OR
2. Subagent-failure rate exceeds 50% over the last 6 dispatches.

When tripped, the controller pauses and surfaces the failure pattern. Resume requires explicit human action.

**Rationale:** Closes the T3 gap "no back-pressure." Aligns with the council's per-ADR amendment for ADR 0042. Without back-pressure, a degenerate dispatcher loop (e.g., a buggy brief template) can flood the PR queue.

#### MEDIUM (within 2 weeks)

**M1. SHA-pin every third-party action in `.github/workflows/*.yml`.** Replace `actions/checkout@v6` with `actions/checkout@<sha>` (current trusted SHA, with a `# v6.x` trailing comment). Use Dependabot's `package-ecosystem: github-actions` to PR SHA bumps when the upstream releases a new tag.

**Rationale:** Closes T4. SHA-pinning is GitHub's documented hardening for workflows handling secrets or merging code. Sunfish's workflows do both.

**M2. SBOM generation + verification on every release.** Generate an SBOM (SPDX or CycloneDX) at release time and publish it as a release artifact. CI verifies the SBOM matches the actual dep graph before publishing.

**Rationale:** Closes T2 (transitive dep visibility). SBOM also satisfies the supply-chain expectations of any future commercial customer (Bridge SaaS path per ADR 0031).

**M3. Live-ruleset drift detection workflow.** A nightly workflow that fetches the live Ruleset via the API and diffs it against `infra/github/main-ruleset.json`. On diff, opens an issue and tags the maintainer.

**Rationale:** Closes the T1 gap "no drift detection on Settings-UI edits." Council recommended this on ADR 0038 as a POLISH; surfaced here as MEDIUM because it is also the only mechanical detection of T1's bypass-actor mutation.

**M4. Bypass-merge alerting.** A workflow (or a webhook + simple notifier) that fires whenever a merge to `main` did NOT have all 4 required checks pass. The maintainer gets a notification within minutes; if the bypass was unintentional, recovery is immediate; if it was T1, the response window is short.

**Rationale:** Closes the T1 gap "no alerting on bypass-merge events."

#### LOW (when contributor #2 joins, OR by 2026-Q4 regardless)

**L1. `required_approving_review_count` flips from 0 to 1.** Today's `0` is a single-maintainer accommodation per ADR 0038 Revisit trigger #3. When contributor #2 joins, this MUST flip; pre-staging it here means the Ruleset PR is one-line.

**L2. `CODEOWNERS` file with security-sensitive path coverage.** At minimum: `.github/workflows/*`, `infra/github/*`, `docs/adrs/*`, anything matching `*ruleset*.json`. Code-owner approval becomes required on these paths via the Ruleset's `pull_request` rule.

**L3. Off-boarding runbook.** A `docs/runbooks/contributor-offboarding.md` that enumerates: remove from `bypass_actors`, remove from `CODEOWNERS`, rotate any shared secrets the contributor had access to, revoke their PATs from the repo, audit recent merges by that contributor.

**Rationale for the cluster:** All three close T5. None are exploitable today (no contributor #2). All three are cheap to add but have no urgency until the trigger fires; the LOW priority reflects that.

### What this ADR does NOT do

- It does NOT amend ADRs 0038, 0039, or 0042 directly. Those amendments are recommended in the council review and tracked separately. This ADR sits above them.
- It does NOT mandate that every control above ship in one PR. The HIGH controls (H1, H2, H3) are in scope for the next high-velocity session; MEDIUM is 2-week scope; LOW is trigger-gated.
- It does NOT extend to T6+ threats not currently on the surface. Threats this ADR explicitly does NOT cover: malicious accelerator-app deployment (covered separately by ADR 0026/0031), long-tail cryptographic threats (covered by ADR 0004), CRDT-engine compromise (covered by ADR 0028).
- It does NOT pre-decide what happens when GitHub adds new branch-protection or ruleset features. That is a Revisit trigger.

---

## Consequences

### Positive

- The chain of permissiveness is named, in one place, where future security reviews will find it. Per-ADR readers can still read 0038/0039/0042 in isolation; system-level readers find the frame here.
- The HIGH controls (kill-switch, audit-trail, circuit-breaker) are scoped tightly enough that they can be built in one focused session. Each closes a named gap; each is independently testable.
- The MEDIUM controls (SHA-pinning, SBOM, drift detection, bypass alerting) have clear deliverables and clear gaps they close. None are vapor.
- The LOW controls are pre-staged for the trigger that activates them. When contributor #2 joins, the response is "open this one PR" not "design the response from scratch."
- Threat catalog (T1-T5) gives future ADRs a referenceable taxonomy. New threats can be added as T6, T7, etc., without renumbering.
- The "what this ADR does NOT do" section pre-empts scope creep — every threat on the public-OSS surface is named here OR pointed at the ADR that owns it.

### Negative

- Adds a layer of indirection. A reader of ADR 0039 now needs to also read 0043 to understand the full system surface. Mitigated by the ADR cross-references at the bottom of each affected ADR (and the README index entry).
- Risks becoming stale faster than its sibling ADRs because it depends on the state of three other ADRs simultaneously. Any change to 0038/0039/0042 should trigger a re-read of 0043 to confirm the chain analysis still holds. The Revisit triggers below codify this.
- HIGH controls require dispatcher-side tooling that does not exist today. Building H1-H3 is real work. The cost is justified by the surface they close, but it is not zero.
- Some controls (M3 drift detection, M4 bypass alerting) require GitHub API polling or webhooks; these introduce their own operational surface (the polling job, the notifier endpoint). This is acceptable at current scale but each adds a small surface.
- The threat model is enumerated for the current solo-maintainer + pre-LLC + pre-v1 phase. Each posture flip (LLC, v1, contributor #2) requires re-deriving the catalog. Tracked as Revisit triggers.

---

## Revisit triggers

This ADR should be re-opened — and the threat catalog re-derived — when **any one** of the following occurs:

1. **LLC formation.** Per the user's `project_sunfish_private_until_llc` memory, the repo flips to private when the LLC forms. Threat T2 (supply-chain) and T4 (CI-action compromise) become the dominant surface; T1 (compromised maintainer) becomes higher-impact because there is now commercial value behind the credential. Re-derive T1-T5 under the new posture.
2. **Contributor #2 joins.** T5 (insider threat) becomes exploitable. The LOW controls (L1-L3) flip from "pre-staged" to "MUST ship in the same PR that grants contributor #2 their bypass-actor entry." Re-evaluate H1-H3 (kill-switch / audit / circuit-breaker) for whether the multi-contributor pattern needs additional controls.
3. **First v1 release ships.** Per-PR scrutiny rises post-v1. Some advisory checks (Build & Test, A11y Storybook) should be promoted to required per ADR 0039's three-condition test. T3's "subagent regression escapes the 4-check floor" gap shrinks because the floor itself rises.
4. **A real T1-T5 incident occurs.** A compromised credential, a dependency-supply-chain incident, a subagent producing damaging code that auto-merged, a CI-action compromise, or an insider-threat event. Post-mortem feeds back into this ADR; new threat (T6+) added if the incident exposed a class this ADR did not name.
5. **GitHub adds new branch-protection / ruleset features** that materially change the chain. Specific watches: path-aware required-check semantics (would shrink T3's heavy-gate-advisory gap); per-PR-author bypass denial (would close T1's bypass override); native action-allowlist enforcement (would close T4).
6. **Subagent harness changes materially.** A new background-process model, a different worktree semantics, a different `gh` auth model, or a different completion-notification primitive. Re-derive H1-H3 against the new harness.
7. **At minimum every 6 months regardless of triggers.** Forces a re-read so this ADR doesn't ossify. Confirm the chain analysis still matches the live state of 0038/0039/0042 and the listed controls' status.

---

## References

### ADRs in the chain

- [ADR 0038](./0038-branch-protection-via-rulesets.md) — Branch protection via GitHub Rulesets. Defines the bypass-actor model that T1 exploits.
- [ADR 0039](./0039-required-check-minimalism-public-oss.md) — Required-check minimalism. Defines the 4-check floor that T3 routes around.
- [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — Subagent-driven development. Defines the auto-merge dispatch loop that T3 exploits.

### Related ADRs

- [ADR 0037](./0037-ci-platform-decision.md) — CI platform decision. T2 and T4 are GHA-specific in encoding (would re-derive on Forgejo Actions).
- [ADR 0040](./0040-translation-workflow-ai-first-3-stage-validation.md) — AI-first translation. Has its own AI-output trust model; T3 here is about code, not strings, so the surfaces are adjacent but distinct.
- [ADR 0041](./0041-dual-namespace-components-rich-vs-mvp.md) — not on the chain; no security implication for the surface this ADR names.
- [ADR 0018](./0018-governance-and-license-posture.md) — governance baseline that this ADR's posture-flip triggers (LLC, contributor #2) inherit from.

### Source review

- [`waves/governance/2026-04-26-council-review-adrs-0037-0042.md`](../../waves/governance/2026-04-26-council-review-adrs-0037-0042.md) — the 5-seat adversarial council pass that surfaced the chain. The "Cross-ADR recommendation — unified threat model" section recommends this ADR by name and provisional number.

### Foundational

- [`_shared/product/local-node-architecture-paper.md`](../../_shared/product/local-node-architecture-paper.md) — the paper Sunfish implements. The security-posture sections (managed-relay model, kernel/plugin trust boundary) frame the surface this ADR catalogs.

### Related security PRs

- PR #129 — workflow-injection defense (banned `pull_request_target`).
- PR #130 — secret-scanning + push-protection audit; established the T1 baseline mitigations.
- PR #132 — minimal `permissions:` blocks on every workflow; reduces the T2/T4 blast radius.
- PR #133 — auto-merge scope audit; established that CI is the sole automation gate (which is the premise the chain exploits when CI is the 4-check minimum).
- PR #138 — trimmed required-checks list to 4 always-on entries; the merge that materialized ADR 0039's posture.

### External

- GitHub Rulesets API: `https://docs.github.com/en/rest/repos/rules`.
- GitHub Actions security hardening (SHA-pinning guidance): `https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions`.
- SLSA supply-chain framework (informs M2 SBOM control): `https://slsa.dev/`.
- OpenSSF Scorecard (related; informs MEDIUM controls): `https://securityscorecards.dev/`.

### Memory

- User's `project_sunfish_private_until_llc` — the public→private flip that fires Revisit trigger #1.
- User's `feedback_pr_push_authorization` — the PR-with-auto-merge convention that the chain depends on.
- User's `reference_github_actions_paths_schema` — related GHA workflow-loading hazard, distinct from the chain analysis here.
