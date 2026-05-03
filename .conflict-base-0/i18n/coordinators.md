# Sunfish Locale Coordinators

> Authoritative roster of named coordinators per locale. Required by spec §4 line 387:
> "All Phase 1 coordinators named before Phase 1 Day 1 in `i18n/coordinators.md`."

**Last updated:** 2026-04-24 (Plan 4 supplementary; Phase 1 Day 1 prerequisite).
**Source-of-truth pairing:** [`i18n/locales.json`](./locales.json) — coordinator field per locale must
match this file.

---

## What "coordinator" means

A coordinator is the named accountable owner for a locale's translation quality. Responsibilities:

1. Maintain the locale at or above its `completenessFloor`.
2. Approve translator-authored XLIFF entries before they advance from `state="translated"` to `state="final"`.
3. Triage `i18n.missing_key` telemetry and flag systemic gaps.
4. Sign off on tier transitions (e.g., `bake-in` → `complete` requires the coordinator's review).
5. First responder for locale-specific bugs filed against `ui-core` / `ui-adapters-*` packages.

Coordinator field values per spec §4:

- `@<github-handle>` — named individual owner.
- `vendor:<name>` — commercial service engagement (e.g., `vendor:gengo`, `vendor:transperfect`).
- `community:open-recruiting` — open recruitment ticket; **allowed for `bake-in` tier only;
  prohibited for `complete` tier**.

---

## Roster

| Locale | Tier | Coordinator | Status | Notes |
|---|---|---|---|---|
| `en-US` | complete | `@chriswood` | **named** | Source locale; the de facto coordinator until LLC formation reorgs ownership. |
| `es-419` | complete | `community:open-recruiting` | **OPEN — promotion required** | Recruit before Phase 1 Day 1. Tier `complete` prohibits this value; either name a coordinator or downgrade to `bake-in`. |
| `pt-BR` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same as es-419. |
| `fr` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. |
| `de` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. |
| `ja` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. Japan-market commercial tier; vendor engagement may be the right call. |
| `zh-Hans` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. China-market commercial tier. |
| `ar-SA` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. RTL proof locale; coordinator should also vouch for layout under RTL. |
| `hi` | complete | `community:open-recruiting` | **OPEN — promotion required** | Same. India-market mission-aligned tier per book. |
| `he-IL` | bake-in | `community:open-recruiting` | acceptable | bake-in tier permits open recruitment per spec §4. |
| `fa-IR` | bake-in | `community:open-recruiting` | acceptable | Same. |
| `ko` | bake-in | `community:open-recruiting` | acceptable | Same. |

---

## Action: Promotion gate before Phase 1 Day 1

The 8 `complete`-tier locales currently use the `community:open-recruiting` placeholder, which the
spec **prohibits** for that tier. Before Phase 1 Day 1:

- **Option 1 (preferred for paid tiers):** engage a translation vendor (`vendor:<name>`) for
  es-419, pt-BR, fr, de — Plan 6 budgets for these as paid tiers ($0.08–0.15/word per the
  Plan 6 translator-volume estimate).
- **Option 2:** recruit named volunteer coordinators with the right language credentials and
  community ties for ja, zh-Hans, ar-SA, hi.
- **Option 3:** downgrade specific locales from `complete` → `bake-in` if coordinator search
  fails. Spec §4 mutation policy: lowering tier is `sunfish-feature-change` (user-visible
  commitment weakened).

The mandate "global UX as first-class, not bolted on" precludes Option 3 as a default escape
hatch — it should fire only when Option 1 + 2 have demonstrably failed for a specific locale.

---

## Coordinator transition policy

Replacing a coordinator mid-locale-cycle is a `sunfish-docs-change` (per spec §4):

1. New coordinator named in this file.
2. Update `i18n/locales.json` to match.
3. Outgoing coordinator hands off any in-flight XLIFF reviews.
4. New coordinator runs `tooling/locale-completeness-check/` against their locale within
   30 days of taking over to confirm they can drive the metric.

If a coordinator goes inactive (no XLIFF reviews for 90 consecutive days, no response to
`i18n.missing_key` telemetry alerts), the BDFL initiates a transition.

---

## Vendor engagement template

When recruiting a `vendor:<name>` coordinator:

- Vendor MUST agree to per-locale completeness-floor SLA (95% for `complete` tier).
- Vendor's translator team must include native speakers for review work, not only freelance pool.
- Glossary (`localization/glossary/sunfish-glossary.tbx`) must be loaded into vendor's TM.
- AGPL §13 caveat: if Sunfish's hosted Weblate exposure scope changes (e.g., commercial managed
  translation hosting), legal review of the vendor's modified-source-availability obligations
  per [`waves/global-ux/decisions.md`](../waves/global-ux/decisions.md) entry 2026-04-25.

---

## Cross-references

- [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../docs/superpowers/specs/2026-04-24-global-first-ux-design.md) §4 — locales spec (coordinator field rules)
- [`i18n/locales.json`](./locales.json) — machine-readable locale list
- [`localization/glossary/sunfish-glossary.tbx`](../localization/glossary/sunfish-glossary.tbx) — translator glossary
- [Plan 3 — Translator-Assist](../docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md) — vendor recruitment runbook
- [Plan 6 — Phase 2 Cascade](../docs/superpowers/plans/2026-04-24-global-first-ux-phase-2-cascade-plan.md) — translator-volume budget estimate
