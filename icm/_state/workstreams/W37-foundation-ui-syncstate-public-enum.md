---
sort_order: 40
number: 37
slug: foundation-ui-syncstate-public-enum
title: "Foundation.UI.SyncState public enum (ADR 0036-A1 contract surface)"
status: "built"
status_cell: "`built` (1 PR shipped 2026-05-01)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "This PR"
---

## Notes

**Built 2026-05-01 in single PR per hand-off scope.** New `packages/foundation-ui-syncstate/` package (COB picked the new-package path over the foundation-localfirst extension — UI-tier vs sync-substrate-tier separation per the hand-off recommendation). 5-value `SyncState` enum (Healthy / Stale / Offline / Conflict / Quarantine) per A1.1. `SyncStateExtensions.ToCanonicalIdentifier()` + `TryFromCanonicalIdentifier(string)` round-trip helpers per A1.2 — canonical lowercase wire form (`healthy` / `stale` / `offline` / `conflict` / `quarantine`) parsed ordinal-only so external-consumer drift surfaces here. `JsonStringEnumConverter` paired with `JsonNamingPolicy.CamelCase` produces the canonical lowercase form (single-word identifiers flat-case identically). Round-trips through `Sunfish.Foundation.Crypto.CanonicalJson.Serialize`. **20 tests in the foundation-ui-syncstate suite, all green** — 5 enum-value round-trips + 5 canonical-identifier parses + 6 drift-rejection cases (PascalCase / ALLCAPS / mixed case / null / empty / unknown) + unknown-enum-value `ArgumentOutOfRangeException` + JsonSerializer round-trip + CanonicalJson round-trip + dictionary-key context pin. All 4 halt-conditions stayed off the trip-wire (no namespace collision verified pre-build; CanonicalJson round-trip clean; dictionary-key context works value-typed; new-package decision documented). Substrate-only; consumer wiring (ADR 0063 SyncStateSpec consumer) is separate workstream. Cohort patterns: PascalCase enum literals + camelCase property names + lowercase wire-form for enum values, mirroring W#34 + W#35 substrate conventions.
