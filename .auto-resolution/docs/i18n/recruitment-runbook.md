# Translator recruitment runbook

> **STATUS: SKELETON.** Wave-2-Plan3-Cluster-C scaffold. Each section below
> is a one-line placeholder; full content lands per Plan 3 task 4.x.
> Note: Plan 3 §File Structure references `docs/i18n/translator-recruitment.md`;
> the Phase 1 Finalization Loop brief renamed this file to `recruitment-runbook.md`
> for consistency with `review-guide.md`. If the divergence matters, reconcile
> in a follow-up commit.

## Table of contents

- [Purpose](#purpose)
- [Target locales](#target-locales)
- [Translator selection criteria](#translator-selection-criteria)
- [Onboarding workflow](#onboarding-workflow)
- [Compensation model](#compensation-model)
- [Quality review cadence](#quality-review-cadence)
- [Off-boarding](#off-boarding)

## Purpose

This runbook defines how Sunfish recruits, onboards, pays, and (when
necessary) off-boards translators contributing to the 12 Phase 1 locales.
It is the operational companion to the `review-guide.md` reviewer playbook.

TODO: expand per Plan 3 task 4.x.

## Target locales

Phase 1 covers 12 locales: **en-US** (source), **es-419** (Latin American
Spanish), **pt-BR** (Brazilian Portuguese), **fr** (French), **de**
(German), **ja** (Japanese), **zh-Hans** (Simplified Chinese), **ar-SA**
(Arabic, Saudi Arabia — RTL anchor), **hi** (Hindi), **he-IL** (Hebrew —
second RTL coverage), **fa-IR** (Persian — third RTL coverage), and **ko**
(Korean). The four "paid" locales are en-US, es-LA, fr, de; the rest
target volunteer recruitment.

TODO: expand per Plan 3 task 4.x.

## Translator selection criteria

Translators must be native or near-native in the target locale, comfortable
with technical UI strings (not literary translation), and willing to use
Weblate's review surface. Prior open-source localization experience is a
plus; ICU MessageFormat / SmartFormat familiarity is required for paid
locales and preferred for volunteer locales.

TODO: expand per Plan 3 task 4.x.

## Onboarding workflow

1. **Outreach** — recruit via LinkedIn, ProZ, the Weblate volunteer network, and open-source contributor channels.
2. **Screening** — short paid trial: translate a 30-segment fixture; reviewer scores against the rubric in `review-guide.md`.
3. **Account provisioning** — create Weblate account, add to the locale-specific team, share glossary walkthrough and `review-guide.md`.
4. **First real component** — assign a low-stakes component (e.g., `kitchen-sink` UI strings) before any production-facing component.
5. **Two-week check-in** — reviewer pairs with the translator on three of their merged segments; calibration adjustments captured in the translator's notes file.

TODO: expand per Plan 3 task 4.x.

## Compensation model

Paid locales (en-US, es-LA, fr, de) follow the per-segment rate schedule
in the Phase 1 budget envelope; volunteer locales receive named
acknowledgment in the Sunfish CONTRIBUTORS file plus annual swag. Link to
Plan 6 budget envelope: `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md` (budget section TBD).

TODO: expand per Plan 3 task 4.x.

## Quality review cadence

Every translator's first 30 merged segments are 100%-reviewed; after that,
review drops to a 10% sampling rate, with full review re-triggered if any
sampled segment fails the rubric. Per-locale review burden is tracked in
the Weblate analytics dashboard.

TODO: expand per Plan 3 task 4.x.

## Off-boarding

Inactive translators (no merged segment in 90 days) are moved to "alumni"
status: account is preserved read-only, glossary access retained for
reference, notification opt-out enabled. Translators who repeatedly fail
the quality rubric (three consecutive failed reviews) are off-boarded with
a written summary of the rubric findings.

TODO: expand per Plan 3 task 4.x.
