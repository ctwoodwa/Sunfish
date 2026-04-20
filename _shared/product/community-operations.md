# Community Operations

**Status:** Posture for pre-release / pre-community
**Last reviewed:** 2026-04-20
**Governs:** How Sunfish engages with contributors, users, and adopters once a community forms — and, just as importantly, what the project deliberately does *not* do while no community exists yet.
**Companion docs:** [vision.md](vision.md), [roadmap-tracker.md](roadmap-tracker.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md), [`../../CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md), [`../engineering/ai-code-policy.md`](../engineering/ai-code-policy.md), [`../engineering/data-privacy.md`](../engineering/data-privacy.md).

## Posture summary

**Sunfish has no community today.** The founder-BDFL is the only active contributor, the first external bundle has not yet shipped, and there is no Slack, no Discord, no newsletter, no project social account, no conference presence. This document is deliberately deferred content — it describes the shape community operations take *when* triggers fire, not what the founder should be doing right now.

Pre-community operations is a narrow, honest job:

1. Make the first external contribution arrive gracefully.
2. Maintain responsive issue and PR review so that when someone does show up, they're not left on read.
3. Keep governance honest — every decision traceable, every transition trigger named in advance.

Everything else in this document is a plan for a future state. The transition triggers from [`GOVERNANCE.md`](../../GOVERNANCE.md) are what activate each level of investment.

## Why this document exists now

Writing community-ops posture before a community exists serves three purposes:

1. **Avoid the pre-community trap.** It's easy to burn weeks on Discord setup, newsletter drafting, and social-media strategy before a single external contribution has arrived. That's motion, not progress. Naming the deferral in advance makes it easier to refuse the temptation.
2. **Be ready when triggers fire.** When the first external PR lands, the founder doesn't want to be figuring out the All Contributors bot config from scratch. Having the playbook pre-written means the response is minutes, not days.
3. **Be honest with prospective adopters.** An evaluator looking at Sunfish today deserves to know the community posture is explicit and staged — not hidden, not overclaimed. Pretending to have a community Sunfish doesn't have would violate the ODF "release early, release often" principle in a more damaging way than admitting the project is early.

## Pre-community obligations (active now)

Even a solo maintainer has baseline community obligations. These are in force today, before any trigger fires:

- **Responsive communication.** 5-business-day response SLA on new issues and PRs, per `GOVERNANCE.md`. "Responded" means acknowledged, labeled, and routed — not necessarily resolved. Silence on a first-time contribution is the fastest way to lose a community before it starts.
- **Public transparency on decisions.** ADRs live in `docs/adrs/` under the Open Decision Framework (ODF) shape — problem, alternatives, rationale, consequences. Roadmap changes land publicly in [`roadmap-tracker.md`](roadmap-tracker.md). Design conversations happen in Discussions and Issues rather than DMs or private documents.
- **Welcoming posture in issues and PRs.** Assume good faith. Explain context the first-time contributor couldn't reasonably know. Prefer "here's how" over "you should have." First-contributor experience disproportionately determines whether a second contribution ever arrives.
- **Clear contribution paths.** [`CONTRIBUTING.md`](../../CONTRIBUTING.md), [`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md), [`GOVERNANCE.md`](../../GOVERNANCE.md), [`.github/ISSUE_TEMPLATE/`](../../.github/ISSUE_TEMPLATE/), and the PR template all exist and are kept current as the repo evolves.
- **GitHub Discussions enabled.** Open-ended conversation routes to Discussions via [`.github/ISSUE_TEMPLATE/config.yml`](../../.github/ISSUE_TEMPLATE/config.yml); issues stay scoped to bugs, features, and RFCs. Having the channel open before a community exists means the first arrival has somewhere to land.
- **DCO enforced, no CLA required.** Per `GOVERNANCE.md`, every commit carries `Signed-off-by` via `git commit -s`. No Contributor License Agreement is required. This lowers the bar for a drive-by contribution without giving up accountability.

## Community levels and their activation triggers

Each level corresponds to a governance trigger in [`GOVERNANCE.md`](../../GOVERNANCE.md). The project moves between levels when the trigger fires, not on a schedule.

| Level | State | Activation trigger | What turns on |
|---|---|---|---|
| **0 — Solo** | Current | N/A | BDFL maintains everything; no community operations beyond the pre-community obligations above. |
| **1 — First external contributor** | Aspirational | First external issue or PR | "Good first issue" and "help wanted" labels applied to backlog; fast review; personal welcome and public thank-you. |
| **2 — Three external committers** | Deferred | 3+ committers sustained over 3 months (GOVERNANCE maintainer-tier trigger) | Maintainer tier designated; [All Contributors](https://allcontributors.org/) bot or equivalent attribution; monthly changelog summary posted to Discussions Announcements. |
| **3 — Ten external committers** | Deferred | 10+ committers (GOVERNANCE TSC trigger) | Technical Steering Committee forms; lightweight formal RFC process adopted; optional community calls or office hours if the community asks for them; CHAOSS-aligned metrics dashboard (Augur or GrimoireLab) stood up. |
| **4 — Corporate adopter signals** | Deferred | First corporate adopter running in production (GOVERNANCE commercial trigger) | DevRel content program begins (blog posts, case studies, conference talks); commercial support intake channel; project social account (if any) created. |
| **5 — Foundation evaluation** | Deferred | Production adoption by 3+ unrelated organizations (GOVERNANCE foundation trigger) | Foundation proposal process begins (CNCF Sandbox, Linux Foundation, Apache, or independent); community and infrastructure move to foundation-neutral channels. |

## Communication channels

Each channel has a defined purpose and a defined activation level. Adding a channel before its level is a maintenance tax the project can't afford yet.

| Channel | Purpose | Status |
|---|---|---|
| **GitHub Issues** | Bugs, features, RFCs | Active |
| **GitHub Discussions** | Questions, ideas, show-and-tell, pre-RFC discussion | Active — routed via [`.github/ISSUE_TEMPLATE/config.yml`](../../.github/ISSUE_TEMPLATE/config.yml) |
| **GitHub Discussions → Announcements** | Release notes summaries, governance changes | Active |
| **Changelog** | Per-release user-facing changes | Active — per Tier 1 `releases.md` (when added) |
| **Security advisories** | Private vulnerability reporting | Active — per [`SECURITY.md`](../../.github/SECURITY.md) |
| **Blog / long-form writing** | Technical deep dives, case studies | Deferred to Level 4 |
| **Conference talks and podcasts** | Industry presence | Deferred to Level 4 |
| **Discord / Slack / Matrix chat** | Real-time chat | Deferred — only if the community explicitly asks. High maintenance burden, poor searchability, excludes async contributors. Projects that have tried this and regretted it are numerous. |
| **Newsletter** | Periodic digest | Deferred until ~100 subscribers would plausibly exist |
| **Project social media** (Twitter / Bluesky / Mastodon) | Marketing and recognition | Deferred to Level 4. The BDFL posts personally; there is no project account until a community exists to engage. |

GitHub Discussions is Sunfish's deliberate choice over Discourse at Levels 0–3:

- **Zero hosting cost.** Discourse self-hosted requires a VPS and admin time; Discourse hosted costs money Sunfish doesn't yet have customers to fund.
- **Account unification.** Contributors already have GitHub accounts for Issues and PRs; a second account on a separate platform is friction that deflects first-time participants.
- **Tight integration with code.** Discussions can reference Issues, PRs, and commits natively; cross-linking is trivial.

Discourse becomes worth evaluating only if the community outgrows Discussions' features (trust levels, per-category moderation, theming, plugins) — which requires a community to exist first. Projects that adopted both early have typically ended up consolidating back to one.

## Metrics posture (CHAOSS-aligned)

[CHAOSS](https://chaoss.community/) (Community Health Analytics in Open Source Software, a Linux Foundation project) publishes 89+ metrics and 17+ metrics models for OSS community health. Sunfish adopts them progressively, not all at once — instrumenting metrics for a community that doesn't exist produces noise, not signal.

| Level | What to measure | How |
|---|---|---|
| **Pre-Level 2** | Simple counts — issues opened/closed, PRs merged, stars, forks, Discussions activity | GitHub Insights; no instrumentation needed |
| **Level 2+** | CHAOSS basics — time to first response, PR merge time, active contributors (monthly), first-contributor retention, contributor absence factor (bus factor) | Augur or GrimoireLab, scoped to the Sunfish repo |
| **Level 3+** | CHAOSS DEI metrics — new contributor onboarding success, review-load distribution across maintainers, geographic and organizational diversity of contributors | CHAOSS DEI working-group metrics models |
| **Level 4+** | Full CHAOSS instrumentation plus relationship-mapping (Orbit model, CommonRoom, or open-source equivalent) | Depends on whether a community manager has been hired by commercial services |

**Vanity metrics are not success signals.** GitHub stars, Twitter followers, download counts without context, "community size" as a single number — Sunfish tracks these because they're free to observe, but it does not chase them and does not report them as headline health indicators. The milestones that matter are in [vision.md §"What winning looks like"](vision.md): a small-landlord operator running PM on their own hardware; the first commercial school-district customer onboarded; federation carrying cross-organizational data in one vertical; third-party developers publishing bundles for verticals Sunfish hasn't touched.

## Recognition and attribution

Contribution recognition scales with the community. Under-crediting contributors is the single fastest way to burn community goodwill; over-crediting is a much smaller sin.

- **Co-authored-by in commits.** Per `commit-conventions.md`, AI assistance and pair contributions use the standard `Co-authored-by:` trailer. Human attribution stays intact even when AI helped. See [`ai-code-policy.md`](../engineering/ai-code-policy.md) for the disclosure policy.
- **Changelog credit.** Each release's changelog names contributors whose PRs shipped in that release. Non-code contributions (docs, translation, triage, design feedback that shaped a decision) get named when they're significant.
- **All Contributors spec at Level 2+.** The [All Contributors](https://allcontributors.org/docs/en/specification) spec and bot get adopted when the maintainer tier forms — it recognizes non-code contributions (docs, triage, design, review, accessibility audit, translation) alongside code, which matters more as the community diversifies. Using it earlier than Level 2 would produce a README badge-wall listing the BDFL in every category, which is not the point.
- **Maintainer status is earned, not assigned.** Per the ODF meritocracy principle in `GOVERNANCE.md`, maintainer scope reflects sustained contribution in a CODEOWNERS area, not appointment. No "come be a maintainer" recruiting campaigns; the path is: contribute → sustain → get named.
- **Public graduation.** When a contributor becomes a maintainer, it's announced in Discussions and the changelog, not quietly merged into CODEOWNERS.

## Community moderation

- **Code of Conduct applies universally** across all project spaces — repo, Discussions, any future community channels, and any event where Sunfish is officially represented. See [`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md).
- **Moderation reports** go to the BDFL via private GitHub Security Advisory (the same private channel vulnerabilities use) until a moderation-specific channel is warranted. Reporters are not required to identify themselves publicly; confidentiality is honored to the extent compatible with investigation.
- **Response time.** Moderation reports get acknowledged within 2 business days and a disposition within 10 business days. A public summary of enforcement actions (with reporter identity protected) may be published at Level 3+ for accountability.
- **Level 3+:** a dedicated Code of Conduct committee forms when the TSC forms, so moderation authority is not concentrated in a single person and conflicts of interest (BDFL as both investigator and subject) have a clear escalation path.
- **No retaliation.** Reporting a Code of Conduct violation, in good faith, is never itself grounds for project consequences — no matter who the subject of the report is.

## DevRel and content posture

DevRel investment at the pre-community stage is a classic misallocation — it produces content for an audience that doesn't exist yet, and costs maintainer hours that are better spent shipping the first bundle. Sunfish defers it explicitly.

- **Pre-Level 4:** no scheduled content program. No blog calendar. No conference talks on behalf of the project. The founder posts opportunistically on personal channels (blog, LinkedIn, personal social) without representing a project voice that doesn't exist yet. Writing about Sunfish's technical decisions publicly is still valuable — it contributes to the ODF transparency record — but it happens when there's something worth saying, not on a cadence.
- **Level 4+:** commercial services revenue (per [vision.md §"Business model"](vision.md)) funds dedicated content work. Target audiences in priority order: technical founders building vertical SaaS, small operators evaluating self-hosting, AI-savvy stakeholders on vertical teams. Content formats: long-form technical writeups, reference-bundle case studies, conference talks at events where the target audience actually attends.
- **Never:** sponsored or paid-placement content that obscures the commercial relationship. If Sunfish commercial services are mentioned in a post, the post says so. If a vendor sponsors a talk, the talk discloses it.

## Events and meetups

Deferred until Level 4+. Sunfish will show up at relevant conferences once a commercial customer signal justifies travel budget:

- **General OSS and developer events.** `.NET Conf` (Microsoft-stack audience, first natural fit given Sunfish's .NET core), CHAOSScon (community-health audience, relevant once metrics instrumentation lands), `All Things Open`, `FOSDEM`, `Open Source Summit`.
- **Vertical-specific events** aligned with shipped reference bundles. HIMSS for healthcare, buildingSMART International Summit for BIM and construction, Ed-Fi Summit for K-12 education, IRS Nationwide Tax Forum for tax filing, NARPM for property management.
- **Local meetups** in cities where commercial customers and contributors cluster — attend first, speak second, host last.

Hosting a Sunfish-specific meetup or summit is deferred until Level 4+; the community has to exist before gathering it makes sense. A "Sunfish Conf" before the first external contributor arrives is theater, not community-building.

## Commercial community tier

Sunfish's business model (per [vision.md §"Business model"](vision.md)) is "services on top of OSS." Paid support, managed hosting, implementation help, and federation onboarding are legitimate revenue streams. But **paid status does not buy governance influence** — the meritocracy principle in ODF and `GOVERNANCE.md` applies equally to commercial customers and free users. Commercial contracts purchase service-level response, implementation help, and hosting; they do not purchase roadmap priority, ADR outcomes, or private features.

Specifically, commercial customers get:

- Guaranteed response times and escalation paths per their support contract.
- Private channels for deployment-specific questions that shouldn't be public (credentials, tenant data, infrastructure topology).
- Priority scheduling for professional-services engagements.

Commercial customers do not get:

- Roadmap vetoes or private feature roadmaps.
- Private builds of Sunfish with proprietary features not available in OSS.
- The ability to suppress public discussion of bugs or limitations they care about.

The commercial community tier activates when the first paying customer signals materialize (Level 4). Intake goes through a documented commercial channel separate from public issues; day-to-day technical questions still flow through public Discussions so that answers benefit everyone and institutional knowledge accrues in searchable form.

## Anti-patterns

Things Sunfish explicitly won't do, regardless of what level activates:

- **Buying stars, downloads, or manufactured hype.** Vanity metrics obtained by purchase are dishonest about project health and corrupt the signal that future adopters rely on.
- **Creating fake community activity.** Sockpuppet accounts, astroturfed "satisfied user" posts, fabricated testimonials, or AI-generated reviews that impersonate users.
- **Gatekeeping OSS features behind community-gated walls.** Every extension point in [vision.md §"How Sunfish grows"](vision.md) is available in the free OSS release. Commercial services help customers *use* features, not *gain access to them*. There is no "community edition" vs "enterprise edition" split.
- **Suppressing critical feedback.** Negative issues stay open and visible; the response is a reasoned reply, not deletion. Legitimate criticism is a contribution even when it's uncomfortable.
- **Chat-only communication.** Discord, Slack, or Matrix as the primary channel excludes search, excludes async contributors across timezones, and buries institutional memory in a log nobody reads. If real-time chat ever activates, it supplements Issues and Discussions; it does not replace them, and meaningful decisions made in chat get summarized back to a searchable public record.
- **Requiring a CLA for OSS contributions.** Per `GOVERNANCE.md`, DCO signoff is the bar. If a specific corporate adopter requires a CLA to participate, that request triggers a governance revision — it does not become the project default.

## Activation triggers (consolidated)

Tying the preceding sections back to the triggers in [`GOVERNANCE.md`](../../GOVERNANCE.md):

| Trigger | Operational response |
|---|---|
| First external issue filed | Apply `good-first-issue` or `help-wanted` labels across the backlog; personal welcome reply within 1 business day; ensure the reporter knows where to follow up. |
| First external PR merged | Public thank-you in the PR; changelog credit in the next release; `All Contributors` entry (when bot activates at Level 2). |
| **3+ external committers** (Level 2) | Maintainer tier per `GOVERNANCE.md`; activate `All Contributors` bot; begin monthly changelog summary in Discussions Announcements. |
| **10+ external committers** (Level 3) | TSC forms; formal RFC process; consider optional community call cadence; stand up CHAOSS metrics dashboard. |
| **First corporate adopter signal** (Level 4) | DevRel content plan; commercial support intake channel; case-study process; project social account if warranted. |
| **3+ production orgs** (Level 5) | Foundation evaluation begins; community moves toward foundation-neutral stewardship. |

## Cross-references

- [vision.md](vision.md) — audience, business model, milestones.
- [roadmap-tracker.md](roadmap-tracker.md) — phase status that drives when Level 4 signals become plausible.
- [`GOVERNANCE.md`](../../GOVERNANCE.md) — the transition triggers this document operationalizes.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — how contributors actually land a change.
- [`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md) — community conduct baseline.
- [`ai-code-policy.md`](../engineering/ai-code-policy.md) — AI-assisted contribution disclosure and accountability.
- [`data-privacy.md`](../engineering/data-privacy.md) — privacy posture for user and contributor data (being drafted in parallel).

### External references

- [CHAOSS (Community Health Analytics in Open Source Software)](https://chaoss.community/) — Linux Foundation project publishing the metrics and models this document aligns with.
- [All Contributors specification](https://allcontributors.org/docs/en/specification) — non-code-contribution recognition spec adopted at Level 2.
- [Open Source Guides: Leadership and Governance](https://opensource.guide/leadership-and-governance/) — reference for the evolution path from BDFL to broader governance.
- [CNCF Sandbox entry criteria](https://www.cncf.io/sandbox-projects/) — reference point for Level 5 foundation evaluation.
- [TODO Group](https://todogroup.org/) — OSPO practices referenced when commercial adopters evaluate Sunfish internally.
- [Orbit model](https://github.com/orbit-love/orbit-model) — community-gravity framework considered at Level 4+.
