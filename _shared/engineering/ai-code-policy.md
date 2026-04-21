# AI-Assisted Contribution Policy

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** Every contribution to the Sunfish repo, whether AI-assisted or not — how to disclose AI involvement, how reviewers handle AI-authored changes, and who is accountable when something goes wrong.
**Companion docs:** [commit-conventions.md](commit-conventions.md), [code-review.md](code-review.md), [testing-strategy.md](testing-strategy.md), [coding-standards.md](coding-standards.md), [planning-framework.md](planning-framework.md), [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md), [vision §Pillar 4](../product/vision.md).
**Agent relevance:** Loaded by agents (and human sponsors) opening PRs. High-frequency — every commit discloses AI involvement and signs the DCO.

> **Not legal advice.** This is project policy, not counsel. Contributors remain responsible for complying with their employer's policies, their AI tool vendor's terms of service, and the laws of their jurisdiction.

## Sunfish's stance

**AI-assisted contributions are welcome.** Sunfish is AI-native by commitment (vision Pillar 4: *"AI-native development as a cost primitive"*) — prohibiting AI assistance would contradict the platform's own premise. But accountability stays with the human: the contributor who signs the DCO is the one asserting the change is fit to merge, regardless of whether an AI helped write it.

In one line: **pro-AI, pro-disclosure, pro-accountability.** Tests and code review are the quality gate regardless of authorship.

## Why this policy

Three questions come up about AI-assisted contributions. Sunfish answers each deliberately:

1. **Legal landscape** — still evolving in 2026. The U.S. Copyright Office (Part 2 of its AI report, January 2025) and the USPTO (November 2025 inventorship guidance) both require meaningful human contribution for protection; *Doe v. GitHub* (2024 N.D. Cal., under Ninth Circuit appeal) narrowed most training-data claims but kept open-source license-violation theories alive. Sunfish can't wait for the dust to settle. A pragmatic policy anchored on human accountability holds up under any plausible outcome of the cases still in flight.
2. **Technical quality** — solvable through normal review. AI assistants produce plausible-looking but sometimes wrong code; the same is true of tired humans. The fix is the same in both cases: tests, review, and the "I understand what this does" bar.
3. **Accountability** — solvable through DCO. The Developer Certificate of Origin v1.1 (developercertificate.org) already puts the submitting human on the hook. The Linux kernel's late-2025 `coding-assistants.rst` policy and the Apache Software Foundation's Generative Tooling Guidance v1.0 both converge on this model: AI may assist, but only a human signs off. Sunfish adopts the same anchor.

Prohibitionist policies (forbidding AI assistance entirely) are being tried by some projects and maintainer groups. Sunfish declines that posture: it's unenforceable at scale, incoherent with vision Pillar 4, and cuts off the contributor pool Sunfish is explicitly designed to welcome (AI-savvy stakeholders, see vision §"Who Sunfish is for").

## Three contribution modes

Mode is determined by how much of the change the AI produced, not by which tool the contributor used.

### Mode 1 — Routine AI assistance

Autocomplete, small snippet generation, one-line refactor suggestions, rename-variable proposals, boilerplate fill-in. Typical Copilot / Cursor / IDE-integrated use.

- **Disclosure:** not required.
- **Accountability:** the contributor, under DCO signoff on the commit.
- **Review:** standard code-review.md treatment. Reviewers do not need to know AI was involved.

### Mode 2 — Substantial AI generation

Whole files, tests, documentation sections, ADR drafts, bundle manifests, or multi-hundred-line blocks produced by an AI assistant and then reviewed and edited by the human contributor.

- **Disclosure:** encouraged in the commit body. Use either a `Co-authored-by:` trailer (preferred when the assistant has a stable identifier) or a one-line note:
  ```
  Assisted-by: Claude Sonnet 4.6 (Claude Code)
  ```
  This follows the Apache Software Foundation's `Generated-by:` convention and the Linux kernel's `Assisted-by:` tag, adapted to Sunfish's Conventional-Commits footer grammar.
- **Accountability:** the contributor, under DCO signoff. The AI assistant does not sign off and cannot.
- **Review:** standard code-review.md treatment, but reviewers may reasonably ask the contributor to explain any section. See §"Review standards".

### Mode 3 — Autonomous agent contribution

An AI agent (e.g., an unattended Claude Code or GitHub Copilot Workspace task) opens a PR directly.

- **Human sponsor required.** A named human must be on the PR, signs the DCO on the commits that land, and takes full accountability for the change.
- **Disclosure required** in the PR description. State the agent, model, and prompt/task source (e.g., *"This PR was authored by Claude Code running a `/loop` task against issue #123. Sponsor reviewed every file and ran the test suite."*).
- **Sponsor certification.** By signing off, the sponsor affirms they have read the diff, understood the change, run the build and tests locally, and can answer reviewer questions about any part of it.
- **No bulk unreviewed PRs.** See §"What's explicitly not allowed".

**Sponsor accountability rule:** The sponsor must (a) run `dotnet build Sunfish.slnx` and `dotnet test Sunfish.slnx` locally and confirm green before opening the PR, and (b) read the full diff — not just the summary the agent produced. If a sponsored commit ships a regression (test failure, build break, or behavior bug reported within 7 days of merge), it is reverted without ceremony. No process, no blame debate — revert, then re-land once the issue is understood. This rule exists because sponsorship is a human accountability mechanism, not a rubber-stamp mechanism. A sponsor who cannot explain what a section does should not sponsor it.

## Human accountability

The DCO signoff (`git commit --signoff`, producing `Signed-off-by:`) is the accountability hinge. It has been since 2004 (Linux Foundation, post-SCO disputes) and it still works for the AI era with one clarification: **the human signer is certifying the submission act, not that they typed every character**. What they must certify has not changed:

- (a) They have the right to submit the contribution under the project's license, or
- (b) The contribution is based on work covered by a compatible license and they have the right to submit it under that license, and
- (c) Every person mentioned in `Signed-off-by` / `Co-authored-by` was contacted and agreed, and
- (d) They understand the contribution is public and permanent.

Two concrete consequences when AI assisted:

1. **Contributors cannot sign off on code they have not understood.** If you cannot explain a line to a reviewer, you cannot certify it under DCO. Delete it, or replace it with code you do understand.
2. **Contributors warrant their tool's output is submittable.** If an AI assistant produced verbatim chunks of code from a restrictive upstream license (GPL when Sunfish is MIT, a paid dataset under a no-redistribution clause, etc.), the human signer is the one warranting that didn't happen — not the tool vendor.

## What every contributor MUST do, with or without AI

The baseline is identical for AI-assisted and hand-written contributions:

- **Tests.** Per [testing-strategy.md](testing-strategy.md). Unit tests for new logic; adapter-parity tests when adapter code changes; integration tests where contracts cross packages.
- **Coding standards.** Per [coding-standards.md](coding-standards.md). `Nullable=enable`, XML docs on public members, `TreatWarningsAsErrors=true` — the build must be warning-free regardless of who wrote the code.
- **Build and test locally** before opening the PR. CI is not the first pass.
- **Conventional Commits + DCO** per [commit-conventions.md](commit-conventions.md). `git commit -s` adds the signoff.
- **Explain on request.** The reviewer's "can you walk me through this?" is a reasonable ask. Inability to answer = `issue (blocking)` per code-review.md.

If any of these bullets can't be satisfied for an AI-assisted change, the change isn't ready — same as for a hand-written change.

## Acceptable AI assistants

Sunfish is **tool-agnostic**. Contributors may use any AI assistant that fits their workflow — Claude (Anthropic), GitHub Copilot (Microsoft/OpenAI), Cursor, Cody (Sourcegraph), Gemini Code Assist, local models (Llama, DeepSeek Coder, Qwen), or anything else. There is no allowlist and no denylist.

Contributors warrant two things when using any tool:

- **Tool terms compliance.** You are using the tool within the terms of service you agreed to. OpenAI and Anthropic both assign output ownership to the user; Copilot's terms are documented; Google and Microsoft's enterprise tiers ship explicit no-training commitments. It is the contributor's job to know which tier they're on and what the terms say.
- **Upstream training-data disputes are the vendor's problem, not Sunfish's.** If an unresolved case (e.g., the *Doe v. GitHub* remaining license-violation claim) later reshapes what Copilot users warrant, Sunfish will revisit this section. Until then, the contributor certifies their submission under DCO and the project accepts it.

## Review standards for AI-assisted code

Reviewers apply [code-review.md](code-review.md) standards regardless of authorship. Two specific sharpening points:

- **"Explain this" is a fair ask.** Reviewers may ask the contributor to explain any AI-authored section. The request itself is a `question`; a contributor who cannot answer turns it into `issue (blocking)` — not because AI was involved, but because DCO signoff requires understanding.
- **Plausible-but-wrong is the primary AI risk.** AI assistants generate code that passes the "looks right" test but fails on edge cases, uses deprecated APIs, invents function signatures, or misunderstands the surrounding package's conventions. Reviewers look for this class of error explicitly on AI-assisted PRs.

Reviewers should **not**:

- Reject a PR for being AI-assisted. Authorship is not a review criterion.
- Demand disclosure retroactively for Mode-1 routine assistance.
- Use stylometric or AI-detector tools to guess authorship. They are unreliable and incompatible with the policy's anchor on human accountability rather than human typing.

## Test coverage expectations

Elevated scrutiny applies to tests accompanying AI-generated code. AI assistants reliably produce tests that exercise the happy path, assert trivial post-conditions, and miss edge cases (null inputs, empty collections, concurrent access, boundary values, malformed payloads). Reviewers may flag thin test coverage under `issue (blocking)` per code-review.md.

The UPF Stage-1 `FAILED conditions` discipline (see [planning-framework.md](planning-framework.md)) applies to tests too: a test that can't fail on realistic bad inputs isn't testing, it's decoration.

## Attribution

Attribution is **optional for Mode 1**, **encouraged for Mode 2**, **required for Mode 3**. Two forms, both compatible with [commit-conventions.md](commit-conventions.md):

**`Co-authored-by:` trailer** — GitHub renders this in the commit UI and in contributor stats. Use it when the AI has a stable, citable identity:

```
feat(foundation-catalog): add bundle overlay resolver

Refs: #198
Signed-off-by: Jane Contributor <jane@example.com>
Co-authored-by: Claude Sonnet 4.6 <noreply@anthropic.com>
```

**`Assisted-by:` trailer** — a free-form note when `Co-authored-by` doesn't fit (tool chain, multiple assistants, IDE-embedded autocomplete aggregate):

```
fix(bridge): prevent double token refresh on 401 retry

Refs: #224
Signed-off-by: Jane Contributor <jane@example.com>
Assisted-by: GitHub Copilot + Cursor autocomplete
```

The human contributor is the accountable party in both forms. `Co-authored-by` on an AI does not make the AI a DCO signatory — only the `Signed-off-by` line does that, and it must be a human.

## AI-generated artifacts that are not code

Sunfish treats AI-generated *artifacts* as first-class inputs (vision Pillar 4). Policy:

- **Mermaid diagrams, SVG images, documentation prose.** Same review standards as code. Disclosure encouraged when substantial (Mode 2 equivalent).
- **ADR drafts.** Explicitly fine. AI-drafted ADRs go through the same Open Decision Framework ratification as human-drafted ones — the merge is the ratification (see [planning-framework.md §Decision-Making](planning-framework.md) and [GOVERNANCE.md](../../GOVERNANCE.md)). The ADR author in the frontmatter is the human who took responsibility for its content.
- **Bundle manifests and templates.** AI-generated manifests go through the same meta-schema validation (ADR 0007) and catalog checks (ADR 0011) as hand-written ones. The validator doesn't care who typed the JSON.
- **Tests generated from specifications.** Welcome. Subject to the elevated-scrutiny rule above.

## Security and trust

Three risks worth naming:

1. **Confident nonsense.** AI assistants generate plausible-looking but wrong code. Reviewers are the defense. Tests are the second defense. Neither can be skipped on AI-assisted PRs.
2. **Prompt injection.** Agents that read external inputs (dependency READMEs, fetched web pages, data files, issue comments written by third parties) can be steered by adversarial content embedded in those sources. Contributors using agent tools treat external inputs as untrusted the same way a server would — don't let an agent run unreviewed code, don't let it commit based on instructions from data it fetched, don't let it exfiltrate secrets.
3. **Training-data license bleed.** No code may be committed that was generated from training data in a way that violates the upstream license. AI vendors work to prevent this (filters, similarity detection); contributors warrant it under DCO. If a reviewer spots suspected verbatim reproduction of a recognizable upstream, flag it `issue (blocking)` and rewrite.

## What's explicitly not allowed

- **Signing off on code you haven't reviewed.** Applies equally to AI-assisted and human-written code. DCO requires understanding, not typing.
- **Bulk autonomous-agent PRs without a human sponsor.** An agent batch that opens 40 PRs from unreviewed output is a denial-of-review attack on maintainers. Sunfish follows the CPython Steering Council's posture (April 2026 meeting summary) and the Rust Foundation's current discussion (RFC 3936): unsupervised AI PR floods are closed without review.
- **Claiming AI-generated content as human-authored when attribution was requested.** If a reviewer asks "did AI help with this?", answer honestly. Deception here violates the Code of Conduct, not just this policy.
- **Proprietary AI output under terms incompatible with MIT redistribution.** If an AI tool's terms of service forbid open-source redistribution of its outputs, the contributor cannot submit those outputs to Sunfish. Default commercial terms of the major tools (OpenAI API, Anthropic API, Copilot Business) do not forbid this; consumer free tiers and experimental models sometimes do. Read the terms before contributing.
- **Use of AI to generate content that defeats a safety or policy layer** — e.g., fabricated test evidence, synthetic benchmark results presented as real measurements, or security-review assertions the AI produced without actually checking. Not specific to AI; already covered by the code of conduct and review honesty norms.

## Evolution triggers

This policy revises when any of the following fire:

- **Legal ruling changes the landscape.** A final *Doe v. GitHub* outcome, a binding Copyright Office regulation, EU AI Act enforcement specifics touching code contributions, or a comparable event.
- **First corporate contributor's legal team requires a CLA or AI-specific warranty.** Triggers the same revision path as the GOVERNANCE.md "first production corporate adopter" trigger.
- **Peer-project consensus shifts.** If the Linux kernel, Apache Software Foundation, or a comparable reference project materially tightens or loosens their AI policy, Sunfish reviews the delta.
- **Any [`GOVERNANCE.md` §Transition trigger](../../GOVERNANCE.md#transition-triggers)** fires. Governance evolution and contribution-policy evolution are coupled.

Like GOVERNANCE.md itself, this document evolves on named triggers rather than calendars. Each revision updates `Last reviewed` and enumerates what changed and why.

## Cross-references

- [vision.md §Pillar 4](../product/vision.md) — AI-native development as a cost primitive; the stance this policy implements.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — BDFL model, DCO requirement, transition triggers.
- [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) — contributor onboarding and PR mechanics.
- [planning-framework.md](planning-framework.md) — UPF (Stage-1 FAILED conditions, Stage-2 Cold Start Test, adversarial hardening) applies to AI-assisted plans the same way it applies to human-authored ones.
- [commit-conventions.md](commit-conventions.md) — Conventional Commits format, DCO signoff, `Co-authored-by` trailer grammar.
- [code-review.md](code-review.md) — Conventional Comments label set, blocking / non-blocking decorators, the "explain this" bar.
- [coding-standards.md](coding-standards.md) — nullable, XML docs, `TreatWarningsAsErrors`.
- [testing-strategy.md](testing-strategy.md) — unit, parity, and integration test expectations.
- [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) — governance and license posture that anchors this policy.
- [Developer Certificate of Origin v1.1](https://developercertificate.org/) — upstream text the DCO signoff references.
- [Linux Kernel `coding-assistants.rst`](https://docs.kernel.org/process/coding-assistants.html) — reference policy Sunfish's Mode 3 accountability model aligns with.
- [Apache Software Foundation Generative Tooling Guidance](https://www.apache.org/legal/generative-tooling.html) — reference policy Sunfish's disclosure convention aligns with.
