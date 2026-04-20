# Code Review

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Governs:** Code review language and process for Sunfish PRs — how reviewers write comments, what blocks merge, how fast the BDFL responds.
**Companion docs:** [commit-conventions.md](commit-conventions.md), [planning-framework.md](planning-framework.md), [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [ci-quality-gates.md](ci-quality-gates.md).

Sunfish is a remote, async, pre-community OSS project. Every reviewer comment is read without tone-of-voice, without shared whiteboard, and often across several time zones. This doc sets the convention reviewers follow so the *shape* of a comment carries the same information its words do.

[`CONTRIBUTING.md`](../../CONTRIBUTING.md) describes the PR mechanics. [`.github/pull_request_template.md`](../../.github/pull_request_template.md) carries the quality checklist. This document is about **how reviewers write comments** — the conventions that keep review fast, kind, and unambiguous.

## Why a tone standard

Async review fails without one. A comment like *"this could be better"* lands as anything from a passing thought to a merge-blocker depending on who reads it and when. Authors then either over-correct (wasting hours on what the reviewer meant as an idea) or dismiss (shipping something the reviewer meant to block). Both outcomes produce a re-review loop that the comment was supposed to avoid.

Two explicit fixes:

- **Label the intent.** Whether a comment is a question, a nit, an idea, or a block is encoded in the comment itself.
- **Mark blocking vs. non-blocking.** Nothing merges over an unresolved block; nothing is held hostage by an un-marked thought.

Google's [Code Review Developer Guide](https://google.github.io/eng-practices/review/), Rust's `rfcbot` conventions, and the Kubernetes review guide all converge on this pattern for the same reason. Sunfish adopts the [Conventional Comments](https://conventionalcomments.org/) spec as the concrete form.

## Conventional Comments adoption

Every PR comment on the Sunfish repo uses the [Conventional Comments v1.0](https://conventionalcomments.org/) format:

```
<label> [decorators]: <subject>

<optional discussion>
```

- **label** — one of the labels below, lower-case, no leading emoji.
- **decorators** — zero or more of `(non-blocking)`, `(blocking)`, `(if-minor)`, comma-separated in parentheses.
- **subject** — a single sentence. What you want the author to see first.
- **discussion** — optional paragraph(s) with reasoning, references, sample diffs.

The spec is unmodified. Sunfish does not invent new labels or decorators.

## The label set

Sunfish uses the full Conventional Comments label set. Pick the narrowest one that fits.

| Label | Meaning | Sunfish example |
|---|---|---|
| `nit` | Trivial preference. Author can decline without justification. | `nit: could fold this into the existing using block above.` |
| `suggestion` | Propose a concrete change, with a reason. | `suggestion: extract the tenant-resolution branch into TryResolveTenant so the dispatcher stays under 40 lines.` |
| `question` | Ask for clarification. Often becomes a `suggestion` or `issue` once answered. | `question: is the StringComparer.Ordinal here deliberate? TenantKey elsewhere uses OrdinalIgnoreCase.` |
| `thought` | Non-actionable idea. Never blocks. Use freely. | `thought: a source generator over the bundle manifests could kill this reflection path in a later pass.` |
| `todo` | Small required change — a nit that must land before merge. | `todo: add the XML doc summary; TreatWarningsAsErrors will otherwise fail CI.` |
| `praise` | Acknowledge good work. See §"The praise label". | `praise: the FAILED condition in this ADR is sharper than the ones we've been shipping. More of this.` |
| `issue` | Blocking problem. Must resolve before merge. Pair with a `suggestion`. | `issue (blocking): Blazor types leaked into packages/foundation — see CONTRIBUTING §Package Architecture.` |
| `chore` | Housekeeping (rename, reformat, move, update a stale reference). | `chore: rename TenantCtx to TenantContext to match naming.md §Types.` |
| `typo` | Spelling/grammar. Non-blocking unless in a public API name. | `typo: "catagory" → "category" in BundleCategoryKeys.` |

Unlabeled comments are a smell. If you catch yourself writing one, pick a label — `thought` is always valid when nothing else fits.

## Decorators

Three decorators, all from the Conventional Comments spec:

| Decorator | Meaning | When to use |
|---|---|---|
| `(non-blocking)` | Must not prevent merge. | On any `nit`, `thought`, `praise`, `question` that's curiosity-only. Also fine on `suggestion` when you're flagging an improvement you'd accept either way. |
| `(blocking)` | Must be resolved before merge. | On `issue` always; on `todo` when the todo is a merge-gate (e.g. missing XML doc, missing parity test). Rare on other labels. |
| `(if-minor)` | Author resolves only if the fix is trivial; otherwise punt to a follow-up issue. | On `suggestion` or `chore` where the idea is worth landing but not worth rerunning CI for. |

Default polarity: **`nit`, `thought`, `praise`, `typo`, `question`** are non-blocking unless decorated otherwise. **`issue`** is blocking unless decorated otherwise. **`suggestion`, `todo`, `chore`** are advisory unless decorated — mark them explicitly.

## The praise label

Sunfish explicitly encourages `praise` comments. Remote async review without them becomes a grim drip of corrections that burns reviewer-author goodwill and drives turnover. There is no threshold — *"praise: nice test coverage here"* is a complete, useful comment. Aim for at least one praise per review when the work warrants it. Don't manufacture it when it doesn't.

What good praise looks like:

- **Specific.** `praise: the FAILED conditions on this plan are concrete enough to actually trip on.` beats *"great work!"*.
- **Unconditional.** Don't sandwich praise between two critiques to soften them; mix it in naturally across the review.
- **About the work, not the person.** `praise: this refactor removed the last ConfigureAwait(false) omission I was tracking.` beats *"you're great at refactoring."*

## Examples from Sunfish-shaped PRs

### On an ADR PR

```
question (non-blocking): does this ADR conflict with ADR 0014's
"all features available in all adapters" rule? The exception here feels
worth calling out explicitly in the Consequences section.

suggestion (if-minor): add a backlink from ADR 0014 once this merges,
so future readers find the exception from either direction.

praise: the FAILED conditions under "When to revisit this decision"
are concrete — "React parity slips more than one release cycle behind"
is the kind of trigger we can actually catch.
```

### On a component migration PR (Blazor → Web Components)

```
issue (blocking): SunfishDataGrid still takes a RenderFragment
parameter in ui-core — this violates the framework-agnostic contract in
packages/foundation and ui-core (see CONTRIBUTING.md §Package Architecture).
suggestion: move RenderFragment to the ui-adapters-blazor wrapper and
expose a slot-style contract from ui-core.

todo (blocking): add the bUnit parity test the adapter-parity policy
requires (ADR 0014).

nit: column-menu.razor.css has two nearly-identical .sf-menu-item rules;
fold them.
```

### On a bundle-manifest update PR

```
issue (blocking): the new category "operations-intel" isn't in
BundleCategoryKeys — commit will break the catalog validator.

chore: filename is leases-renewal.json but the bundle id inside is
lease-renewal. Pick one (naming.md prefers the singular).

thought: once we have three manifests using this same overlay block,
we should promote it to a shared fragment file — not this PR.

praise: nice that the schema $id is stable and the version bumped
cleanly per ADR 0011.
```

## What reviewers look for

Reviewer scope for a Sunfish PR, in rough order:

1. **Architectural fit** — matches the principles in [`../product/architecture-principles.md`](../product/architecture-principles.md); no framework types leaking into `foundation`/`ui-core`.
2. **ICM stage alignment** — the PR links the ICM stage output it came from, or explicitly marks itself accelerated per [CONTRIBUTING §ICM Pipeline](../../CONTRIBUTING.md#icm-pipeline).
3. **UPF anti-pattern scan** — the Stage-2 checks from [planning-framework.md](planning-framework.md) (Cold Start Test, FAILED conditions, Discovery Consolidation) hold for anything non-trivial.
4. **Test coverage** — per [testing-strategy.md](testing-strategy.md). Unit tests for the package's own logic; parity tests where adapter work is involved.
5. **Adapter parity** — per [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md). Blazor-only or React-only features require an explicit exception.
6. **Public API documentation** — XML docs on every new or changed public member per [coding-standards.md §XML documentation](coding-standards.md).
7. **Doc impact** — kitchen-sink demo + apps/docs updates for user-facing changes per [CONTRIBUTING §User-Facing Changes](../../CONTRIBUTING.md#user-facing-changes).
8. **Commit + PR title compliance** — per [commit-conventions.md](commit-conventions.md). The squash-merge subject is the commit that lands on `main`.
9. **CI quality gates** — see [ci-quality-gates.md](ci-quality-gates.md). Build, test, commitlint, CodeQL must all be green.

Reviewers don't have to cover everything in one pass, but the maintainer owns the final gate across all of them.

## Review cadence

Pre-community (current): the BDFL responds to open PRs within **5 business days** of the author marking them ready for review. Work-in-progress (draft) PRs have no SLA.

Post-community: once the maintainer tier activates (per [`GOVERNANCE.md` §Transition triggers](../../GOVERNANCE.md#transition-triggers)), first-review SLA tightens to **2 business days** and CODEOWNERS gets a scoped review assignment automatically. TSC-era review cadence will be set when the TSC forms.

If a PR is going to miss the SLA (travel, illness, conference week), the BDFL comments on the PR with an expected review date. Silent delay is a defect.

## When a comment blocks merge

A merge is blocked only when one of the following is true on the PR at merge time:

- An `issue` comment without `(non-blocking)`.
- Any comment carrying the `(blocking)` decorator.
- An explicit line in the PR description (or a maintainer comment) stating *"must resolve before merge"* with a reason.

Everything else — `nit`, `thought`, `praise`, curiosity-only `question`, undecorated `suggestion`, `typo` outside public API — is advisory. Authors may decline with a one-line rationale and merge.

Requesting changes via GitHub's native "Request changes" button is reserved for blocking comments. Using it for a nit defeats the signal.

## Who can leave blocking comments

**Pre-community:** only the BDFL and `.github/CODEOWNERS` entries can leave `(blocking)` comments on PRs. Anyone (including drive-by reviewers) can leave non-blocking feedback of any label, and it's read as seriously.

**Post-community:** any listed maintainer can block within their CODEOWNERS scope. Cross-scope blocking still routes through the BDFL (and later, the TSC) per [`GOVERNANCE.md`](../../GOVERNANCE.md).

This isn't about gatekeeping ideas — anyone's `suggestion` or `issue` without `(blocking)` still carries weight. It's about who can stop the merge button.

## Resolving comments

Authors own thread resolution. A thread is resolvable when one of:

- The author addressed it in a commit (link the commit SHA in the reply).
- The author explicitly declined with a one-line reason (`declining this nit: keeping the inline comparer for symmetry with the other catalogs.`).
- The reviewer closed it themselves (e.g., a `question` they answered without asking for change).

Unresolved `(blocking)` threads are a merge-time defect — the maintainer will not click merge while any remain. Unresolved non-blocking threads are fine; GitHub preserves them in the PR record.

Follow-ups for `(if-minor)` suggestions or deferred `thought` comments live in a new issue linked from the PR, not in a new comment on the closed PR.

## Cross-references

- [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) — PR mechanics, ICM linkage, user-facing-change rules.
- [planning-framework.md](planning-framework.md) — UPF anti-pattern checks that reviewers apply.
- [commit-conventions.md](commit-conventions.md) — commit + PR title format that ships with the squash merge.
- [ci-quality-gates.md](ci-quality-gates.md) — automated checks that run alongside human review.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — who decides, transition triggers that change review cadence.
- [ADR 0014](../../docs/adrs/0014-adapter-parity-policy.md) — adapter parity requirements reviewers enforce.
- [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) — governance posture this review model reports under.
- [Conventional Comments spec](https://conventionalcomments.org/) — upstream source of truth for the label set and decorator grammar.
- [Google Code Review Developer Guide](https://google.github.io/eng-practices/review/) — reference for reviewer-author etiquette norms Sunfish adopts.
