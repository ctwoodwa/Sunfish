# Intake Note — Governance Docs Reframe (Project Mode)

**Date:** 2026-04-21
**Variant:** sunfish-docs-change
**Mode:** Fast-track (docs-only, no code changes)
**Acceleration:** Skipping 01_discovery, 02_architecture, 03_package-design, 04_scaffolding — proceeding directly from intake to implementation-plan (05) + build (06).

---

## Request summary

Align all `/_shared/` documents with Sunfish's actual purpose as an AI-coding-agent learning vehicle, and close concrete issues from a multi-perspective review. Three categories of work:

1. **Project Mode reframe** — strip commercial scaffolding that assumes an enterprise buyer who does not exist; keep optionality.
2. **Contradiction resolution** — pick one canonical answer where two docs disagreed.
3. **Token-cost audit** — relocate misfiled docs, trim bloated docs, add agent-relevance markers.

## Affected areas

- `_shared/product/` — vision.md, sustainability.md, community-operations.md, roadmap-tracker.md, compatibility-policy.md, naming.md, (+ documentation-framework.md moving here)
- `_shared/engineering/` — releases.md, ai-code-policy.md, code-review.md, testing-strategy.md, ci-quality-gates.md, data-privacy.md, commit-conventions.md, coding-standards.md + all 30 docs for agent-relevance notes
- `_shared/design/` — component-principles.md, accessibility.md (documentation-framework.md moving out)
- `docs/research/` — new directory; receives 4 research files from `_shared/engineering/`
- `icm/CONTEXT.md`, `icm/_config/routing.md` — fast-track-as-default documentation

## No code changes

This work touches only `_shared/`, `_shared/design/`, `docs/`, and `icm/`. No packages, no apps, no tooling.

## Pipeline variant

**sunfish-docs-change** — fast-tracked per CLAUDE.md ("docs-change: skip architecture and package-design").

## Acceleration rationale

All changes are editorial / organizational. The request's work plan is the implementation plan; no discovery or architecture phase needed.

---

*Fast-track approved at intake. Proceeding to 06_build.*
