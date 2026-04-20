# ADR 0018 — Governance Model and License Posture

**Status:** Accepted
**Date:** 2026-04-20
**Resolves:** Codify Sunfish's pre-release governance model (BDFL), license choice (MIT), and the frameworks layered on top (ODF, UPF, ICM) so the decision is in the architectural trail and revisable against named transition triggers.

---

## Context

Sunfish is pre-release and pre-community. The project's community health files (README, LICENSE, CODE_OF_CONDUCT, CONTRIBUTING, CODEOWNERS, SECURITY, issue templates, PR template) exist, but the governance model itself is undocumented. As the project opens to external contributors — especially around the first commercial customer (the school district per vision milestones) — ambiguity about *who decides* and *how* becomes a liability.

Three adjacent frameworks have been adopted in recent ADRs and docs:

- **[Integrated Change Management (ICM)](../../icm/CONTEXT.md)** — Sunfish's workflow-stage orchestration (exists).
- **[Universal Planning Framework (UPF)](../../.claude/rules/universal-planning.md)** — Plan-quality discipline ([ADR follow-up tracked in `_shared/engineering/planning-framework.md`](../../_shared/engineering/planning-framework.md)).
- **[Red Hat Open Decision Framework (ODF)](https://github.com/red-hat-people-team/open-decision-framework)** — Decision-transparency framework proposed in prior governance research.

What's missing is the meta-doc that names the governance model, ties the three frameworks together, records the license choice, and defines transition triggers. This ADR records that decision; the practical form is `GOVERNANCE.md` at the repo root.

---

## Decision

### 1. Governance model: BDFL with explicit transition triggers

Sunfish is led by a single Benevolent Dictator for Life (BDFL) — [@ctwoodwa](https://github.com/ctwoodwa) — who has final authority over all architectural, roadmap, release, and community decisions. This is honest about the current reality of every pre-community OSS project rather than pretending at a broader governance body that doesn't exist yet.

Governance **evolves on named triggers**, not calendars. The triggers and their resulting governance changes are enumerated in [`GOVERNANCE.md`](../../GOVERNANCE.md) §"Transition triggers" — reproduced briefly:

- 3+ sustained external committers → add maintainer tier.
- 10+ external committers → consider Technical Steering Committee.
- First disputed ADR → formalize RFC process beyond GitHub Discussions.
- First production corporate adopter → publish SLA/SLO; revisit license + CLA.
- 3+ unrelated organizations in production → evaluate foundation membership.
- First hostile fork or governance complaint → tighten acceptance criteria.
- BDFL unavailable >30 days → activate succession protocol.

### 2. Four-framework stack

Sunfish layers four frameworks at distinct altitudes:

| Framework | Altitude | What it owns |
|---|---|---|
| **GOVERNANCE.md** (this doc) | Meta | Who decides, with what accountability, under what evolution plan. |
| **ODF** (Red Hat) | Decision transparency | Problem / constraints / criteria shared publicly; external input invited; meritocratic decision. |
| **UPF** (Primeline AI) | Plan rigor | Three-stage planning (Discovery → Plan → Meta-Validation) with optional Stage 1.5 Autonomous Hardening, DSV principle, 21 anti-patterns, C/B/A quality rubric. |
| **ICM** (Sunfish) | Workflow | Nine numbered stages from `00_intake` through `08_release`. |

ODF sits *inside* governance's decision lifecycle; UPF sits *inside* ODF's Planning-and-Research phase; ICM stages receive UPF-quality artifacts. The frameworks don't compete — they answer different questions.

### 3. License: MIT

Sunfish is licensed under the **[MIT License](../../LICENSE)** (Copyright © 2026 Christopher Wood). The choice was made at project inception and is reaffirmed here in the ADR trail:

- **MIT** is the most permissive mainstream OSS license (92% of OSS projects in 2025 per OSSRA); simplest for adopters.
- Apache 2.0 was considered; its explicit patent grant is its main advantage. The project will revisit if a downstream adopter raises patent-exposure concerns.
- Source-available licenses (BSL, SSPL, Sustainable Use) were rejected. They are not OSI-approved and conflict with Sunfish's business model (commercial revenue from services, not license restrictions).
- GPL / AGPL were rejected. Their copyleft requirements conflict with Sunfish's "consumers ship proprietary products on top" premise.

### 4. Contributor IP: DCO, not CLA

Contributors retain copyright. IP provenance is asserted through the **Developer Certificate of Origin** — `git commit --signoff` / `-s` on every commit. No CLA is required. When the first corporate contributor raises a CLA requirement (expected in the "first production corporate adopter" trigger), this is revisited.

### 5. Security posture

Vulnerability reporting is via **GitHub private Security Advisories** per [`.github/SECURITY.md`](../../.github/SECURITY.md). Coordinated disclosure; fixes ship before public disclosure; expected response within 7 days; supported version is `main` only until a stable release is tagged.

### 6. Deferred until triggers fire

To avoid pre-community LARPing:

- No Technical Steering Committee today.
- No working groups.
- No foundation membership.
- No CHAOSS instrumentation.
- No formal RFC process beyond GitHub Discussions + the RFC issue template.
- No CLA.
- No SLA commitments (Sunfish is pre-release).

Each is explicitly deferred rather than ignored. A trigger in `GOVERNANCE.md` names when to reopen the question.

---

## Consequences

### Positive

- **Honest posture** that contributors and adopters can evaluate. No ambiguity about authority.
- **Scalable shape.** The BDFL model has evolved into steering committees and foundation governance at Python, Linux, Django, Rust, and countless others. Triggers are the evolution path.
- **Frameworks layer cleanly.** ODF + UPF + ICM + governance doc cover the decision / planning / workflow surface without duplication.
- **MIT adoption friction is near zero.** License-review questions from potential adopters answer themselves.
- **DCO keeps contributor friction low.** Signoff is a one-flag commit; no legal document to execute.

### Negative

- **Bus factor = 1.** This is real. `GOVERNANCE.md` §"Succession and bus factor" acknowledges and commits to addressing it before production adoption.
- **MIT lacks Apache 2.0's patent grant.** An adversarial patent holder among contributors could theoretically assert claims. Low probability today; flagged as a trigger-revisable choice.
- **BDFL authority concentrates risk.** Bad decisions by one person are harder to catch without a steering committee's check. Offset by ODF's public-decision requirement and ADR trail.

### Follow-ups

1. **Publish `GOVERNANCE.md`** at the repo root (concurrent with this ADR).
2. **Add the RFC issue template** for design-proposal issues (`.github/ISSUE_TEMPLATE/rfc.yml`).
3. **Add the issue-template config** to disable blank issues and route users to Discussions and Security Advisories (`.github/ISSUE_TEMPLATE/config.yml`).
4. **Update `CONTRIBUTING.md`** to reference `GOVERNANCE.md`.
5. **Refresh `CODEOWNERS`** to cover foundation packages added in recent ADRs (0005, 0008, 0009, 0012, 0013).
6. **Run OpenSSF Scorecard baseline** against the repo as a hygiene snapshot (automated; not an ADR commitment).

---

## References

- [`GOVERNANCE.md`](../../GOVERNANCE.md) — the doc this ADR ratifies.
- [`CONTRIBUTING.md`](../../CONTRIBUTING.md) — how contributors engage.
- [`CODE_OF_CONDUCT.md`](../../CODE_OF_CONDUCT.md) — conduct standard.
- [`LICENSE`](../../LICENSE) — MIT License text.
- [`.github/SECURITY.md`](../../.github/SECURITY.md) — vulnerability reporting.
- [`_shared/engineering/planning-framework.md`](../../_shared/engineering/planning-framework.md) — UPF adoption + ODF + ICM mapping.
- [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md) — UPF rule file (MIT-licensed, Primeline AI).
- [Red Hat Open Decision Framework](https://github.com/red-hat-people-team/open-decision-framework) — ODF source.
- [Open Source Guides: Leadership and Governance (GitHub)](https://opensource.guide/leadership-and-governance/) — BDFL-to-broader-governance reference paths.
- [OpenSSF Best Practices Badge](https://openssf.org/best-practices-badge/) — deferred target; Passing level when first adopter signals.
