# Intake — ADR 0007 Amendment A1: `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field

**Date:** 2026-04-30
**Requestor:** XO (declared by ADR 0063 §"Sibling amendment dependencies named" as the coordinated companion amendment to ADR 0063's install-UX layer)
**Request:** Amend ADR 0007 (Bundle Manifest Schema) with a new amendment A1 adding a `Requirements: MinimumSpec?` field to `BusinessCaseBundleManifest`, where `MinimumSpec` is the type defined by ADR 0063 (Mission Space Requirements). The field is optional (nullable); existing bundles default to `null` (no install-time gating; preserves backward compatibility).
**Pipeline variant:** `sunfish-api-change` (introduces new field on a public schema; non-breaking due to nullability)
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

ADR 0063 (Mission Space Requirements; in flight via PR #411) introduces:

- `MinimumSpec` schema (10 per-dimension spec records + `SpecPolicy` enum + `PerPlatformSpec` overrides)
- Install-time UX rendering minimum-spec vs user's MissionEnvelope (Steam-style System Requirements page)
- 4 new `AuditEventType` constants for install-time + post-install regression detection

ADR 0063's substrate (Phase 1) ships independently — types + resolver + 3 platform renderers + audit events. **But it cannot wire bundles' specs until ADR 0007 declares the field on `BusinessCaseBundleManifest`.**

Without ADR 0007-A1, bundles have nowhere to declare their `MinimumSpec`; ADR 0063's substrate has the resolver but no per-bundle data to resolve. The substrate is then a runtime-only mechanism (consumers declare specs at runtime in code), losing the bundle-manifest declarative path that ADR 0063 describes as the canonical authoring model.

## Predecessor

ADR 0007 (BusinessCaseBundleManifest schema) — Accepted on `origin/main`. Currently specifies:

- `BusinessCaseBundleManifest` with `requiredModules: string[]` + `optionalModules: string[]`
- `ProviderRequirement` with `required: bool`
- `ModuleManifest` with `key, name, version, description, capabilities`

(Note: the A6 council on ADR 0028 caught a structural-citation failure where ADR 0028-A6.2 rule 3 cited `required: true` on the wrong tier of ADR 0007's schema — see `feedback_council_can_miss_spot_check_negative_existence` memory's third-direction cohort lesson. This intake is correctly scoped to `BusinessCaseBundleManifest` proper, not to `ModuleManifest`.)

**Why amendment, not new ADR:** the field addition is intrinsic to ADR 0007's bundle-manifest contract. Adding a nullable field is mechanically inside ADR 0007's contract surface.

## Industry prior-art

- **Apple `Info.plist` `UIRequiredDeviceCapabilities` + `MinimumOSVersion`** — iOS App Store filters by these declarations; closest engineering analog
- **Android Manifest `<uses-feature>` declarations** — `android:required="true"` per feature; identical pattern
- **MSBuild `<TargetFramework>` + `<RuntimeIdentifier>`** — .NET equivalent for runtime + platform requirements
- **VS Code Extension Manifest (`engines` field)** — JSON schema declaring extension requirements; minimal but matched to ADR 0007's manifest-as-JSON shape

## Scope

- **Schema field addition:** `BusinessCaseBundleManifest.Requirements: MinimumSpec?` (nullable; defaults to "any")
- **Canonical-JSON encoding:** field encodes via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` per existing ADR 0007 conventions
- **Backward compatibility:** existing bundles continue to install on every device per ADR 0044 + ADR 0048; `null` semantics = "no install-time gating"
- **Forward compatibility:** unknown fields tolerated per CanonicalJson convention (verified per ADR 0028 A6 council F12)
- **Validation:** ADR 0007's existing schema-validation surface gains `MinimumSpec` validation (resolver's structural validation; not behavioral checks against any specific MissionEnvelope)
- **Documentation:** apps/docs entry for ADR 0007 gains a §"Requirements field" section linking to ADR 0063

## Dependencies and Constraints

- **Hard predecessor:** ADR 0007 itself (Accepted on origin/main).
- **Hard predecessor:** ADR 0063 (Mission Space Requirements; in flight via PR #411 — must land before ADR 0007-A1 ships, since A1 cites the `MinimumSpec` type which ADR 0063 introduces).
- **Hard consumer:** ADR 0063 Phase 2 (substrate wiring; Phase 1 substrate ships first; Phase 2 consumes ADR 0007-A1's field).
- **Effort estimate:** small (~3–5h authoring + council review). Mechanical schema-extension amendment.
- **Council review posture:** pre-merge canonical per cohort discipline (13-of-13 substrate amendments needed council fixes; 6-of-13 had structural-citation failures caught pre-merge; ADR 0007's specific susceptibility to structural-citation failures was the original lesson — this amendment is precisely the kind that benefits most from council).

## Affected Areas

- packages/foundation-bundles (or wherever `BusinessCaseBundleManifest` lives in code) — schema field addition
- packages/foundation-mission-space (post-ADR-0063 build) — `MinimumSpec` type used in the field
- apps/docs/foundation/bundle-manifest/ — schema documentation update
- All existing bundle-manifest authors — backward-compat preserved via `null` default; opt-in spec authoring

## Downstream Consumers

- **ADR 0063 Phase 2 build** — wires `MinimumSpec` declarations into install-time evaluation; depends on this field being on `BusinessCaseBundleManifest`
- **Every `Sunfish.Blocks.*` bundle** — gains the option to declare its `Requirements`
- **kitchen-sink demo bundle** — gets a meaningful `Requirements` declaration as the first reference example

## Next Steps

Promote to active workstream when CO confirms; XO authors the A1 amendment; pre-merge council; merge under the same auto-merge-disabled-then-re-enabled pattern as prior substrate amendments. Recommend authoring **after** ADR 0063 settles (currently PR #411 with auto-merge intentionally DISABLED until council); ADR 0007-A1 cites ADR 0063's `MinimumSpec` type which must exist on `origin/main` before A1 can ship structurally.

## Cross-references

- Parent: ADR 0063 §"Sibling amendment dependencies named" (declaration)
- Parent ADR: ADR 0007 (BusinessCaseBundleManifest schema)
- ADR 0063 PR: #411 (in flight; auto-merge DISABLED pending council)
- W#33 follow-on queue: `project_workstream_33_followon_authoring_queue.md` (memory)
- Cohort lesson: ADR 0007's structural surface was the original A7 lesson — verify this amendment cites ADR 0007 fields STRUCTURALLY correctly (the new field is on `BusinessCaseBundleManifest`, NOT on `ModuleManifest`)
