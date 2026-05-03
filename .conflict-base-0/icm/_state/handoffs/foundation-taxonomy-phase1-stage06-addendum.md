# Foundation.Taxonomy Phase 1 Stage 06 — Hand-off Addendum (research-session response)

**Addendum date:** 2026-04-29
**Resolves:** sunfish-PM `cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md` beacon (PR #256)
**Original hand-off:** [`foundation-taxonomy-phase1-stage06-handoff.md`](./foundation-taxonomy-phase1-stage06-handoff.md) (PR #248, merged)
**Workstream:** #31

---

## What this addendum does

The original hand-off referenced two types that don't exist in the repo:

1. `IdentityRef` — used 11× as the type of `TaxonomyDefinition.Owner`, `TaxonomyLineage.DerivedBy`, and 9 `ITaxonomyRegistry` method parameters.
2. `IAuditRecord` — implemented by 8 record types in §"Audit emission."

Sunfish-PM correctly halted Stage 06 build at pre-build audit on these (the halt-conditions in the original hand-off enumerate both gaps). This addendum picks the resolution per option, replacing the references in the original hand-off **without restating the rest of the spec** — read the original for everything else; read this for the two type substitutions + one wording fix.

---

## Resolution 1 — `IdentityRef` becomes `ActorId` (with new `ActorId.Sunfish` sentinel)

**Decision:** Option A from the COB analysis. Reuse the existing `Sunfish.Foundation.Assets.Common.ActorId` primitive; add a `Sunfish` sentinel alongside the existing `System` sentinel.

**Rationale:**
- `ActorId` is already a `readonly record struct ActorId(string Value)` with `static ActorId System { get; } = new("system");` at `packages/foundation/Assets/Common/ActorId.cs:20`. Adding `static ActorId Sunfish { get; } = new("sunfish");` is a 1-line additive change with no breakage.
- Phase 1 is the *substrate* (registry contract + InMemory impl + audit emission + seed). Civilian/Enterprise governance enforcement isn't yet load-bearing on a typed regime distinction; a string-equality check (`owner == ActorId.Sunfish`) suffices for the Authoritative-regime guard.
- Option B (discriminated-union `IdentityRef = Sunfish | Tenant(TenantId) | Actor(ActorId)`) is the right long-term shape *if* tenant-scoped owners need to carry `TenantId`. That's a Phase 2 question; introducing a new foundation-tier primitive in a Stage 06 build violates the "introduce typed surfaces when forced" pattern. Defer to a Phase 2 ADR amendment if/when forced.
- Option C (raw string for Phase 1) loses type safety the kernel already provides via `ActorId`. No reason to take that hit when Option A is mechanically tiny.

**Mechanical changes (vs. the original hand-off):**

| Original | Replace with |
|---|---|
| `TaxonomyDefinition.Owner : IdentityRef` | `TaxonomyDefinition.Owner : ActorId` |
| `TaxonomyLineage.DerivedBy : IdentityRef` | `TaxonomyLineage.DerivedBy : ActorId` |
| `ITaxonomyRegistry.Create(..., IdentityRef owner, ...)` | `ITaxonomyRegistry.Create(..., ActorId owner, ...)` |
| `ITaxonomyRegistry.PublishVersion(..., IdentityRef publishedBy)` | `ITaxonomyRegistry.PublishVersion(..., ActorId publishedBy)` |
| `ITaxonomyRegistry.RetireVersion(..., IdentityRef retiredBy)` | `ITaxonomyRegistry.RetireVersion(..., ActorId retiredBy)` |
| `ITaxonomyRegistry.AddNode(..., IdentityRef addedBy)` | `ITaxonomyRegistry.AddNode(..., ActorId addedBy)` |
| `ITaxonomyRegistry.ReviseDisplay(..., IdentityRef revisedBy)` | `ITaxonomyRegistry.ReviseDisplay(..., ActorId revisedBy)` |
| `ITaxonomyRegistry.TombstoneNode(..., IdentityRef tombstonedBy)` | `ITaxonomyRegistry.TombstoneNode(..., ActorId tombstonedBy)` |
| `ITaxonomyRegistry.Clone(..., IdentityRef clonedBy)` | `ITaxonomyRegistry.Clone(..., ActorId clonedBy)` |
| `ITaxonomyRegistry.Extend(..., IdentityRef extendedBy)` | `ITaxonomyRegistry.Extend(..., ActorId extendedBy)` |
| `ITaxonomyRegistry.Alter(..., IdentityRef alteredBy)` | `ITaxonomyRegistry.Alter(..., ActorId alteredBy)` |
| `IdentityRef.Sunfish` (sentinel reference) | `ActorId.Sunfish` |
| Authoritative-regime guard | `if (owner != ActorId.Sunfish) throw new TaxonomyGovernanceException(...)` |

**New checklist item (insert into Phase 1):**

- [ ] `packages/foundation/Assets/Common/ActorId.cs`: add `public static ActorId Sunfish { get; } = new("sunfish");` next to the existing `System` sentinel. Update XML doc on the type to mention both sentinels and their semantics. Add a unit test asserting `ActorId.Sunfish.Value == "sunfish"` and that `ActorId.Sunfish != ActorId.System`.

This is a 3-line code change + 2 test lines + 1 XML doc line. Lands as the first commit of the Phase 1 build.

---

## Resolution 2 — `IAuditRecord` dropped; emit `AuditRecord` directly via 8 new `AuditEventType` constants

**Decision:** Option A from the COB analysis. Drop the `IAuditRecord` marker interface from the spec; emit `AuditRecord` directly per the existing kernel-audit pattern.

**Rationale:**
- `AuditRecord` is `sealed` at `packages/kernel-audit/AuditRecord.cs`; the extensibility point is `AuditEventType` (a `readonly record struct AuditEventType(string Value)` with static readonly fields, see `packages/kernel-audit/AuditEventType.cs`).
- Per ADR 0049: "domain producers construct `AuditRecord` with new `AuditEventType` discriminator + payload-body factory" — not "implement a marker interface." The original hand-off's `IAuditRecord` shape contradicts the substrate it claims to integrate with.
- `EquipmentLifecycleEvent` already follows this pattern (see `packages/blocks-property-equipment/Models/EquipmentLifecycleEvent.cs` — it explicitly defers audit emission to a Phase-N follow-up rather than fabricate `IAuditRecord` types).
- Option B (unseal `AuditRecord`, add `IAuditRecord` marker) is substantial kernel-audit churn that revisits ADR 0049's deliberate layering decision. Wrong direction.

### New `AuditEventType` constants (add to `packages/kernel-audit/AuditEventType.cs`)

Append to the existing list, under a new `===== ADR 0056 — Foundation.Taxonomy =====` divider:

```csharp
// ===== ADR 0056 — Foundation.Taxonomy substrate =====

/// <summary>A new taxonomy definition was created (Authoritative or Civilian regime).</summary>
public static readonly AuditEventType TaxonomyDefinitionCreated = new("TaxonomyDefinitionCreated");

/// <summary>A taxonomy version transitioned Draft → Published.</summary>
public static readonly AuditEventType TaxonomyVersionPublished = new("TaxonomyVersionPublished");

/// <summary>A taxonomy version transitioned Published → Retired.</summary>
public static readonly AuditEventType TaxonomyVersionRetired = new("TaxonomyVersionRetired");

/// <summary>A node was added to a draft taxonomy version.</summary>
public static readonly AuditEventType TaxonomyNodeAdded = new("TaxonomyNodeAdded");

/// <summary>A node's display label / description was revised in a draft version.</summary>
public static readonly AuditEventType TaxonomyNodeDisplayRevised = new("TaxonomyNodeDisplayRevised");

/// <summary>A node was tombstoned (soft-deleted) in a draft version.</summary>
public static readonly AuditEventType TaxonomyNodeTombstoned = new("TaxonomyNodeTombstoned");

/// <summary>A taxonomy definition was cloned (Civilian-regime derivation from Authoritative).</summary>
public static readonly AuditEventType TaxonomyDefinitionCloned = new("TaxonomyDefinitionCloned");

/// <summary>A taxonomy definition was extended (subset/superset/disjoint per ADR 0056 governance).</summary>
public static readonly AuditEventType TaxonomyDefinitionExtended = new("TaxonomyDefinitionExtended");

/// <summary>A taxonomy definition was altered post-publish (Authoritative-only; emits compliance trail).</summary>
public static readonly AuditEventType TaxonomyDefinitionAltered = new("TaxonomyDefinitionAltered");
```

(Note: 9 event types, not 8 — splitting `TaxonomyVersionPublished` and `TaxonomyVersionRetired` because the hand-off's original 8 implicitly conflated `PublishVersion` and `RetireVersion` audit shapes. If you prefer the 8-count from the original spec, drop `TaxonomyVersionRetired` and reuse `TaxonomyVersionPublished` for both transitions with a `body["transition"]` discriminator. Recommend the 9-version above — same number of event types as the audit constants exposed for ADR 0046 plus payments + capabilities, matches the granularity of `KeyRecoveryInitiated`/`Attested`/`Disputed`/`Completed`.)

### Replace 8 `IAuditRecord`-implementing record types with payload-body factory methods

Drop the type declarations entirely. In `packages/foundation-taxonomy/Audit/TaxonomyAuditPayloadFactory.cs` (new file), add 9 static factory methods that build `AuditPayload.Body` dictionaries:

```csharp
namespace Sunfish.Foundation.Taxonomy.Audit;

internal static class TaxonomyAuditPayloadFactory
{
    public static AuditPayload Created(TaxonomyDefinition def, ActorId owner) =>
        new(new Dictionary<string, object?>
        {
            ["definition_id"] = def.Id.Value,
            ["definition_key"] = def.Key,
            ["regime"] = def.Regime.ToString(),  // "Authoritative" | "Civilian" | "Enterprise"
            ["owner"] = owner.Value,
        });

    public static AuditPayload VersionPublished(TaxonomyVersionId vId, ActorId publishedBy) =>
        new(new Dictionary<string, object?>
        {
            ["version_id"] = vId.Value,
            ["published_by"] = publishedBy.Value,
        });

    // ... 7 more factory methods, one per AuditEventType above
}
```

Each method returns an `AuditPayload`; the caller (`InMemoryTaxonomyRegistry`) wraps it in `SignedOperation.Sign(...)` and constructs the `AuditRecord`:

```csharp
await _auditTrail.AppendAsync(new AuditRecord(
    AuditId: Guid.NewGuid(),
    TenantId: tenantId,
    EventType: AuditEventType.TaxonomyDefinitionCreated,
    OccurredAt: DateTimeOffset.UtcNow,
    Payload: SignedOperation.Sign(TaxonomyAuditPayloadFactory.Created(def, owner)),
    AttestingSignatures: ImmutableList<AttestingSignature>.Empty,
    FormatVersion: AuditRecord.CurrentFormatVersion), ct);
```

### Tests (replace original hand-off's audit-shape assertions)

For each of the 9 event types, write a unit test that:

1. Calls the corresponding `ITaxonomyRegistry` operation
2. Captures the emitted `AuditRecord` from a test `IAuditTrail` spy
3. Asserts: `record.EventType` matches the expected constant
4. Asserts: `record.Payload.Operation.Body` (the unwrapped `AuditPayload.Body` dictionary) contains the expected keys with the expected values
5. Asserts: `record.TenantId` is the test tenant
6. Asserts: `record.FormatVersion == 0` (per current substrate v0)

That replaces "type-equality assertion on `IAuditRecord` subtype" with "discriminator + payload-body shape assertion." Same coverage; matches the substrate.

### Why 9 event types is correct (not 8)

The original hand-off had 8 `IAuditRecord` types. Splitting `Publish` from `Retire` (both currently lumped under one event type in the original) makes the audit log easier to project and matches the kernel-audit naming convention (one event per transition, not per method). If the original count of 8 must hold for ADR 0049 alignment, drop `TaxonomyVersionRetired` and reuse `TaxonomyVersionPublished` with a `body["transition"]` field of `"published"` or `"retired"` — same outcome.

---

## Resolution 3 — OQ-4 wording fix

The original hand-off's OQ-4 says:

> *"OQ-4: introduce `IdentityRef.Sunfish` sentinel constant if not present in `Foundation.Identity`."*

**Issue 1:** `Foundation.Identity` is not a package. The existing identity primitives (`ActorId`, `TenantId`, `EntityId`, `SchemaId`, `VersionId`) live in `packages/foundation/Assets/Common/`.

**Issue 2:** OQ-4 vs the hand-off's halt-condition section is internally contradictory: OQ-4 says "introduce if not present," halt condition says "research must amend Foundation.Recovery first." Both are wrong now (this addendum supersedes the OQ).

**Resolution:** OQ-4 is closed by Resolution 1 above. The Phase 1 build introduces `ActorId.Sunfish` in `packages/foundation/Assets/Common/ActorId.cs` as a 1-line additive change; no separate ADR amendment, no Foundation.Recovery change, no new package.

---

## Updated halt conditions

The original hand-off's halt conditions §3 ("Existing `Sunfish.Foundation.Recovery.IdentityRef` doesn't have a `Sunfish` sentinel") is now resolved at the substrate-tier (it was misdiagnosed; `IdentityRef` doesn't exist in Foundation.Recovery either — there's no such type). The remaining halt conditions stand.

If any of the *substituted* references trip a fresh halt (e.g., `ActorId` requires a different namespace adjustment than expected, or the audit-payload factory pattern surfaces an interaction with `SignedOperation` that wasn't anticipated), write a new `cob-question-*` beacon — same protocol.

---

## What this addendum does NOT change

- All other §"Phase 1 scope" deliverables: registry contract + 11 model types (now 11 with `IdentityRef` substitutions absorbed into existing types) + 2 service interfaces + InMemory implementations + DI extension + tests + kitchen-sink seed page + apps/docs entry.
- The 5 starter taxonomies (`Sunfish.Signature.Scopes` + 4 charter taxonomies per PR #242). Seed format unchanged.
- The Phase 1 acceptance criteria. Tests still verify governance regime enforcement, version lifecycle, lineage projection, audit emission shape (now via discriminator+body, not interface).
- The estimated scope (~5–10h sunfish-PM time).

---

## How sunfish-PM should pick this up

1. **Resume Phase 1 build** with the type substitutions above.
2. **First commit:** `feat(foundation-assets-common): add ActorId.Sunfish sentinel` — 1-line addition + test + XML doc; lands instantly.
3. **Second commit + PR:** `feat(foundation-taxonomy): Phase 1 substrate scaffold` — the bulk of the hand-off, with `ActorId` substituted everywhere `IdentityRef` appeared and audit emission via factory + `AuditEventType`.
4. **`git mv` the beacon file** `icm/_state/research-inbox/cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md` to `_archive/` in the second PR (or this addendum's PR — whichever lands first; the addendum-PR archives it).
5. **Update ledger row #31** from `ready-to-build` → `building` when the second PR opens; `built` when it merges.

---

## References

- Original hand-off: [`foundation-taxonomy-phase1-stage06-handoff.md`](./foundation-taxonomy-phase1-stage06-handoff.md)
- Beacon (this addendum resolves): [`../research-inbox/_archive/cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md`](../research-inbox/_archive/cob-question-2026-04-29T19-12Z-31-taxonomy-prerequisites.md) (post-archive path; pre-archive path is `../research-inbox/`)
- COB analysis (full): user memory `project_workstream_31_taxonomy_handoff_halts.md`
- Kernel-audit substrate: `packages/kernel-audit/AuditEventType.cs`, `packages/kernel-audit/AuditRecord.cs`
- ActorId primitive: `packages/foundation/Assets/Common/ActorId.cs`
- ADR 0056 (Foundation.Taxonomy substrate): `docs/adrs/0056-foundation-taxonomy-substrate.md`
- ADR 0049 (Audit substrate): `docs/adrs/0049-audit-trail-substrate.md`
