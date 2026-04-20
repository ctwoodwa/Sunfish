# Funding and Sustainability

**Status:** Posture for pre-release
**Last reviewed:** 2026-04-20
**Governs:** Funding sources, financial sustainability path, and the relationship between commercial services and the OSS project.
**Companion docs:** [vision.md](vision.md), [community-operations.md](community-operations.md), [roadmap-tracker.md](roadmap-tracker.md), [compatibility-policy.md](compatibility-policy.md), [`../../GOVERNANCE.md`](../../GOVERNANCE.md), [`../../docs/adrs/0018-governance-and-license-posture.md`](../../docs/adrs/0018-governance-and-license-posture.md).

## Posture summary

Sunfish's sustainability model is **commercial services on an OSS platform**. The open-source stack — components, blocks, bundles, Bridge, federation, local-first — is MIT-licensed and will remain so. Commercial revenue flows from services that add value on top: managed SaaS hosting, implementation help, federation onboarding, accessibility audits, internationalization work, privacy compliance consulting.

Traditional OSS funding mechanisms — [GitHub Sponsors](https://github.com/sponsors), [Open Collective](https://opencollective.com/) / [Open Source Collective](https://docs.oscollective.org/), [Tidelift](https://tidelift.com/), and foundation membership ([CNCF](https://www.cncf.io/sandbox-projects/), [Linux Foundation](https://www.linuxfoundation.org/), [Apache](https://www.apache.org/)) — are **deferred until production adoption signals warrant them**. This document describes the shape each activation takes, so that when a trigger fires, the response is hours of setup rather than weeks of research.

Pre-revenue is the honest state today. Every mechanism below is a plan, not a current practice.

## The operating thesis

Per [vision.md §"Business model"](vision.md):

- **The OSS stack is free and unrestricted.** MIT-licensed, no feature gating, no "community edition" vs "enterprise edition" split. The thing you run in production is the thing anyone can fork.
- **Commercial revenue comes from services that add value.** Managed SaaS hosting (a small medical office pays for a running instance rather than learning Docker). Implementation help (setup, integration with existing systems, custom bundle authoring). Federation onboarding (getting a school district's node talking to a state agency's node). Accessibility audits, i18n translation, privacy compliance consulting — specialist work at the edges of a real deployment.
- **AI leverage makes low-cost services economically viable.** A two-person services company using AI can support customers a traditional 20-person SaaS vendor couldn't. The unit economics of services-on-OSS assume AI as a cost primitive (per vision Pillar 4).
- **We make money when we add value.** If a customer can self-host competently, we want them to — not because we're purists, but because forcing them into a managed tier would be charging for nothing.
- **No VC funding for the OSS project itself.** The commercial services entity is separate and may take outside capital if its founders choose; the OSS project is not capitalized against equity, runway pressure, or exit expectations.

## Revenue streams

Ordered by priority and activation trigger. Each stream has a shape — what it is, when it turns on, what it commits to.

### 1. Commercial services (primary stream)

**Activation trigger:** first commercial customer (currently targeting the school-district pilot per vision milestones).

The services business is the primary revenue path. Shape:

- **Managed SaaS hosting.** Bridge-operated tenants on infrastructure Sunfish commercial services runs. First-tenant pricing is **cost-plus** (customer pays the infrastructure cost plus a margin); per-tenant / per-seat / per-action pricing gets formalized when there's a second hosted customer to compare against.
- **Implementation services.** Hourly or project-based engagements for setup, custom bundle authoring, integrations with the customer's existing systems (SIS, EHR, accounting, identity provider).
- **Federation onboarding.** Getting a new node on a federation mesh: identity, credentials, capability delegation, wire-format adapter for whatever standard the vertical speaks (FHIR, Ed-Fi, IFC, MeF, etc. per vision Pillar 2).
- **Domain audits.** Accessibility (WCAG 2.2 AA attestation), internationalization (new-locale translation + RTL / pluralization review), privacy / compliance (HIPAA, FERPA, state privacy regulations), security review.

These are **value-for-money transactions, not donations**. Customers buy because self-service would cost them more in time or risk than the service costs in money.

### 2. GitHub Sponsors (optional, low-friction)

**Activation trigger:** first external contributor engagement (community-operations Level 1).

[GitHub Sponsors](https://github.com/sponsors) is a light-weight donation channel for individuals or organizations who want to support maintainer time without entering into a commercial contract. Shape when activated:

- Sponsor page on the maintainer's GitHub profile and (later) the Sunfish org.
- **Tier-free or minimal tiers** — no perks that conflict with OSS equality. Sponsorship does not buy roadmap priority, governance influence, or private features (per [community-operations.md §"Commercial community tier"](community-operations.md) and [GOVERNANCE.md](../../GOVERNANCE.md)).
- Optional public recognition (sponsor name in a `SPONSORS.md`) is fine; tiered access to anything technical is not.
- Funds flow to the maintainer personally pre-Open-Collective; post-activation they route through the project's fiscal host.

GitHub Sponsors is a signal mechanism more than a revenue mechanism at this scale. It matters more as evidence that external people care than as budget.

### 3. Open Collective + fiscal sponsorship (transparent project funds)

**Activation trigger:** first recurring committer beyond the BDFL (community-operations Level 2).

[Open Collective](https://opencollective.com/) with fiscal sponsorship through [Open Source Collective](https://docs.oscollective.org/) (the nonprofit fiscal host that supports 2,500+ OSS projects and partners directly with GitHub Sponsors) is the right shape for a **transparent project ledger** once the project has expenses beyond the BDFL's personal costs. Shape:

- **Fiscal host:** Open Source Collective is the default choice — US-based, GitHub Sponsors integration, 10% fees on funds raised, legal and accounting support. [Open Collective Europe](https://opencollective.com/europe) is the alternative if the contributor base shifts European.
- **What the collective funds:** CI / hosting costs, domain renewals, event travel stipends for contributors presenting Sunfish work, design / illustration / audio work commissioned from freelancers, first-contributor welcome packs (stickers, swag) if the project ever reaches the scale where that makes sense.
- **What the collective does not fund:** the BDFL's commercial services work (that's billed to customers, not the collective), private or closed work of any kind, expenses that could reasonably be funded by the commercial entity.
- **Public ledger.** Every transaction is visible on the collective's page. This is the model's strongest feature — contributors, users, and auditors can all see where the money goes.

### 4. Tidelift (commercial subscription for dependents)

**Activation trigger:** 3+ organizations using Sunfish in production (aligns with [GOVERNANCE.md](../../GOVERNANCE.md) foundation-evaluation trigger and [community-operations.md](community-operations.md) Level 5).

[Tidelift](https://tidelift.com/) pays maintainers recurring subscriptions (historically ~$100-$150 per developer annually at the subscriber company) in exchange for security and maintenance commitments — license compliance, vulnerability response SLAs, end-of-life notice, dependency hygiene. It works well for **libraries used as dependencies**, which Sunfish's `foundation-*`, `ui-core`, `blocks-*`, and `federation-*` packages all qualify as once adopted.

Shape when activated:

- Apply as a Tidelift "lifter" for the packages with external adopter signal — not the whole repo at once.
- Commitments that ship with lifter status: vulnerability response within the Tidelift SLA (currently 14 days for disclosure coordination), license metadata accuracy, deprecation notice lead time.
- Revenue splits between the maintainer and any co-maintainers on the package per a published attribution rule.

Tidelift's value is **predictable recurring revenue** tied to dependents who are willing to pay for assurance. It is complementary to direct commercial services, not a replacement.

### 5. Foundation funding (neutral stewardship)

**Activation trigger:** foundation membership evaluation (per [GOVERNANCE.md](../../GOVERNANCE.md) — production adoption by 3+ unrelated organizations).

When Sunfish has demonstrated value to multiple organizations, **foundation membership** becomes the appropriate next step for neutral stewardship. Candidate foundations:

- **[CNCF Sandbox](https://www.cncf.io/sandbox-projects/)** — entry point for cloud-native projects; transfers the project name to the Linux Foundation trademark, requires maintainers from multiple organizations, provides infrastructure and program support. Natural fit if Sunfish's federation / multi-tenancy / cloud deployment story is what adopters care about most.
- **[Linux Foundation](https://www.linuxfoundation.org/)** (directly or via a sub-foundation like LF AI & Data, LF Networking, or a new project vehicle) — broader scope than CNCF, more flexibility on project shape.
- **[Apache Software Foundation](https://www.apache.org/)** (Incubator) — if the contributor community prefers Apache's governance style and mentor-driven incubation process.
- **Independent Sunfish foundation** — viable once there are enough contributor organizations that donating into an independent 501(c)(6) trade association or 501(c)(3) charitable entity makes sense. Higher operational cost than joining an existing foundation.

Foundation funding shape:

- **Corporate membership fees** from companies running Sunfish in production (tiered — Platinum / Gold / Silver / Academic).
- **Infrastructure support** (build servers, CDN for docs, event sponsorship, legal support for trademarks and contracts).
- **Grants** for specific work (security audit, accessibility certification, documentation overhaul).

Foundation membership requires **foundation-neutral governance** (per [community-operations.md](community-operations.md) Level 5) — the BDFL becomes one voice among many, the project brand transfers, and decisions route through the foundation's processes.

## What Sunfish will NOT do

Anti-patterns the project explicitly rejects, regardless of financial pressure:

- **No paid-tier feature gating.** The MongoDB (SSPL, 2018) → Elastic (2021, partially reversed 2024) → HashiCorp (BSL, 2023) → Redis (RSALv2 + SSPL, 2024, reversed to AGPLv3 in May 2025) license-change pattern is the cautionary precedent. No evidence shows those changes improved revenue; they spawned major forks (OpenSearch, OpenTofu, Valkey) and forfeited community trust. Sunfish's MIT license stays MIT per [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md). No dual-license pivots. No "community edition" vs "enterprise edition" split.
- **No VC funding for the OSS project itself.** The commercial services entity is legally separate and may take outside capital if its founders choose. The OSS project has no runway pressure, no exit expectations, and no investor influence on architecture or roadmap.
- **No crypto / token fundraising.** Regulatory surface (SEC, FTC, state money-transmitter laws) is prohibitive; community perception is worse; the mechanism adds no capability Sunfish actually needs. Foreclosed.
- **No bait-and-switch license changes.** MIT stays MIT. If a future circumstance somehow justified reconsidering, the decision would go through the [ADR 0018](../../docs/adrs/0018-governance-and-license-posture.md) revision process with a public comment window — never as a surprise release.
- **No soliciting foundation membership fees before the project has demonstrated value.** Asking for corporate sponsorship of a project that hasn't yet shipped a second bundle or landed a first commercial customer is premature and makes the project look like vaporware.
- **No sponsor-for-governance trades.** Per ODF meritocracy and [GOVERNANCE.md](../../GOVERNANCE.md) — paid status does not buy roadmap influence, ADR outcomes, or private features. Sponsorship supports the work; it does not steer it.
- **No purchased stars, downloads, or manufactured hype.** Vanity metrics obtained by payment are dishonest about project health (per [community-operations.md §"Anti-patterns"](community-operations.md)).

## Relationship between commercial entity and OSS project

Separation of concerns is the point. Two entities, aligned interests, clean boundaries.

- **Commercial services operate as a separate legal entity.** LLC or corporation structure is TBD and will be chosen with local tax/legal counsel — rules vary meaningfully by state (Wyoming, Delaware, and the BDFL's state of residence have different cost and filing profiles), single-member vs multi-member tax treatment differs, and IP assignment from the BDFL personally to the entity needs written capture at formation. Formation fires on **the first of** the following, whichever happens first: (a) the repo is made public (the BDFL has elected to keep the repo private until the entity is formed, explicitly coupling the `private → public` flip with LLC formation as a single governance decision), (b) a Bridge-hosted tenant signing a terms-of-service agreement, (c) a paid support or consulting engagement requiring a legal counterparty, or (d) a commercial-license, OEM, or trademark-licensing inquiry requiring a legal counterparty. The entity is formed the same quarter the trigger fires — in the public-release case, the entity is formed *before* the visibility flip, not concurrently. Forming earlier accrues annual fees and bookkeeping against zero revenue; forming later exposes the BDFL personally on the first contract or on public-facing exposure.
- **The commercial entity employs maintainer time** — the BDFL's OSS work is funded by services revenue. Commercial engagements are how the project pays for the BDFL's hours.
- **The commercial entity contributes back upstream.** Improvements discovered during customer work flow into the OSS project. No private forks, no proprietary branches, no "commercial-only" features held back from the public repo.
- **The commercial entity does not gatekeep upstream features.** Services customers benefit from OSS improvements at the same time as everyone else. There is no private release channel.
- **License boundary is enforced in `LICENSE`.** MIT applies to everything in the repo. Commercial services are contracts layered on top; they do not modify license terms.
- **Trademark and brand** (the "Sunfish" name and logo) are held by the commercial entity pre-foundation, with a written commitment to transfer to a neutral entity (foundation or an independent Sunfish Foundation) once foundation membership activates.
- **Fair-use policy for the name.** Other developers can build on Sunfish and advertise that fact — including for commercial offerings. They cannot imply endorsement, official affiliation, or certification by the Sunfish project unless the project has actually conferred it. "Built on Sunfish" is fine. "Sunfish Certified" or "Official Sunfish" is not, pre-foundation.

## Transparency commitment

Financial transparency scales with the funding mechanism in use.

- **Pre-Open-Collective (today).** Informal transparency: the BDFL publishes a rough allocation of OSS-vs-commercial time in any annual project retrospective that gets written. No formal ledger required because there are no project funds to account for.
- **Post-Open-Collective (Level 2 activation).** The Open Collective public ledger is the project's financial record. Every incoming contribution and outgoing expense is visible on the collective's page.
- **Post-foundation (Level 5 activation).** Foundation-standard financial reporting — annual reports, audited financials for 501(c)(3) hosts, trademark usage disclosure, membership roll.

Commercial services revenue and P&L are **not public** — that is the commercial entity's business, not the OSS project's. What is public is the boundary: OSS project funds (Open Collective ledger, foundation reporting) are separate from commercial entity revenue, and transfers between them are disclosed in whichever ledger applies.

## Activation triggers (consolidated)

Tied back to [GOVERNANCE.md §"Transition triggers"](../../GOVERNANCE.md) and [community-operations.md §"Community levels"](community-operations.md):

| Trigger | Sustainability response |
|---|---|
| First external contributor engagement | GitHub Sponsors page enabled (optional, low-friction). No fundraising push. |
| 3+ external committers sustained over 3 months (Level 2) | Open Collective setup via Open Source Collective fiscal host. Public project-fund ledger. `SPONSORS.md` if warranted. |
| Public release of the repo (coupled governance decision — repo stays private until entity is formed) | Commercial entity formed **before** the `private → public` flip (LLC or corporation; choice is counsel-advised per §"Relationship between commercial entity and OSS project"). IP assignment from the BDFL personally to the entity executed at formation. Trademark and domain registrations transferred to the entity. |
| First commercial engagement (Level 4 signal) — hosted-tenant ToS, paid support/consulting contract, or commercial-license inquiry, whichever fires first | If the entity was already formed at public-release (above), contract templates are published (master services agreement, SLA, statement-of-work template, terms of service for hosted tenants). If the engagement trigger fires before public release (rare but possible for private design partners), the entity is formed the same quarter. |
| 3+ organizations in production (Level 5) | Tidelift lifter application for packages with adopter signal. Commercial support tier formalized with published response-time commitments. |
| Foundation membership evaluation (per GOVERNANCE) | Foundation proposal process begins (CNCF Sandbox, LF project, Apache Incubator, or independent). Trademark transfer plan drafted and published. |

No trigger fires "because enough time has passed." Activation is evidence-based.

## Cost posture

Pre-production, infrastructure costs are deliberately near-zero. This is the budget posture that makes pre-revenue survivable.

- **Code hosting:** GitHub free tier (unlimited public repos, GitHub Actions minutes sufficient for current CI scope).
- **Docs hosting:** GitHub Pages (`ctwoodwa.github.io/sunfish/`). Free.
- **Package hosting:** NuGet.org (free for public packages) when Sunfish begins publishing. npm when the UI Core / React adapter ships JavaScript packages.
- **Container images:** GitHub Container Registry (free for public images).
- **Domain:** single domain under the commercial entity; renewal is the one recurring pre-revenue expense.
- **Communication channels:** GitHub Issues and Discussions. Free.
- **CI minutes:** GitHub Actions; free tier sufficient today. If it becomes insufficient, it is a Level 2 activation signal (project funds can cover overflow via the collective).

First-tenant hosted-SaaS cost posture: **cost-plus pricing**. The customer covers the infrastructure (compute, storage, bandwidth, database, backups, monitoring) at cost, plus a margin that reflects Sunfish's operational work. Flat-premium tier pricing is deferred until there is a second hosted customer to compare against.

Scale pricing models for hosted SaaS — per-tenant flat, per-seat, per-action / API call, usage-based, or a mix — are **TBD when hosted SaaS actually activates**. Picking a pricing model in advance of a customer is premature optimization.

## Commercial services pricing posture

Not a price list; a shape.

- **Hourly rates** for implementation and consulting engagements. Published band (e.g., "commercial rates in the range typical for senior-engineer-led .NET / platform consulting") rather than a single fixed rate. Customer agreements specify the exact rate.
- **Monthly subscription** for managed SaaS, scaled by tenant size (seats, active users, storage, federation node count, or a composite). Exact model settles in Level 4.
- **Project-based fixed pricing** for audits (accessibility attestation, privacy compliance review, federation onboarding for a single node) and for scoped implementation work where the deliverable is well-defined.
- **Data portability clause in every contract.** Every commercial services agreement includes a "continue-to-own-your-data" clause reflecting Sunfish's local-first commitment (vision Pillar 1, Kleppmann ideal #7): the customer's data is exportable in a documented format at any time, both during the engagement and after termination. This is not negotiable.
- **No lock-in pricing tricks.** No "free to enter, expensive to exit" patterns. No termination fees that penalize exit. If a customer wants to leave the hosted service and self-host, the migration path is the same data-export-plus-data-import event any self-hoster uses.

## Sustainability risks

What would make Sunfish fail financially, named explicitly so each is watchable:

- **Commercial services revenue cannot cover BDFL time before external revenue arrives.** The pre-customer phase is the highest-risk period. Mitigation: BDFL's non-Sunfish income sustains the project until first customer; the project does not require a salary from Sunfish to continue.
- **First commercial customer takes longer than 18 months to land.** Current targeting is the school-district pilot (per vision). If that timeline slips materially, the project continues but the Level 4 activation triggers slip too. Mitigation: secondary customer targets in other verticals (small medical office, small-landlord SaaS) keep options open.
- **AI productivity advantage compresses faster than expected.** The services-on-OSS economics assume AI remains a meaningful cost primitive through at least 2028. If AI tooling becomes trivially accessible to every competitor before Sunfish reaches sustainability, the margin advantage erodes. Mitigation: Sunfish's moat is domain-specific bundles and federation integration — not AI access itself.
- **A hostile commercial fork captures the commercial-services market.** A well-funded entity forks the OSS, markets itself as the commercial provider, and outspends Sunfish on sales. MIT permits this. Mitigation: the BDFL's trademark on the "Sunfish" name, the project's continued feature velocity, and the direct customer relationships that a fork cannot inherit. Foundation membership (Level 5) further reduces this risk by routing trademark and governance through a neutral body.
- **Legal event disrupts project.** License enforcement dispute, AI-code-provenance lawsuit against the maintainer (per [ai-code-policy.md](../engineering/ai-code-policy.md)), patent assertion by an adversarial contributor (mitigated partially by MIT + DCO; Apache 2.0's patent grant is a trigger-revisable alternative per ADR 0018). Mitigation: commercial entity carries appropriate E&O and cyber-liability insurance once revenue starts; the maintainer does not personally indemnify anyone pre-commercial-entity.
- **Bus factor = 1 materializes.** The BDFL becomes unavailable before a successor or co-maintainer is in place. Mitigation: GOVERNANCE succession protocol; the project's architecture (typed code, documented contracts, AI-comprehensible manifests) is deliberately designed to be pickupable by a new maintainer.

None of these risks is fully mitigated today. Each is a watch-item rather than a solved problem.

## Cross-references

- [vision.md](vision.md) §"Business model" — the operating thesis this document operationalizes.
- [community-operations.md](community-operations.md) — community levels and the activation triggers that parallel the financial ones.
- [roadmap-tracker.md](roadmap-tracker.md) — phase status that drives when Level 4 signals become plausible.
- [compatibility-policy.md](compatibility-policy.md) — pre-1.0 posture that adopter organizations evaluate before commercial contracts.
- [`../../GOVERNANCE.md`](../../GOVERNANCE.md) — governance triggers that parallel sustainability triggers.
- [`../../docs/adrs/0018-governance-and-license-posture.md`](../../docs/adrs/0018-governance-and-license-posture.md) — MIT license decision and DCO-no-CLA posture.
- [`../engineering/ai-code-policy.md`](../engineering/ai-code-policy.md) — AI provenance posture referenced under legal-event risk.

### External references

- [GitHub Sponsors](https://github.com/sponsors) — individual and organization donation channel.
- [Open Collective](https://opencollective.com/) and [Open Source Collective](https://docs.oscollective.org/) — fiscal hosting for OSS projects; 2,500+ projects hosted; 10% fees on funds raised.
- [Tidelift](https://tidelift.com/) — commercial subscription for maintainers of production-adopted OSS dependencies.
- [CNCF Sandbox](https://www.cncf.io/sandbox-projects/) — Cloud Native Computing Foundation's entry tier; trademark transfer + multi-org maintainer requirement.
- [Linux Foundation](https://www.linuxfoundation.org/) — umbrella foundation hosting CNCF and many sub-foundations.
- [Apache Software Foundation](https://www.apache.org/) — mentor-driven incubation model.
- [Sentry's Open Source Pledge](https://opensourcepledge.com/) — the $2,000-per-FTE-per-year pledge model referenced for company-pays-maintainers norms.
- [CHAOSS](https://chaoss.community/) — community health metrics referenced under sustainability watch-items.
- [Server Side Public License (Wikipedia)](https://en.wikipedia.org/wiki/Server_Side_Public_License) — SSPL history; MongoDB (2018) through Redis (2024) license-change pattern.
- [Redis's return to AGPLv3 (May 2025)](https://redis.io/blog/agplv3/) — reversal precedent; "the SSPL, in practical terms, failed to be accepted by the community."
- [Bryan Cantrill and Adam Jacob on corporate OSS antipatterns (P99 CONF 2023)](https://www.p99conf.io/2023/11/07/bryan-cantrill-friends-on-corporate-open-source-antipatterns-at-p99-conf/) — post-open-source discourse on sustainability of unpaid maintainer labor.
- [Open Source Guides: Leadership and Governance](https://opensource.guide/leadership-and-governance/) — reference for evolution paths.
