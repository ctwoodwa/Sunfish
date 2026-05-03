# Effort + Model Selection Policy for Sunfish

This file governs how Claude Code's reasoning effort and model selection are
configured for work in the Sunfish repository. The project sets a baseline so
contributors do not have to type `/effort` or `/model` each session, and so
the choice of effort matches the kind of work Sunfish actually demands.

Canonical references:
- Effort: <https://platform.claude.com/docs/en/build-with-claude/effort>
- Model selection: <https://platform.claude.com/docs/en/about-claude/models/overview>

---

## Model positioning (canonical)

| Model | Canonical description | Pricing (in / out, per MTok) | Latency | Context | Max output | Knowledge cutoff |
|---|---|---|---|---|---|---|
| **Opus 4.7** | "Most capable generally available model for complex reasoning and agentic coding" — step-change improvement in agentic coding over 4.6 | $5 / $25 | Moderate | 1M | 128k | Jan 2026 |
| **Sonnet 4.6** | "The best combination of speed and intelligence" | $3 / $15 | Fast | 1M | 64k | Aug 2025 |
| **Haiku 4.5** | "The fastest model with near-frontier intelligence" | $1 / $5 | Fastest | 200k | 64k | Feb 2025 |

**Sunfish-specific selection notes:**

- **Default to Opus 4.7 for build sessions.** The canonical "step-change
  improvement in agentic coding" claim plus the Jan 2026 knowledge cutoff
  make Opus 4.7 the right default for Sunfish work touching .NET 11
  preview, MAUI 10 preview, EFCore preview, Aspire preview — areas where
  Sonnet's Aug 2025 cutoff or Haiku's Feb 2025 cutoff would miss recent
  API changes.
- **Sonnet 4.6 for cost-bound work.** ~60% of Opus output cost; same
  context window (1M); same `medium` default story. Use for routine
  builds, doc-only changes, mechanical refactors.
- **Haiku 4.5 has a 5× smaller context window (200k vs 1M).** Don't use
  it for sessions that read large parts of the Sunfish codebase — even
  a routine cross-package status query may overflow. Effort parameter
  is also unsupported on Haiku, so you can't tune token spend the
  normal way. Reach for Sonnet 4.6 + `low` instead.
- **Opus 4.7 uses adaptive thinking** (no manual `budget_tokens` knob);
  effort IS the thinking-depth control. That's why setting `effortLevel`
  in `.claude/settings.json` is the right place to encode the project
  policy on Opus 4.7.

---

## What the effort parameter actually does

Quoting the canonical doc:

> The effort parameter affects **all tokens** in the response, including
> text responses and explanations, tool calls and function arguments,
> and extended thinking. Lower effort means Claude makes fewer tool calls,
> combines operations, and skips preamble. Higher effort means more tool
> calls, more planning, more detailed summaries.

That all-token reach matters for Sunfish, where most sessions chain dozens
of `Read` / `Grep` / `Edit` / `Bash` calls per turn. Effort isn't just a
"think harder" knob — it's a tool-call-density knob too.

---

## Project default

`.claude/settings.json` declares:

```json
{
  "effortLevel": "xhigh"
}
```

The Claude Code API default is `high` (equivalent to not setting effort).
Sunfish overrides to `xhigh`.

**Why `xhigh` (canonical rationale, Opus 4.7-specific):**

> Start with `xhigh` for coding and agentic use cases, and use `high` as
> the minimum for most intelligence-sensitive workloads. Step down to
> `medium` for cost-sensitive workloads, or up to `max` only when your
> evals show measurable headroom at `xhigh`.
>
> `xhigh` — The recommended starting point for coding and agentic work,
> and for exploratory tasks such as repeated tool calling, detailed web
> search, and knowledge-base search. Expect meaningfully higher token
> usage than `high`.

Sunfish work fits this description: multi-package refactors, paper-
alignment waves, ICM stage transitions, ADR drafting, conformance scans,
MVP-phase build sessions. Most are 30+ minute sessions with budgets in
the millions of tokens — exactly what `xhigh` is for.

**Why not `max` as default:**

> Reserve for genuinely frontier problems. On most workloads `max` adds
> significant cost for relatively small quality gains, and on some
> structured-output or less intelligence-sensitive tasks it can lead to
> **overthinking**.

Reserve `max` for: a single deliberately-scoped deep-reasoning slot the
user opts in to (e.g., a stuck correctness-critical bug in concurrency,
crypto, or consensus paths where `xhigh` has demonstrably hit a wall).

**Model coupling — important:**

| Level | Models that support it |
|---|---|
| `max` | Mythos Preview, Opus 4.7, Opus 4.6, Sonnet 4.6 |
| `xhigh` | **Opus 4.7 only** |
| `high` | Mythos Preview, Opus 4.7, Opus 4.6, Sonnet 4.6, Opus 4.5 (API default on each) |
| `medium` | Same as `high` |
| `low` | Same as `high` |
| (effort unsupported) | **Haiku 4.5** — effort parameter is a no-op |

The project default `xhigh` only takes effect when you're on Opus 4.7. If
you `/model sonnet` mid-session, set `/effort medium` explicitly per the
Sonnet-specific guidance (next section). Don't rely on the project default
to mean the same thing across models.

---

## When to downgrade per session

Override the project default with `/effort <level>` (and optionally
`/model <name>`) at the start of a session that doesn't need elevated
reasoning. Use this rubric:

| Work type | Effort | Model | Notes |
|---|---|---|---|
| Correctness-critical debugging where `xhigh` has hit a wall | `max` | Opus 4.7 | Only when evals show measurable headroom; otherwise `xhigh` is the right ceiling |
| Architecture, ADR drafting, gap analysis | `xhigh` (default) | Opus 4.7 (default) | Wave-N planning, paper-alignment, ICM Stage 02/03/05 |
| Substantive code review | `xhigh` (default) | Opus 4.7 (default) | Security-critical PRs, council reviews, cross-package change |
| Multi-step plan execution requiring judgment | `xhigh` (default) | Opus 4.7 (default) | MVP Phase work, novel feature, breaking-change rollout |
| Implementation from a clear, complete plan | `high` | Opus 4.7 | Stage 06 build with exact spec already pinned in Stage 05 — saves on token budget vs `xhigh` |
| Cost-sensitive Sonnet workflow | `medium` | Sonnet 4.6 | Canonical Sonnet 4.6 default. Suitable for agentic coding, tool-heavy workflows, code generation |
| Latency-sensitive Sonnet workflow | `low` | Sonnet 4.6 | High-volume or chat-style; canonical Sonnet 4.6 low-effort use case |
| Routine bug fix with clear repro | `medium` | Sonnet 4.6 | Single-file patch, commitlint subject fix |
| Mechanical edits | `low` | Sonnet 4.6 | Renames, format fixes, dependency bumps — `low` already includes "Proceed directly to action without preamble" per the canonical doc |
| Doc-only changes | `low` | Sonnet 4.6 | README updates, ADR copyedit, changelog entries |
| Status / info queries | `low` | Sonnet 4.6 | "What PRs are open?", "What's the status of X?" — don't use Haiku 4.5; effort is a no-op there |
| Subagent dispatch (default) | `low` | varies by subagent | Canonical: low is for "Simpler tasks that need the best speed and lowest costs, such as subagents" |

**How to switch per session:**

```text
/effort high
/model sonnet
```

These apply to the current session only. The project default reasserts on
the next session.

**Don't downgrade when:**
- The task touches more than one package and you can't predict the blast
  radius until you start
- The task is a breaking change (anything in an `api-change` ICM pipeline
  variant)
- The task involves crypto, key handling, or security primitives
- You're not sure — staying on `xhigh` is cheaper than fixing a flawed
  result

**Sonnet 4.6 has a different default story.** Per the canonical doc:

> Sonnet 4.6 defaults to `high` effort. Explicitly set effort when using
> Sonnet 4.6 to avoid unexpected latency. **Medium effort (recommended
> default):** Best balance of speed, cost, and performance for most
> applications.

So when you switch to Sonnet, the right effort is `medium` (not `high`,
even though `high` is what you'd inherit). Always pair `/model sonnet`
with an explicit `/effort medium` (or `low` for latency-bound work).

---

## ICM stage → effort mapping

For routine ICM work, use the project default unless you have a specific
reason to downgrade. Approximate guidance:

| ICM Stage | Effort | Model | Notes |
|---|---|---|---|
| `00_intake` | `low` | Sonnet 4.6 | Classification, scope identification — lightweight, canonical low-effort fit |
| `01_discovery` | `xhigh` (default) | Opus 4.7 (default) | Research, dependency mapping, impact analysis |
| `02_architecture` | `xhigh` (default) | Opus 4.7 (default) | Design judgment — never downgrade |
| `03_package-design` | `xhigh` (default) | Opus 4.7 (default) | API design — never downgrade |
| `04_scaffolding` | `medium` | Sonnet 4.6 | Generator/template work — mechanical-ish |
| `05_implementation-plan` | `xhigh` (default) | Opus 4.7 (default) | Decomposition + sequencing judgment |
| `06_build` | `high` to `xhigh` (default) | Opus 4.7 to Sonnet 4.6 | `xhigh` if novel; `high` if plan is exact; `medium` Sonnet for purely mechanical Stage 06 |
| `07_review` | `xhigh` (default) | Opus 4.7 (default) | Substantive review; downgrade to `medium` Sonnet for checklist-style audit |
| `08_release` | `low` | Sonnet 4.6 | Release notes, version bumps, tag pushes |

---

## Subagent dispatch

The canonical doc names subagents explicitly:

> `low` — Most efficient. Significant token savings with some capability
> reduction. Simpler tasks that need the best speed and lowest costs,
> **such as subagents**.

Translation for Sunfish: when this main session dispatches a subagent
(via the `Agent` tool), the subagent's effort should default to `low`
unless its task is an exception. Most subagent dispatches fit `low`:
file searches, build verifications, focused PR reviews, single-file
edits with clear specs.

**Exceptions** — subagents that should run at higher effort:

| Subagent role | Effort | Model | Reason |
|---|---|---|---|
| Council / adversarial review (`council-reviewer`, etc.) | `high` to `xhigh` | Opus 4.7 | Multi-stakeholder design judgment |
| Plan author (when delegated whole-plan authoring) | `xhigh` | Opus 4.7 | Decomposition + risk identification need depth |
| Spec-compliance reviewer | `medium` | Sonnet 4.6 | Reading code against a known spec — doesn't need full reasoning |
| Mechanical implementer with exact instructions | `low` | Sonnet 4.6 | Most subagent dispatches fall here |
| Codebase exploration / search | `low` | Sonnet 4.6 | The bundled `Explore` agent's natural fit |

**Frontmatter overrides** — subagents and skills can pin their own
effort and model via frontmatter:

```yaml
---
name: my-subagent
model: opus
effort: xhigh
---
```

When set, frontmatter wins over the project default. When omitted, the
subagent inherits the project default (`xhigh`).

**Current state of Sunfish-relevant agents:**

- **No project-local subagents** exist (no `.claude/agents/` directory).
- **User-level subagents** (in `~/.claude/agents/`) are shared across all
  of Chris's projects and are mostly book-writing agents for the Inverted
  Stack project, not Sunfish code work. They declare `model:` (mostly
  `sonnet`, only `council-reviewer` uses `opus`); none declare `effort:`.
  Don't add Sunfish-specific overrides — they leak into other projects.
- **Bundled agents** (`Explore`, `Plan`, `general-purpose`,
  `code-simplifier`, etc.) are managed by Claude Code itself —
  frontmatter is not editable from this project. They will inherit the
  project's `xhigh` default unless their internal config overrides it.
- **Plugin agents** (`superpowers:code-reviewer`,
  `superpowers:subagent-driven-development`, etc.) are managed by their
  upstream plugins.

**Guidance for future project-local Sunfish agents:** if Sunfish ever
adds an agent under `.claude/agents/`, declare `model:` and `effort:`
explicitly in the frontmatter. Inheritance from the project default
(`xhigh`) is too aggressive for most subagent roles — explicit is better.

Suggested frontmatter for hypothetical project-local Sunfish agents:

| Hypothetical agent | model | effort |
|---|---|---|
| `sunfish-icm-router` (classify intake, choose pipeline variant) | sonnet | low |
| `sunfish-package-architect` (Stage 02/03 design) | opus | xhigh |
| `sunfish-conformance-scanner` (Kleppmann P1–P7 audit) | opus | xhigh |
| `sunfish-stage-06-implementer` (Stage 06 build from a complete Stage 05 plan) | sonnet | medium |
| `sunfish-test-expander` (parity tests, regression coverage) | sonnet | medium |
| `sunfish-doc-copyeditor` (Stage 06 doc deliverables) | sonnet | low |

---

## Override hierarchy (highest wins)

1. `CLAUDE_CODE_EFFORT_LEVEL` env var (set in shell)
2. Subagent / skill frontmatter `effort:` field
3. `/effort <level>` slash command in the current session
4. `.claude/settings.json` `effortLevel` field (this project's default)
5. Claude Code's API default (`high` on every effort-supporting model;
   no support on Haiku 4.5)

For Sunfish, levels 1–3 override the project default per-session. The
project default at level 4 is what kicks in when no override is set.

---

## How to verify

Open a Claude Code session in `C:/Projects/Sunfish` and run:

```text
/effort
```

The reply should show `xhigh` (assuming you're on Opus 4.7). If you're on
a non-Opus-4.7 model, `xhigh` won't apply and Claude Code will fall back
to the model's default (`high`). To diagnose:

- Verify `.claude/settings.json` has `"effortLevel": "xhigh"` (not in
  `.claude/settings.local.json`, which is gitignored and per-user).
- Verify the file is valid JSON (`jq . .claude/settings.json` on a
  POSIX shell, or any JSON validator).
- Confirm your active model: `/model` should show `opus` (or
  `claude-opus-4-7` specifically).
