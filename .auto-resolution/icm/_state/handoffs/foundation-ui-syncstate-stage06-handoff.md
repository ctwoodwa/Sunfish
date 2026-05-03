# Hand-off — Foundation.UI.SyncState public enum (ADR 0036-A1)

**From:** XO (research session)
**To:** sunfish-PM session (COB)
**Created:** 2026-05-01
**Status:** `ready-to-build`
**Spec source:** [ADR 0036 amendment A1](../../docs/adrs/0036-syncstate-multimodal-encoding-contract.md) (landed via PR #427)
**Approval:** ADR 0036-A1 Accepted on origin/main; pre-merge council waived per Decision Discipline Rule 3 (mechanical type-exposure precedent)
**Estimated cost:** ~2–4h sunfish-PM (foundation-tier enum exposure + canonical-identifier round-trip + ~6 tests + apps/docs page)
**Pipeline:** `sunfish-api-change`
**Audit before build:** `git grep -n "namespace Sunfish.Foundation.UI" packages/` to confirm no existing `Sunfish.Foundation.UI.SyncState` collision (audit not yet run; COB confirms before commit)

---

## Context

Smallest Stage 06 hand-off in the W#33-derived sibling-amendment chain. ADR 0036-A1 ratifies a public `Sunfish.Foundation.UI.SyncState` enum — exposing the existing 5-state encoding contract (canonical identifiers `healthy / stale / offline / conflict / quarantine` per ADR 0036 §"Decision") as a typed C# enum that downstream substrate ADRs (specifically ADR 0063's `SyncStateSpec.AcceptableStates`) can consume in type signatures.

**Backward-compat preserved.** Existing string-form consumers continue to work; the enum is additive.

---

## Files to create

### Package decision: extend `foundation-localfirst` OR new `foundation-ui-syncstate`

ADR 0036-A1 §A1.4 says: *"Live in `packages/foundation-localfirst/` (or a new sub-namespace if foundation-localfirst is not the right home — Stage 06 picks)."*

**Recommended: new package `packages/foundation-ui-syncstate/`** — the SyncState enum is a UI-tier concept (ARIA roles + visibility tables per ADR 0036). foundation-localfirst is a sync-substrate-tier concept (CRDT engine integration). Separating them keeps tier boundaries clean. **COB makes the call** if real-world testing exposes a different shape; document the decision in the PR description.

```
packages/foundation-ui-syncstate/
├── Sunfish.Foundation.UI.SyncState.csproj
├── README.md
├── SyncState.cs                              (the 5-value enum per A1.1)
├── SyncStateExtensions.cs                    (canonical-identifier round-trip helpers per A1.2)
└── tests/
    └── Sunfish.Foundation.UI.SyncState.Tests.csproj
        ├── SyncStateRoundTripTests.cs        (5 tests — one per enum value)
        └── SyncStateDictionaryKeyTests.cs    (ReadAsPropertyName / WriteAsPropertyName per W#34 P1 PluginId precedent)
```

### Type definition (post-A1 surface; implement exactly per ADR 0036-A1)

```csharp
namespace Sunfish.Foundation.UI;

/// <summary>
/// Canonical sync-state enum per ADR 0036's encoding contract.
/// 5-value set; PascalCase form of canonical lowercase identifiers (healthy / stale / offline / conflict / quarantine).
/// Round-trips via Sunfish.Foundation.Crypto.CanonicalJson.Serialize as lowercase strings (per A1.2).
/// </summary>
public enum SyncState
{
    Healthy,    // canonical identifier "healthy"
    Stale,      // canonical identifier "stale"
    Offline,    // canonical identifier "offline"
    Conflict,   // canonical identifier "conflict"
    Quarantine  // canonical identifier "quarantine"
}
```

`JsonStringEnumConverter` configuration must produce `"healthy"` / `"stale"` / `"offline"` / `"conflict"` / `"quarantine"` (lowercase) — NOT PascalCase. Use `JsonNamingPolicy.CamelCase` (single-word identifiers flat-case identically; produces lowercase).

---

## Phase breakdown (~1 PR)

This hand-off ships as a **single PR** (no multi-phase split given ~2-4h scope). The PR contents:

- Package scaffold per the file structure above
- `SyncState` enum with 5 values
- `SyncStateExtensions` with `ToCanonicalIdentifier()` + `TryFromCanonicalIdentifier(string)` round-trip helpers
- `JsonStringEnumConverter` configuration confirmed working with CanonicalJson.Serialize
- 6 tests:
  - 5 round-trip tests (one per enum value)
  - 1 dictionary-key context test (ReadAsPropertyName / WriteAsPropertyName per W#34 P1 PluginId precedent)
- `apps/docs/foundation-ui-syncstate/overview.md` walkthrough page (cite ADR 0036-A1 explicitly; brief — this is a small surface)
- README.md per the standard package-README pattern
- Active-workstreams.md row 37 added (`ready-to-build` → `built` in same PR; the work is small enough to land atomic)

**Acceptance:**
- [ ] All 5 enum values defined exactly (`Healthy`, `Stale`, `Offline`, `Conflict`, `Quarantine`)
- [ ] Round-trips via `CanonicalJson.Serialize` to lowercase canonical identifier strings (5 tests pass)
- [ ] Dictionary-key context round-trip works (ReadAsPropertyName / WriteAsPropertyName per W#34 P1 PluginId/AdapterId pattern; needed for downstream ADR 0063 SyncStateSpec consumers)
- [ ] No namespace collision with existing `Sunfish.Foundation.UI.*` types (verify before commit per COB pre-build checklist)
- [ ] apps/docs page renders + cites ADR 0036-A1
- [ ] PR description names the package-decision rationale (foundation-ui-syncstate vs foundation-localfirst)
- [ ] active-workstreams.md row 37 lands as `built`

---

## Halt-conditions (cob-question if any of these surface)

1. **Namespace collision.** If `Sunfish.Foundation.UI.SyncState` already exists in another package (audit `git grep -n "enum SyncState" packages/` before commit), file `cob-question-*` beacon.

2. **CanonicalJson.Serialize round-trip mismatch.** Expected: enum serializes as lowercase string. If the configuration produces PascalCase or numeric (default `JsonStringEnumConverter` with no naming policy), file `cob-question-*` beacon — A1.2's verification didn't catch a config nuance.

3. **Dictionary-key context broken.** W#34 P1 shipped `JsonConverter.ReadAsPropertyName` / `WriteAsPropertyName` for dictionary-key contexts. If the SyncState converter doesn't support dictionary-key contexts (downstream ADR 0063's `SyncStateSpec.AcceptableStates: IReadOnlySet<SyncState>?` may need it), file `cob-question-*` beacon.

4. **Package-decision regret.** If COB starts in `foundation-ui-syncstate` and discovers it should have been in `foundation-localfirst` (or vice versa), file `cob-question-*` beacon BEFORE shipping; cheap to move pre-merge.

---

## Cited-symbol verification (per cohort discipline)

**Existing on origin/main (verified 2026-05-01):**

- ADR 0036-A1 (PR #427 merged) — substrate spec source
- ADR 0036 (this is the parent ADR) — Accepted; 5-state encoding contract canonical
- `Sunfish.Foundation.Crypto.CanonicalJson.Serialize` (per ADR 0028-A4 retraction; verified existing) ✓
- `JsonStringEnumConverter` (System.Text.Json; Microsoft framework type)
- ADR 0028-A7.8 — camelCase canonical encoding precedent
- W#34 P1 PluginId/AdapterId JsonConverter.ReadAsPropertyName/WriteAsPropertyName precedent (PR #417 merged)

**Introduced by this hand-off** (ship in single PR):

- New package: `Sunfish.Foundation.UI.SyncState` (recommended) or extension to `foundation-localfirst`
- `Sunfish.Foundation.UI.SyncState` enum (5-value)
- `SyncStateExtensions` round-trip helpers
- 6 unit tests
- apps/docs entry

**Cohort lesson reminder (per ADR 0028-A10 + ADR 0063-A1.15):** §A0 self-audit pattern is necessary but NOT sufficient. COB should structurally verify each Sunfish.* symbol exists (read actual cited file's schema; don't grep alone).

---

## Cohort discipline

This hand-off is **not** a substrate ADR amendment; it's a Stage 06 hand-off implementing post-A1-fixed surface. Pre-merge council on this hand-off is NOT required.

- COB's standard pre-build checklist applies
- W#34 + W#35 cohort lessons incorporated: JsonStringEnumConverter for the enum; ReadAsPropertyName/WriteAsPropertyName for dictionary-key contexts; small package layout convention

---

## Beacon protocol

If COB hits a halt-condition or has a design question:

- File `cob-question-2026-05-XXTHH-MMZ-w37-{slug}.md` in `icm/_state/research-inbox/`
- Halt the workstream + add a note in active-workstreams.md row 37
- ScheduleWakeup 1800s

If COB completes + drops to fallback:

- Drop `cob-idle-2026-05-XXTHH-MMZ-{slug}.md` to research-inbox
- Continue with rung-1 dependabot + rung-2 build-hygiene per CLAUDE.md fallback

---

## Cross-references

- Spec source: ADR 0036-A1 (PR #427 merged 2026-05-01)
- Companion intake: PR #414 (merged); intake stub at `icm/00_intake/output/2026-04-30_sync-state-public-enum-intake.md`
- Downstream consumer: ADR 0063 `SyncStateSpec.AcceptableStates` (post-A1.2 corrected to use ADR-0036-canonical state names)
- Sibling Stage 06 hand-offs in flight / queued: W#23 iOS Field-Capture (`ready-to-build`); W#35 Foundation.Migration (now `built` 5/5 phases per PRs #439-#446); W#36 Bridge subscription emitter (`ready-to-build` per PR #443)
