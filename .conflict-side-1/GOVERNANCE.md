# Sunfish Governance

**Status:** Accepted
**Last reviewed:** 2026-04-20
**Applies to:** Every decision about Sunfish's direction, architecture, roadmap, releases, and community — from the core maintainer's day-to-day calls to the transitions that will happen as the project grows.

Sunfish is **pre-release, pre-community**. This document is short, honest about the current state, and designed to scale without rewrite as contributors arrive. It does not describe governance the project hopes to have one day — it describes governance today and the triggers that evolve it over time.

## Current state: Benevolent Dictator (BDFL)

Sunfish is currently led by a single maintainer — the **[BDFL](https://en.wikipedia.org/wiki/Benevolent_dictator_for_life)** — who has final authority over all architectural, roadmap, release, and community decisions.

- **BDFL:** [@ctwoodwa](https://github.com/ctwoodwa) (Christopher Wood)
- **Effective date:** project inception
- **Transition plan:** see §"Transition triggers" below

Every mature open-source project that started with a single contributor began this way. Sunfish names the posture explicitly because pretending otherwise is worse than owning it. What the BDFL decides today, a broader governance body will decide once the community reaches the triggers below.

## The framework stack

Sunfish layers four complementary frameworks. They operate at different altitudes and answer different questions:

| Framework | Source | Altitude | Question it answers |
|---|---|---|---|
| **Open Decision Framework (ODF)** | Red Hat | Governance | *Who decides, how inclusively, with what transparency?* |
| **Universal Planning Framework (UPF)** | Primeline AI | Plan quality | *Is the plan itself rigorous enough to execute?* |
| **Integrated Change Management (ICM)** | Sunfish | Workflow | *What stages does work pass through in the repo?* |
| **This document** | Sunfish | Meta | *How do the above combine, and who's accountable for what?* |

See [`_shared/engineering/planning-framework.md`](_shared/engineering/planning-framework.md) for the ODF + UPF + ICM mapping in detail.

## ODF principles in practice

Sunfish follows Red Hat's Open Decision Framework. The five principles show up concretely:

1. **Open exchange** — Problems, constraints, and success criteria are published before a decision closes. ADRs cite the problem, alternatives considered, and rationale. Roadmap items surface in [`_shared/product/roadmap-tracker.md`](_shared/product/roadmap-tracker.md) before implementation.
2. **Participation** — Every significant decision is reachable by external input. Architecture decisions are ADRs opened as pull requests with a comment window; roadmap changes land in issues with Discussion threads; releases have public notes.
3. **Meritocracy** — The best-argued option wins regardless of source. The BDFL's first-draft position is not privileged over a well-argued alternative from anyone.
4. **Community** — Decisions weigh impact on the broader ecosystem (future contributors, downstream adopters, the platform's longevity), not only the immediate feature roadmap.
5. **Release early, release often** — The minimum viable decision ships quickly; refinement happens in public once deployed rather than behind closed doors.

## Decision types and mechanisms

| Decision type | Mechanism | Transparency artifact |
|---|---|---|
| **Architecture** (cross-cutting commitments, type-customization model, framework choices, …) | ADR in `docs/adrs/NNNN-*.md` | Merged ADR with context, alternatives, rationale |
| **Roadmap priority** (what ships, in what order) | Update to [`_shared/product/roadmap-tracker.md`](_shared/product/roadmap-tracker.md) | Tracker diff with phase/status changes |
| **Bundle composition** (which modules activate for a business case) | Bundle manifest JSON in `packages/foundation-catalog/Manifests/Bundles/` | Manifest file with reviewed diff |
| **Feature / enhancement** | Issue → RFC (via [RFC issue template](.github/ISSUE_TEMPLATE/rfc.yml)) → optional ADR if architectural → PR | RFC issue thread + merged PR |
| **Bug fix / non-architectural change** | Issue → PR via ICM flow | PR thread; ICM stage output if non-trivial |
| **Release** | Tag + release notes | GitHub release with changelog |
| **Security vulnerability** | Private disclosure via [GitHub Security Advisory](.github/SECURITY.md) | Coordinated disclosure after fix ships |
| **Governance change** (this document) | PR to `GOVERNANCE.md` with explicit rationale | Merged PR with change summary |

## How external contributors participate

Sunfish has no external community today. When one arrives, the mechanisms are already wired:

- **GitHub Issues** for bugs, feature requests, and RFCs. Templates in [`.github/ISSUE_TEMPLATE/`](.github/ISSUE_TEMPLATE/).
- **GitHub Discussions** for open-ended questions, design exploration, and pre-RFC conversation.
- **Pull requests** with the template in [`.github/pull_request_template.md`](.github/pull_request_template.md).
- **Code of Conduct** in [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md) governs all project spaces.
- **Security vulnerabilities** via private reporting per [`.github/SECURITY.md`](.github/SECURITY.md).

Every PR author retains copyright to their contribution. Sunfish uses **DCO (Developer Certificate of Origin)** — sign off commits with `git commit -s`. No CLA is required. If a corporate contributor needs a CLA, that request triggers a revision to this document.

## License

Sunfish is licensed under the **[MIT License](LICENSE)** (Copyright © 2026 Christopher Wood). The code is free to use, modify, and distribute — including for commercial purposes. Commercial revenue for Sunfish flows from services (hosting, implementation, integration), never from license fees. See vision §"Business model" for framing.

The license choice is MIT rather than Apache 2.0 to prioritize adoption simplicity. Apache 2.0's patent grant is the main reason to prefer it; the project will revisit if a downstream adopter raises patent-exposure concerns.

## Transition triggers

Sunfish's governance evolves when the community reaches these triggers. Each one produces a revision to this document rather than a silent policy change.

| Trigger | Governance change |
|---|---|
| **3+ external committers** sustained over 3 months | Add a **maintainer tier** below the BDFL. Maintainers can merge PRs in their CODEOWNERS scope; the BDFL retains override + architectural authority. |
| **10+ external committers** | Consider forming a **Technical Steering Committee (TSC)** of 3–7 people for architectural decisions. BDFL becomes a voting member, not the sole authority. |
| **First disputed ADR** (two well-argued positions, no clear winner) | Formalize a **lightweight RFC process** beyond GitHub Discussions — explicit comment window, documented decision criteria, written rationale for the choice made. |
| **First corporate adopter running in production** | Publish an **SLA/SLO posture** and consider a **CLA** (or an explicit "no CLA needed" commitment); review the MIT-vs-Apache-2.0 license choice. |
| **Production adoption by 3+ unrelated organizations** | Evaluate **foundation membership** (CNCF, Linux Foundation, Apache Software Foundation, or an independent Sunfish foundation) for neutral stewardship. |
| **First hostile fork or governance complaint** | Tighten contribution acceptance criteria; review the Code of Conduct enforcement process. |
| **BDFL unavailable for >30 days without notice** | Activate the **succession protocol** (below). |

When a trigger fires, the BDFL opens a PR to this document with the proposed change, invites public comment (2-week window, per ODF "release early" principle), and merges the revision with a summary of feedback received.

## Succession and bus factor

Sunfish has **bus factor = 1** today. That is a real risk the project acknowledges, not a concern to hide.

- The **maintainer's account** ([@ctwoodwa](https://github.com/ctwoodwa)) is the single point of failure for repository administration.
- **Backup-admin trigger.** A second GitHub repository administrator must be appointed before any of the following: (a) the project's first production deployment by an external organization, (b) the first paid commercial support contract, or (c) the first sustained external committer reaching the maintainer tier (see §Transition triggers) — whichever comes first. This is a hard precondition, not an aspiration.
- **Succession protocol** if the maintainer becomes unavailable:
  1. Repository administrators named in this document (currently none; update when additional admins are appointed) assume repository operations.
  2. The maintainer commits to designating a **successor or co-maintainer before production adoption** by any external party.
  3. If no successor is named and the maintainer is unreachable for ≥90 days, any three external committers with sustained contributions may petition to fork under new governance; this is the fallback, not the plan.

Reducing the bus factor is a transition trigger in its own right: it happens before any organization comes to depend on Sunfish in production.

## Conflict resolution

Where reasonable people disagree:

1. **Discuss in public.** GitHub Discussions or the issue thread — not DMs.
2. **Restate the problem.** UPF Stage 0 Check 0.7 (Feasibility) and Check 0.9 (Better Alternatives) both apply — often the disagreement dissolves when the actual problem is re-decomposed.
3. **Name the criteria.** Both sides articulate what would change their mind.
4. **Ship a time-bound experiment when possible.** ODF "release early, release often" — two weeks running the disputed choice often settles it.
5. **BDFL decision + written rationale.** If consensus doesn't form, the BDFL decides and writes out the reasoning so the decision is reviewable and revisable later. Cite ADR if architectural.

Decisions that turn out poorly are revisable. Nothing in this document prevents a revised ADR, a reversed roadmap decision, or a retracted release. The important thing is that reversals are public and reasoned.

## Revising this document

Governance changes are themselves architectural decisions. A PR to `GOVERNANCE.md` follows the same ODF-shaped process as any other ADR:

- Open with rationale: what condition triggers this change?
- Invite comment: 2-week public window for pre-release; longer once there's a community.
- Merge with change summary: what was revised, what feedback influenced the outcome.

Material changes also update [ADR 0018](docs/adrs/0018-governance-and-license-posture.md) or supersede it with a new ADR.

## References

- **[Red Hat Open Decision Framework](https://github.com/red-hat-people-team/open-decision-framework)** — the governance model Sunfish adopts.
- **[Universal Planning Framework](https://github.com/primeline-ai/universal-planning-framework)** — the plan-quality discipline applied inside ODF's Planning-and-Research phase.
- **[ICM pipeline](icm/CONTEXT.md)** — Sunfish's workflow-stage orchestration.
- **[ADR 0018](docs/adrs/0018-governance-and-license-posture.md)** — records governance model + license choice in the architectural trail.
- **[ADR index](docs/adrs/README.md)** — all architectural decisions.
- **[roadmap-tracker.md](_shared/product/roadmap-tracker.md)** — current phase status and decisions.
- **[Open Source Guides: Leadership and Governance](https://opensource.guide/leadership-and-governance/)** — reference for the evolution path from BDFL to broader governance.
- **[OpenSSF Best Practices Badge](https://openssf.org/best-practices-badge/)** — Sunfish's deferred target (Passing level on first external adopter signal).
