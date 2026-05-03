# Hand-off — Foundation.Catalog `BusinessCaseBundleManifest.Requirements` field (ADR 0007-A1)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0007 amendment A1](../../docs/adrs/0007-bundle-manifest-schema.md) (landed via PR #438)
**Approval:** ADR 0007-A1 Accepted on origin/main; pre-merge council waived per Decision Discipline Rule 3 (mechanical schema-extension precedent matching ADR 0036-A1)
**Estimated cost:** ~3–5h sunfish-PM (foundation-catalog field addition + validation + ~8 tests + apps/docs entry)
**Pipeline:** `sunfish-api-change`
**Audit before build:** `git grep -n "BusinessCaseBundleManifest" packages/foundation-catalog/` to confirm the type's current shape (audit not yet run; COB confirms before commit)

---

## Context

ADR 0007-A1 ratifies a `Requirements: MinimumSpec?` field addition to `BusinessCaseBundleManifest` per ADR 0063 §"Sibling amendment dependencies named." Without this field, ADR 0063 Phase 2 wiring cannot proceed (per-bundle `MinimumSpec` declarations would have no place to live).

**Backward-compat preserved.** Existing manifests default to `null` for the new field; no behavior change for bundles that don't opt in.

This is the smallest ADR-tier-amendment-derived hand-off (W#37 SyncState was smallest of the cohort; W#38 is similar shape — single-file schema extension + tests + apps/docs).

---

## Files updated

### `packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs` (or equivalent path)

The existing record gains one new optional field:

```csharp
public sealed record BusinessCaseBundleManifest(
    // ... existing fields ...
    string Key,
    string Name,
    string Version,
    string? Description,
    BundleCategory Category,
    BundleStatus Status,
    string Maturity,
    IReadOnlyList<string> RequiredModules,
    IReadOnlyList<string> OptionalModules,
    IReadOnlyDictionary<string, string> FeatureDefaults,
    IReadOnlyDictionary<string, IReadOnlyList<string>> EditionMappings,
    IReadOnlyList<DeploymentMode> DeploymentModesSupported,
    IReadOnlyList<ProviderRequirement> ProviderRequirements,
    IReadOnlyList<string> IntegrationProfiles,
    IReadOnlyList<string> SeedWorkspaces,
    IReadOnlyList<string> Personas,
    string? DataOwnership,
    string? ComplianceNotes,

    // NEW (A1):
    MinimumSpec? Requirements = null    // optional; null = "no install-time gating"; per ADR 0063 MinimumSpec
);
```

Default value `null` is critical for backward-compat; existing manifests deserialize correctly with `Requirements = null`.

The exact field-position in the record's positional-record signature is COB's call (typically optional fields go at the end of positional records to allow easier addition without breaking constructor calls). If the record is class-style with `init`-properties, positioning is irrelevant.

### Validation (per A1.3)

ADR 0007's existing manifest-validation surface (per `IBundleCatalog` contract) gains structural validation:

- Field is optional — absence is valid (`Requirements == null`).
- When present, MUST be a valid `MinimumSpec` instance. Per-value validation is delegated to ADR 0063's `IMinimumSpecResolver` (post-ADR 0063 Phase 1 substrate; not in W#38 scope).
- `Requirements.Policy` (a `SpecPolicy` enum) MUST be one of `Required` / `Recommended` / `Informational` per ADR 0063.

**No new exceptions.** Existing manifest-validation failures continue via the existing error path.

### Tests

```
packages/foundation-catalog/tests/Bundles/
├── BusinessCaseBundleManifestRequirementsFieldTests.cs   (new file)
│   ├── Requirements_NullByDefault_PreservesBackwardCompat
│   ├── Requirements_NotNull_RoundTripsViaCanonicalJson
│   ├── Requirements_FieldNameInJson_IsCamelCase     // "requirements" not "Requirements"
│   ├── Requirements_SpecPolicy_RoundTripsAsLowercaseString  // per ADR 0063-A1.13 if landed; else PascalCase
│   ├── Requirements_NullSerialization_OmitsField    // JsonIgnoreCondition.WhenWritingNull
│   ├── Validation_RequirementsAbsent_Passes         // structural validator
│   ├── Validation_RequirementsPresentWithInvalidPolicy_Fails  // expects Required/Recommended/Informational
│   └── ForwardCompat_PreA1ManifestSerializedByPostA1Receiver_RoundTripsCleanly
```

8 unit tests covering the round-trip + backward-compat + forward-compat + validation paths.

### apps/docs entry

Update `apps/docs/foundation-catalog/bundle-manifest/overview.md` (if present) OR the closest existing apps/docs walkthrough for ADR 0007 to add a §"Requirements field" section briefly explaining:

- The field is optional; null = no install-time gating
- The field type `MinimumSpec` is defined by ADR 0063 (link)
- Bundle authors opt in to declaring `Requirements` per the install-UX they want; ADR 0063's Steam-style System Requirements page renders the spec at install time

Two-three paragraphs is sufficient; this is a small-surface field addition, not a new architectural concept.

---

## Phase breakdown (~1 PR atomic; ~3-5h)

This hand-off ships as a **single atomic PR** (no multi-phase split given small scope; mirrors W#37 shape).

PR contents:

1. `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field added with `null` default
2. Validation extension (structural; presence-optional + type-shape check)
3. 8 unit tests covering round-trip + backward-compat + forward-compat + validation
4. apps/docs §"Requirements field" subsection
5. Active-workstreams.md row 38 added (`ready-to-build` → `built` in same PR; atomic for small surface)

**Acceptance:**
- [ ] `Requirements: MinimumSpec?` added to `BusinessCaseBundleManifest`; default `null`
- [ ] CanonicalJson round-trip passes; field name `"requirements"` in JSON; null omitted via `JsonIgnoreCondition.WhenWritingNull`
- [ ] Backward-compat test: pre-A1 manifest deserializes with `Requirements == null`
- [ ] Forward-compat test: post-A1 manifest's `requirements` field round-trips losslessly via CanonicalJson unknown-key tolerance (per ADR 0028-A6 council F12 verification)
- [ ] Structural validation: presence-optional; SpecPolicy enum constrained
- [ ] apps/docs §"Requirements field" subsection added + cites ADR 0063
- [ ] active-workstreams.md row 38 lands as `built`

---

## Halt-conditions (cob-question if any of these surface)

1. **`MinimumSpec` type unavailable.** Per ADR 0063 Phase 1 substrate (not yet built; queued via downstream consumer), `MinimumSpec` lives in `Sunfish.Foundation.MissionSpace`. If the type doesn't exist on origin/main yet, the field's type signature can't be `MinimumSpec?` — it would need to be `string?` (raw JSON) OR `JsonNode?` until ADR 0063 Phase 1 substrate ships. Recommend: file `cob-question-*` beacon if this hits; the answer is likely "ship the field as `JsonNode?` in W#38; tighten to `MinimumSpec?` via a future amendment when ADR 0063 Phase 1 substrate lands."

2. **Validation seam unclear.** ADR 0007 mentions "IBundleCatalog contract" for manifest validation, but the actual validation seam may live in a separate validator class. If COB can't find the canonical validation path, file `cob-question-*` beacon — the answer may be "add validation as a separate small `IBundleManifestValidator` interface" rather than extending the existing surface.

3. **`SpecPolicy` enum encoding.** Per ADR 0063 (post-A1), `SpecPolicy` is an enum (`Required` / `Recommended` / `Informational`). Round-trip via CanonicalJson should produce camelCase strings (`"required"` / `"recommended"` / `"informational"`) per ADR 0028-A7.8 + W#34 P1 precedent. If the encoding produces PascalCase or numeric, file `cob-question-*` beacon — the W#34 P1 JsonStringEnumConverter pattern is canonical.

4. **CanonicalJson unknown-key tolerance assumption.** The forward-compat test relies on CanonicalJson silently ignoring unknown fields on deserialize (per ADR 0028-A6 council F12 verification). If this assumption breaks under newer System.Text.Json versions, file `cob-question-*` beacon — the test may need to use a typed `Dictionary<string, JsonNode> _unknownFields` catch-all per ADR 0063-A1.6 option (ii).

---

## Cited-symbol verification

**Existing on origin/main (verified 2026-05-01):**

- ADR 0007-A1 (PR #438 merged) — substrate spec source ✓
- ADR 0063-A1 (PR #411 merged post-A1) — `MinimumSpec` type definition ✓
- ADR 0063 SpecPolicy enum (Required / Recommended / Informational) ✓
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` — encoding contract ✓
- `Sunfish.Foundation.Catalog.Bundles` namespace per ADR 0007 §"Decision" ✓
- ADR 0028-A6 council F12 verification (CanonicalJson unknown-key tolerance) ✓
- W#34 P1 PluginId/AdapterId JsonStringEnumConverter precedent ✓

**Introduced by this hand-off:**

- `BusinessCaseBundleManifest.Requirements` field (single field; `MinimumSpec?` typed)
- Validation extension (structural; presence-optional)
- 8 unit tests
- apps/docs §"Requirements field" subsection

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a small mechanical Stage 06 schema-extension. Pre-merge council on this hand-off is NOT required.

- COB's standard pre-build checklist applies
- W#34 + W#37 cohort lessons incorporated: JsonStringEnumConverter for enums; CanonicalJson camelCase round-trip; small-surface single-PR shape

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w38-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 38
- ScheduleWakeup 1800s

If COB completes + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback

---

## Cross-references

- Spec source: ADR 0007-A1 (PR #438 merged 2026-05-01)
- Companion intake: PR #412 (merged); intake stub at `icm/00_intake/output/2026-04-30_bundle-manifest-requirements-field-intake.md`
- Downstream consumer: ADR 0063 Phase 2 wiring (per-bundle MinimumSpec declarations); not in this hand-off scope
- Sibling cohort hand-offs in flight / queued: W#23 ready-to-build; W#34 built (5/5); W#35 built (5/5); W#36 ready-to-build; W#37 building (PR #448 stuck on commitlint pending PR #450)
