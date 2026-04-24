# ADR 0035 — Global Domain Types as a Separate Wave

**Status:** Proposed (2026-04-27)
**Date:** 2026-04-27
**Deciders:** Chris Wood (BDFL)
**Related ADRs:** [0015](./0015-module-entity-registration.md) (module-entity registration), [0018](./0018-governance-and-license-posture.md) (governance and license posture)
**Related spec:** [`docs/superpowers/specs/2026-04-24-global-first-ux-design.md`](../superpowers/specs/2026-04-24-global-first-ux-design.md) §3C (Scope boundary)

---

## Context

During Global-First UX brainstorming, eleven improvements to Sunfish's localization and internationalization infrastructure were surfaced. Four of those improvements are **domain-modeling** changes, not localization-infrastructure changes:

- `PersonalName` value object replacing bare `FirstName` / `LastName` fields (covers mononyms, patronymics, CJK family-name-first, Hispanic dual surnames, Arabic kunya/nasab chains).
- `Money` value object replacing bare `decimal` amounts (covers currency coupling, minor-unit arithmetic, locale-aware formatting, exchange-rate history).
- `Address` templates replacing flat US-biased address fields (covers country-specific formats per CLDR `postalCodeData`, PO-box semantics, state vs. prefecture vs. oblast).
- `NodaTime` migration replacing `DateTime` (covers time zones as first-class, avoids DST ambiguity, clarifies `Instant` vs. `LocalDateTime` vs. `ZonedDateTime`).

Universal-planning review of the spec's Section 3 flagged inclusion of these four in the Global-First UX sprint as an instance of the timeline-fantasy anti-pattern: rough effort is ~30+ person-weeks (schema migrations across every persisted entity, breaking-change policy sign-off, cross-repo ripple effects, data-migration tooling) presented in spec drafts as ~1 week of "add the value objects."

The `IStringLocalizer<T>` wiring that Global-First UX Phase 1 installs is **independent** of whether the underlying types are `string firstName` or `PersonalName firstName`. The localizer consumes whatever types it is handed; the value-object migration changes what those types are, not how they are localized.

---

## Decision

Split the four domain-type migrations into a separate wave with its own ADR(s), owner, scope, timeline, and migration plan. Global-First UX Phase 1 and Phase 2 ship **without** them. When the separate wave lands, the already-wired `IStringLocalizer<T>` consumes the new value objects transparently — no UX-layer rework is required.

Explicit in-scope items for the separate wave (names, not detailed plan):

1. `Sunfish.Foundation.People.PersonalName` — Kleppmann-style name model with script-aware display.
2. `Sunfish.Foundation.Money.Money` — currency-coupled amount with ISO 4217 identity.
3. `Sunfish.Foundation.Addresses.Address` — country-template driven address record.
4. NodaTime migration for all persisted `DateTime` fields.

Each is a breaking schema change; each needs its own ADR covering migration strategy, downstream adapter updates, and the governance sign-off that ADR 0018 requires.

---

## Consequences

### Positive

- Global-First UX Phase 1 scope is achievable in 6–8 weeks as spec'd. No hidden 30 person-weeks of domain migration.
- Domain-type migration has room for its own decomposition, migration plan, and breaking-schema-change policy discussion.
- Stakeholder expectations align with engineering reality: "first-class internationalization" lands in the UX layer now; "first-class global domain modelling" lands in a subsequent wave.

### Negative / costs

- Until the separate wave lands, `DateTime` remains in domain code; `decimal` remains for money; `FirstName` / `LastName` columns remain in entity models. This is explicit technical debt tracked to the separate wave.
- Some Global-First UX demo scenarios (e.g., "display a Japanese customer's name correctly with surname first") will need string-template workarounds in Phase 1–2 and be revisited when `PersonalName` ships.
- Documentation must be explicit that the "global-first" mandate has two tracks: UX-layer (this wave) and domain-layer (separate wave). Users reading only the UX-layer material may believe the domain side is also done.

### Trust impact

None — this is a planning decision. No protocol, persistence, or runtime behaviour changes.

---

## Alternatives considered

**Option A — Inline domain-type migration into Phase 1.** Rejected: timeline fantasy. Compressing 30+ person-weeks into a 6–8 week sprint either ships broken migrations or postpones the UX deliverables that motivated the sprint.

**Option B — Indefinite deferral.** Rejected: the spec treats these types as first-class for a reason — the mandate is global UX, not just English-with-Unicode. Indefinite deferral is abandonment in disguise. "Separate wave with a named owner and scheduled start" is the smallest commitment consistent with the mandate.

**Option C — Partial inclusion (e.g., NodaTime only).** Rejected: each of the four types touches a different subset of the persistence layer; partial inclusion creates a half-migrated schema and doesn't reduce the Phase 1 risk enough to justify the complexity. All four in, or all four out, of Phase 1.

---

## Rollout

- No Phase 1 Week 1 work item depends on the separate wave starting.
- ADR 0018 (governance-and-license-posture) sign-off on the separate wave is a prerequisite before its kick-off, not before this ADR's acceptance.
- The wave owner will be named in a follow-up commit to `waves/` once the Global-First UX Phase 1 go/no-go gate passes.
