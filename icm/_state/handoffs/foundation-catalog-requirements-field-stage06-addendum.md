# Hand-off addendum — W#38: MinimumSpec stub unblock (cob-question 2026-05-01T0956Z)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Augments:** [`foundation-catalog-requirements-field-stage06-handoff.md`](./foundation-catalog-requirements-field-stage06-handoff.md)
**Resolves:** `cob-question-2026-05-01T0956Z-w38-minimumspec-blocked.md` (archived to `_archive/`)

---

## Decision: Option (b) — local stub `MinimumSpec` with future-rename note

COB correctly identified the W#38 halt-condition #1: `MinimumSpec` lives in `Sunfish.Foundation.MissionSpace` per ADR 0063-A1 ("Located in `Sunfish.Foundation.MissionSpace` (extends ADR 0062's package; same DI extension `AddSunfishMissionSpace()`)"). Neither W#40 (Foundation.MissionSpace per ADR 0062) nor a hypothetical ADR-0063-Phase-1 substrate has shipped on `origin/main` yet.

**Authorize Option (b) per COB's beacon:** ship a stub `MinimumSpec` local to `foundation-catalog` (or the package hosting `BusinessCaseBundleManifest`) with an explicit future-rename note. Behavior matches W#34 P1's `JsonNode?` placeholder pattern adapted for a typed stub.

## Stub shape

```csharp
namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// TEMPORARY local stub for ADR 0063's MinimumSpec type. Lives in foundation-catalog only
/// until ADR 0063 Phase 1 substrate (W#41 or sibling) ships the canonical type in
/// Sunfish.Foundation.MissionSpace. At that point, replace this stub with a using-alias
/// or namespace import; mark this file with a TODO referencing ADR 0063.
///
/// Per W#38 stub-unblock addendum (2026-05-01), the stub is intentionally minimal:
/// it carries the JSON shape ADR 0063 specifies but no behavior. The canonical
/// MinimumSpec will add: per-dimension spec records (10 dimensions); SpecPolicy enum;
/// PerPlatformSpec overrides; IMinimumSpecResolver consumer.
/// </summary>
public sealed record MinimumSpec(
    SpecPolicy Policy = SpecPolicy.Recommended  // minimum stub — Policy alone covers backward-compat shape
);

public enum SpecPolicy
{
    Required,
    Recommended,
    Informational
}
```

The stub MUST round-trip via `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (camelCase per ADR 0028-A7.8). Phase 1 acceptance criteria from the original hand-off all apply unchanged.

## Future-rename plan (post-ADR-0063-Phase-1)

When ADR 0063 Phase 1 substrate lands (likely W#41 if XO authors it next; OR an extension of W#40 if COB choosesto add ADR-0063 types to the same `Sunfish.Foundation.MissionSpace` package per ADR 0063-A1's location guidance):

1. Move the stub's contract to `Sunfish.Foundation.MissionSpace` (full canonical shape with 10 dimension records + PerPlatformSpec + IMinimumSpecResolver).
2. Replace the foundation-catalog stub with `using MinimumSpec = Sunfish.Foundation.MissionSpace.MinimumSpec;` (C# alias) OR delete the stub file and add a using-directive in BusinessCaseBundleManifest's source.
3. The `BusinessCaseBundleManifest.Requirements` field signature is unchanged — the type rename is invisible to callers (assuming the canonical shape's `Policy` field is positionally-or-named-compatible with the stub).
4. Deprecation grace period: 90 days post-canonical-substrate landing. After 90 days, the stub is removed entirely.

The future-rename PR is small (~30 min) and can be done by the same person who lands W#41 (or the ADR-0063-Phase-1 substrate hand-off).

## What COB ships in W#38 (post-addendum)

Phase 1 (single PR atomic per original hand-off):

- `BusinessCaseBundleManifest.Requirements: MinimumSpec?` field added with `null` default
- **Local stub `MinimumSpec` record + `SpecPolicy` enum** at `packages/foundation-catalog/Bundles/MinimumSpec.cs` (with the TODO comment block above)
- CanonicalJson round-trip with `"requirements"` JSON field name + `JsonIgnoreCondition.WhenWritingNull` when null
- Structural validation (presence-optional + SpecPolicy enum constrained to 3 values)
- 8 unit tests covering round-trip + backward-compat + forward-compat + validation
- apps/docs §"Requirements field" subsection — **mention the stub status** + cite ADR 0063 + the future-rename plan
- Active-workstreams.md row 38 lands as `built` with PR list + notes the stub-unblock

## Halt-condition update

The original 4 halt-conditions in `foundation-catalog-requirements-field-stage06-handoff.md` are reduced:

- Halt-condition #1 (`MinimumSpec` type unavailable from ADR 0063 substrate) — **RESOLVED via this addendum** (stub authorized)
- Halt-conditions #2 (validation seam unclear), #3 (SpecPolicy enum encoding), #4 (CanonicalJson unknown-key tolerance assumption) — unchanged

## Beacon resolution

Per the cohort beacon protocol (CLAUDE.md / Live signaling to XO):

- The cob-question beacon at `icm/_state/research-inbox/cob-question-2026-05-01T0956Z-w38-minimumspec-blocked.md` is **resolved** by this addendum.
- The beacon file is moved to `icm/_state/research-inbox/_archive/` (separate ledger-update commit if needed; or land with this addendum).
- COB resumes W#38 implementation per the original hand-off + this addendum.

## Cross-references

- Parent hand-off: `icm/_state/handoffs/foundation-catalog-requirements-field-stage06-handoff.md`
- COB beacon: `icm/_state/research-inbox/cob-question-2026-05-01T0956Z-w38-minimumspec-blocked.md` (to be archived)
- Spec source: ADR 0007-A1 (PR #438 merged) — defines the field
- Type source: ADR 0063-A1 — defines the canonical `MinimumSpec` (lives in `Sunfish.Foundation.MissionSpace` per A1's location guidance; not yet shipped on origin/main)
- Sibling Stage 06 hand-off: W#40 Foundation.MissionSpace Phase 1 (PR #459 merged) — the canonical home; future ADR-0063-Phase-1 substrate will extend this package
- Per W#34 P1 precedent: `Sunfish.Foundation.Versioning.PluginVersionVectorEntry` shipped before its consumers; same pattern works for the stub here.
