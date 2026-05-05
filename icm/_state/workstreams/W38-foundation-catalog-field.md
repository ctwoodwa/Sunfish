---
sort_order: 41
number: 38
slug: foundation-catalog-field
title: "Foundation.Catalog `BusinessCaseBundleManifest.Requirements` field (ADR 0007-A1 contract surface)"
status: "built"
status_cell: "`built` (1 PR shipped 2026-05-01)"
owner: "sunfish-PM"
owner_cell: "sunfish-PM ✓"
reference_cell: "This PR"
---

## Notes

**Built 2026-05-01 in single PR per stub-unblock addendum.** `BusinessCaseBundleManifest` gains optional `Requirements: MinimumSpec?` field with `null` default + `[JsonPropertyName("requirements")]` + `[JsonIgnore(WhenWritingNull)]` so existing manifests serialize unchanged. `MinimumSpec` (record with `Policy: SpecPolicy = Recommended`) + `SpecPolicy` (3-value enum: Required/Recommended/Informational) ship as **local stubs in `foundation-catalog`** per the W#38 stub-unblock addendum (`foundation-catalog-requirements-field-stage06-addendum.md`); each stub carries a TODO referencing ADR 0063 + the future-rename plan to `Sunfish.Foundation.MissionSpace.MinimumSpec` when canonical substrate lands. **8 new tests in foundation-catalog suite, all green** (71/71 total) — null-default backward-compat / canonical-JSON round-trip / camelCase field-name `"requirements"` / SpecPolicy literal-string round-trip / JsonIgnoreCondition.WhenWritingNull omits null / pre-A1 manifest deserializes with Requirements=null / all 3 SpecPolicy values round-trip / forward-compat through deserialize-then-serialize. All 4 hand-off halt-conditions stayed off the trip-wire (#1 resolved via stub-unblock; #2-#4 verified clean). Substrate-only; consumer wiring (ADR 0063 Phase 2 per-bundle declarations) separate when ADR 0063 substrate ships. Cohort patterns: PascalCase enum literals + camelCase property names + `JsonStringEnumConverter` for SpecPolicy.
