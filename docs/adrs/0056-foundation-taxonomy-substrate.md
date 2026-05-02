---
id: 56
title: Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)
status: Accepted
date: 2026-04-29
tier: foundation
concern:
  - dev-experience
composes:
  - 1
  - 7
  - 28
  - 46
  - 49
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0056 — Foundation.Taxonomy Substrate (Versioned Product Model with Lineage)

**Status:** Accepted
**Date:** 2026-04-29 (Proposed) / 2026-04-29 (Accepted by CTO on technical merit; 8/8 pre-acceptance self-audit complete; council-review subagent dispatch waived per cluster decision-velocity preference)
**Resolves:** Stage 00 intake [`taxonomy-management-substrate-intake-2026-04-29.md`](../../icm/00_intake/output/taxonomy-management-substrate-intake-2026-04-29.md) (PR #234) — formalizes CEO's spec for taxonomy management as a substrate-class architectural decision. Cross-referenced + composed by ADR 0055 (Dynamic Forms Substrate; PR #236) and ADR 0054 (Electronic Signatures; SignatureScope UPF Pattern E approved by CO 2026-04-29; PR #237).

---

## Context

Sunfish's primitive type catalog (PR #231 OSS primitives research) proposed `Coding` + `CodeableConcept` as compound primitives for taxonomy entries — triple of (system, code, display). The shape is correct for **referencing** taxonomy entries from data records, but it intentionally punts on the harder questions: who owns taxonomies, how they version, how they evolve, who can fork them, how consumers stay aligned over time, how authoritative sources publish vs marketplace participants extend.

CEO surfaced these as the actually-load-bearing concerns 2026-04-29 with a detailed spec (preserved verbatim in the intake). Five examples that crystallize the need:

1. **Equipment class taxonomy** for property management — water heater types vary across jurisdictions; tenants may add custom classes (solar panels, smart thermostats, EV chargers); cross-tenant analytics need stable identity.
2. **Vendor specialty taxonomy** — plumbing / electrical / HVAC / landscaping / pest control / etc.; each tenant accumulates idiosyncratic specialties (e.g., "pool maintenance" only matters in some markets).
3. **Inspection deficiency categories** — different inspection programs (annual vs move-in vs post-repair) need different deficiency taxonomies; jurisdictions may mandate specific categories.
4. **Signature scope taxonomy** — ADR 0054's SignatureScope (Pattern E adopted 2026-04-29) requires this substrate; lease-execution vs notary-jurat vs witness-attestation are taxonomy nodes, not enum values.
5. **Compliance / regulatory codes** — FCRA classifications, NACHA codes, state-specific landlord-tenant law codes, IRS Schedule E categories — all are external authoritative taxonomies that Sunfish tenants reference.

Without this substrate:
- Equipment class becomes a hardcoded enum baked into code; can't evolve per tenant
- Vendor specialty taxonomies are a free-for-all of inconsistent strings
- Cross-tenant analytics impossible because nobody can map "this water heater class" in tenant A to "this water heater class" in tenant B
- Marketplace / community-contributed taxonomies have no place to live
- Authoritative compliance taxonomies (FCRA, NACHA, IRS, etc.) can't be referenced with stable version-pinning

The substrate is foundation-tier. It enables `Coding` (the lightweight reference primitive) to point at meaningful, governed, versioned taxonomy nodes. ADRs 0054 (Signatures) and 0055 (Dynamic Forms) both depend on it.

---

## Decision drivers

- **CEO directive 2026-04-29:** taxonomies as versioned products with lineage; authoritative vs marketplace roles; clone/extend/alter derivation operations; per-vertical governance; CRDT-backed editing; marketplace distribution
- **ADR 0054 SignatureScope (PR #237; Pattern E approved 2026-04-29):** signature scope is `IReadOnlyList<TaxonomyClassification>` — directly depends on this substrate
- **ADR 0055 Dynamic Forms Substrate (PR #236):** `Coding` + `CodeableConcept` primitives reference this substrate
- **Cluster impact:** equipment classes, vendor specialties, inspection deficiency categories, signature scopes — all become taxonomy-backed
- **Cross-vertical:** future verticals (highway management, healthcare, government) ship as taxonomy bundles without forking the substrate
- **Compliance posture:** authoritative taxonomies (FCRA, NACHA, IRS Schedule E, state landlord-tenant codes) need stable version-pinned references for audit trail
- **Local-first sync:** taxonomy definitions sync as data via ADR 0028 CRDT substrate
- **Multi-platform:** taxonomy resolution runs in browser (Photino+Blazor / web), .NET (Anchor desktop), future iOS — eliminates code-as-taxonomy patterns
- **Marketplace economics:** Sunfish's bundle catalog (ADR 0007) is the natural host; taxonomy products distribute as bundles
- **Provider-neutrality:** ADR 0013 enforcement gate active; substrate must not couple to vendor-specific taxonomy systems

---

## Considered options

### Option A — Full taxonomy substrate (CTO recommendation, matches CEO spec)

Build a foundation-tier substrate that:
- Models taxonomies as first-class versioned artifacts with lineage
- Supports authoritative + marketplace ownership
- Provides clone / extend / alter derivation operations
- Configures per-vertical governance regimes (corporate/government strict; civilian flexible)
- Uses CRDT-backed storage for tenant-owned editing
- Distributes via Sunfish bundle catalog

- **Pro:** Matches CEO spec verbatim
- **Pro:** Unblocks ADR 0054 SignatureScope amendment + ADR 0055 Coding/CodeableConcept primitive resolution
- **Pro:** Composable with existing ADRs (0001 Schema Registry, 0007 Bundle Manifest, 0028 CRDT, 0046 Recovery, 0049 Audit)
- **Con:** ~3-5 weeks COB substrate work
- **Con:** Largest single foundation-tier commitment after ADR 0055

### Option B — Lightweight Coding + CodeableConcept primitives only (no substrate)

Ship just the primitive types (system + code + display) without a registry / governance / lineage substrate. Each tenant manages taxonomy strings ad hoc.

- **Pro:** Fastest (~2-3 days for primitive types)
- **Con:** Direct conflict with CEO directive (CEO explicitly requested versioning + lineage + governance)
- **Con:** Inconsistent strings across tenants; cross-tenant analytics impossible
- **Con:** No version-pinning means historical references decay

**Verdict:** Rejected. CEO directive precludes.

### Option C — Adopt external standards directly (FHIR ValueSet, ICD, NAICS)

Don't build a Sunfish substrate; use FHIR ValueSet + ICD-10 + NAICS as the canonical taxonomy systems; reference them directly.

- **Pro:** Reuses standards
- **Con:** Property management isn't healthcare; FHIR ValueSet is overhead
- **Con:** Doesn't address tenant-defined taxonomies (the property-management common case)
- **Con:** Bundling external standards into Sunfish marketplace requires substrate anyway

**Verdict:** Rejected. External standards are *integration targets* once the substrate exists; not a substitute.

### Option D — Defer to v2

Build the substrate later; for v1 hardcode equipment classes, vendor specialties, signature scopes as enums.

- **Pro:** Faster v1
- **Con:** Direct conflict with CEO directive
- **Con:** ADR 0054 SignatureScope amendment (Pattern E approved by CO 2026-04-29) directly requires the substrate
- **Con:** ADR 0055's `Coding` + `CodeableConcept` primitives lose meaning without the substrate
- **Con:** Hardcoded enums require code changes per tenant per scope addition

**Verdict:** Rejected. v1 dependencies (ADRs 0054 + 0055) require this substrate.

---

## Decision

**Adopt Option A.** Build `Sunfish.Foundation.Taxonomy` as a foundation-tier substrate composing existing primitives.

### Substrate components

```
┌──────────────────────────────────────────────────────────────────┐
│              Sunfish.Foundation.Taxonomy                         │
│                                                                  │
│  ┌────────────────────┐  ┌────────────────────────────────────┐ │
│  │ Taxonomy Registry  │  │ Derivation Engine                  │ │
│  │ (versioned         │  │ (Clone / Extend / Alter operations │ │
│  │  artifacts;        │  │  with lineage tracking)            │ │
│  │  authoritative +   │  │                                    │ │
│  │  marketplace)      │  │                                    │ │
│  └────────┬───────────┘  └──────────┬─────────────────────────┘ │
│           │                         │                            │
│  ┌────────▼─────────────────────────▼─────────────────────────┐ │
│  │ Storage Layer                                              │ │
│  │ - CRDT-backed (ADR 0028) for tenant-owned taxonomies       │ │
│  │ - Snapshot-published for authoritative taxonomies          │ │
│  │ - Foundation.Recovery (PR #223) for authoritative signing  │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Governance Layer                                         │   │
│  │ - Per-tenant regime configuration                        │   │
│  │ - Marketplace integration (ADR 0007 bundle catalog)      │   │
│  │ - Capability-based authoring (ADR 0032 macaroons)        │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │ Audit Layer (ADR 0049)                                   │   │
│  │ - 8 typed event types: TaxonomyCreated, Published,       │   │
│  │   Deprecated, NodeAdded, NodeUpdated, NodeDeprecated,    │   │
│  │   Cloned, Extended                                       │   │
│  └──────────────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────────────┘
```

### Initial contract surface

```csharp
namespace Sunfish.Foundation.Taxonomy;

public sealed record TaxonomyDefinition
{
    public required TaxonomyId Id { get; init; }                     // URI; e.g., "taxonomy://sunfish.dev/equipment-classes"
    public required SemanticVersion Version { get; init; }
    public required TaxonomyStatus Status { get; init; }             // Draft | Published | Deprecated | Withdrawn
    public required IdentityRef Owner { get; init; }                 // Tenant or external authoritative source
    public required InternationalizedText Title { get; init; }
    public InternationalizedText? Description { get; init; }
    public TaxonomyLineage? Lineage { get; init; }                   // optional; set if derived from another taxonomy
    public required IReadOnlyList<TaxonomyNode> Nodes { get; init; } // tree (or DAG) of nodes
    public required TenantId Tenant { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? PublishedAt { get; init; }                // when Status = Published
    public IReadOnlyDictionary<string, string>? Metadata { get; init; } // license, source URL, etc.
}

public readonly record struct TaxonomyId(string Value);              // URI form
public readonly record struct SemanticVersion(int Major, int Minor, int Patch);
public enum TaxonomyStatus { Draft, Published, Deprecated, Withdrawn }

public sealed record TaxonomyLineage
{
    public required TaxonomyId BaseTaxonomyId { get; init; }
    public required SemanticVersion BaseVersion { get; init; }
    public required DerivationKind Kind { get; init; }               // Clone | Extend | Alter
    public required DateTimeOffset DerivedAt { get; init; }
}

public enum DerivationKind { Clone, Extend, Alter }

public sealed record TaxonomyNode
{
    public required string NodeId { get; init; }                     // stable; persists across version bumps within a taxonomy
    public required string Code { get; init; }                       // canonical machine identifier (e.g., "WaterHeater")
    public required InternationalizedText DisplayText { get; init; }
    public string? ParentNodeId { get; init; }                       // tree shape
    public IReadOnlyList<string>? AlsoCategorizedUnder { get; init; } // optional DAG support
    public TaxonomyNodeRef? InheritedFrom { get; init; }             // for Extend operations
    public TaxonomyNodeRef? MappedFrom { get; init; }                // for Alter operations (move/split/merge/rename)
    public TaxonomyNodeStatus Status { get; init; }                  // Active | Deprecated | Withdrawn
    public IReadOnlyDictionary<string, string>? Attributes { get; init; }
}

public enum TaxonomyNodeStatus { Active, Deprecated, Withdrawn }

public sealed record TaxonomyNodeRef
{
    public required TaxonomyId TaxonomyId { get; init; }
    public required SemanticVersion TaxonomyVersion { get; init; }
    public required string NodeId { get; init; }
}

// The classification reference — what records use to point at a taxonomy node
public sealed record TaxonomyClassification
{
    public required TaxonomyId TaxonomyId { get; init; }
    public required SemanticVersion TaxonomyVersion { get; init; }
    public required string NodeId { get; init; }
    public string? AdditionalContext { get; init; }                  // optional free-form context
}

public interface ITaxonomyRegistry
{
    Task<TaxonomyDefinition?> GetAsync(TaxonomyId id, SemanticVersion? version, CancellationToken ct);
    Task<TaxonomyDefinition> RegisterAsync(TaxonomyDefinition definition, CancellationToken ct);
    Task<TaxonomyDefinition> PublishAsync(TaxonomyId id, SemanticVersion version, CancellationToken ct);
    Task<TaxonomyDefinition> DeprecateAsync(TaxonomyId id, SemanticVersion version, CancellationToken ct);
    Task<IReadOnlyList<TaxonomyDefinition>> ListByTenantAsync(TenantId tenant, CancellationToken ct);
    Task<IReadOnlyList<TaxonomyDefinition>> ListMarketplaceAsync(CancellationToken ct);
    Task<TaxonomyDefinition> CloneAsync(TaxonomyId source, SemanticVersion sourceVersion, IdentityRef newOwner, CancellationToken ct);
    Task<TaxonomyDefinition> ExtendAsync(TaxonomyId source, SemanticVersion sourceVersion, IdentityRef newOwner, CancellationToken ct);
}

public interface ITaxonomyResolver
{
    Task<TaxonomyNode?> ResolveAsync(TaxonomyClassification classification, CancellationToken ct);
    Task<IReadOnlyList<TaxonomyNode>> WalkLineageAsync(TaxonomyId id, SemanticVersion version, CancellationToken ct);
    Task<TaxonomyNode?> FindEquivalentInVersionAsync(TaxonomyClassification classification, SemanticVersion targetVersion, CancellationToken ct);
}
```

### Versioning + lineage semantics

**Semantic versioning rules:**
- **Major version bump:** breaking change (node removal, code rename, structural reshape)
- **Minor version bump:** additive change (new nodes, new metadata fields)
- **Patch version bump:** display-only change (translation updates, description rewrites)

**Stable references via classification:** assets store `{taxonomyId, taxonomyVersion, nodeId}` triples. Classification references are version-pinned at write time; subsequent taxonomy version bumps don't invalidate historical classifications.

**Lineage tracking:** every derived taxonomy records its `baseTaxonomyId` + `baseVersion` + `derivationKind`. Walking the lineage chain answers "where did this taxonomy come from."

**Node-level lineage:** for Extend operations, each inherited node records `inheritedFrom: TaxonomyNodeRef`. For Alter operations (move/split/merge/rename), the resulting nodes record `mappedFrom: TaxonomyNodeRef`. This enables migration of classifications when the derived taxonomy alters structure.

### Authoritative vs marketplace governance

Three regimes per tenant configuration:

| Regime | Source taxonomies | Derivation permissions | Mapping requirement |
|---|---|---|---|
| **Authoritative** | Curated authoritative + tenant-owned | Restricted (admin-approved derivations only) | Strict mappings back to base required for compliance |
| **Enterprise** | Authoritative + marketplace + tenant-owned | Free derivation; mappings encouraged | Optional mappings |
| **Civilian** | Marketplace + tenant-owned + free-form | Free derivation, free-form creation, no mapping requirement | None |

Per-tenant config: `tenant.taxonomy_governance_regime ∈ {Authoritative, Enterprise, Civilian}`. Defaults to `Civilian` for new tenants; CO/admin elevates as needed.

### Authoritative source publishing (immutable snapshots)

Authoritative owners (corporate / government / standards bodies) publish taxonomies as **immutable signed snapshots**:

- Server-side `PublishTaxonomyVersionAsync` produces a frozen artifact
- Publisher's identity signature binds the snapshot (Foundation.Recovery primitives — PR #223 — provide signing)
- Subscriber tenants pull snapshot on a defined cadence; verify signature
- Changes require new version; old versions remain accessible

### Tenant-owned CRDT-backed editing

Tenants who own derived (or free-form) taxonomies edit collaboratively via CRDT (ADR 0028 substrate):

- Authorized maintainers (via macaroon capability per ADR 0032) edit offline
- Sync via CRDT merge semantics
- Conflict resolution: last-writer-wins for node-attribute updates; structural changes require explicit lock-and-publish workflow
- Regular users see published snapshots; classify assets via stable references

### Marketplace integration

Taxonomies distribute via existing Foundation.Catalog.Bundles (ADR 0007). New bundle product type: `TaxonomyProduct`.

```csharp
public sealed record TaxonomyProductDescriptor : IBundleContent
{
    public required TaxonomyId TaxonomyId { get; init; }
    public required SemanticVersion Version { get; init; }
    public required InternationalizedText Title { get; init; }
    public required string LicenseSpdxId { get; init; }              // e.g., "Apache-2.0", "MIT", "Proprietary"
    public required IdentityRef Publisher { get; init; }
    public required Money? Price { get; init; }                      // null = free
    public required string? PreviewUri { get; init; }
}
```

Marketplace UX (browse / preview / install) defers to bundle-catalog UI work in Phase 2.x.

### Audit emission (ADR 0049)

8 typed audit record types:

| Audit record | Emitted on |
|---|---|
| `TaxonomyCreated` | New taxonomy registered |
| `TaxonomyPublished` | Status transition Draft → Published |
| `TaxonomyDeprecated` | Status → Deprecated |
| `TaxonomyWithdrawn` | Status → Withdrawn |
| `TaxonomyNodeAdded` | New node within an existing taxonomy version |
| `TaxonomyNodeUpdated` | Node attribute or display update |
| `TaxonomyNodeDeprecated` | Node Status → Deprecated |
| `TaxonomyDerived` | Clone / Extend / Alter operation completed |

### Initial starter taxonomies (shipped in v1)

Sunfish ships **5 starter taxonomies** as marketplace products:

#### 1. `taxonomy://sunfish.dev/signature-scopes/v1.0`

For ADR 0054 SignatureScope (Pattern E):

```yaml
title: "Sunfish Signature Scopes v1.0"
nodes:
  - code: "Contract"
    display: "Contract"
    children:
      - code: "Lease"
        children:
          - code: "Lease.Execution"
          - code: "Lease.Amendment"
          - code: "Lease.Renewal"
          - code: "Lease.Termination"
      - code: "VendorAgreement"
      - code: "Disclosure"
  - code: "Attestation"
    display: "Attestation"
    children:
      - code: "WorkOrderCompletion"
      - code: "Inspection"
        children:
          - code: "Inspection.MoveIn"
          - code: "Inspection.MoveOut"
          - code: "Inspection.Annual"
          - code: "Inspection.PostRepair"
      - code: "MaintenanceSignOff"
  - code: "Acknowledgement"
    children:
      - code: "Acknowledgement.Criteria"      # leasing pipeline
      - code: "Acknowledgement.Disclosure"
  - code: "Witness"
    children:
      - code: "Witness.Generic"
  - code: "Notary"
    children:
      - code: "Notary.Jurat"
  - code: "Other"
```

#### 2. `taxonomy://sunfish.dev/equipment-classes/v1.0`

For property-management Equipment classifications:

```yaml
title: "Sunfish Property Equipment Classes v1.0"
nodes:
  - code: "WaterHeater"
    children:
      - code: "WaterHeater.Tank"
      - code: "WaterHeater.Tankless"
      - code: "WaterHeater.HeatPump"
  - code: "HVAC"
    children:
      - code: "HVAC.Furnace"
      - code: "HVAC.AirConditioner"
      - code: "HVAC.HeatPump"
      - code: "HVAC.Boiler"
  - code: "Appliance"
    children:
      - code: "Appliance.Refrigerator"
      - code: "Appliance.Range"
      - code: "Appliance.Dishwasher"
      - code: "Appliance.Washer"
      - code: "Appliance.Dryer"
      - code: "Appliance.Microwave"
  - code: "Roof"
  - code: "Plumbing"
  - code: "Electrical"
  - code: "Vehicle"
  - code: "Other"
```

#### 3. `taxonomy://sunfish.dev/vendor-specialties/v1.0`

For Vendor classifications:

```yaml
title: "Sunfish Vendor Specialties v1.0"
nodes:
  - code: "Plumbing"
  - code: "Electrical"
  - code: "HVAC"
  - code: "Landscaping"
  - code: "PestControl"
  - code: "Cleaning"
  - code: "Painting"
  - code: "Roofing"
  - code: "GeneralContracting"
  - code: "Locksmith"
  - code: "Inspection"
  - code: "Legal"
  - code: "Accounting"
  - code: "Other"
```

#### 4. `taxonomy://sunfish.dev/inspection-deficiency-categories/v1.0`

For Inspection deficiency classification:

```yaml
title: "Sunfish Inspection Deficiency Categories v1.0"
nodes:
  - code: "Cosmetic"
  - code: "Functional"
    children:
      - code: "Functional.Plumbing"
      - code: "Functional.Electrical"
      - code: "Functional.HVAC"
      - code: "Functional.Appliance"
  - code: "Structural"
  - code: "Safety"
    children:
      - code: "Safety.SmokeDetector"
      - code: "Safety.CarbonMonoxide"
      - code: "Safety.FireExtinguisher"
      - code: "Safety.Egress"
  - code: "Cleanliness"
  - code: "Code-Compliance"
  - code: "Other"
```

#### 5. `taxonomy://sunfish.dev/contact-use-contexts/v1.0`

For ContactPoint UseContext (per Contact use-enum UPF Pattern G):

```yaml
title: "Sunfish Contact Use Contexts v1.0"
nodes:
  - code: "Primary"
  - code: "Mailing"
  - code: "Billing"
  - code: "Emergency"
  - code: "Marketing"
  - code: "AfterHours"
  - code: "Public"
  - code: "Compliance"
```

These 5 starter taxonomies cover ADR 0054 + 0055 + cluster v1 needs. Additional starter taxonomies (project-management, business-templates per CEO spec) ship as Phase 2.x marketplace products.

### What this ADR does NOT do

- **Does not ship the visual taxonomy editor UI.** Stage 02 work; admin authoring happens via API + bundled-product import in v1; visual editor in Phase 2.x.
- **Does not build cross-tenant taxonomy federation.** Phase 3+ if real customer demand.
- **Does not ship marketplace UX (browse/install).** Phase 2.x; v1 ships taxonomies as bundles via existing bundle-catalog API.
- **Does not implement marketplace billing.** Phase 2.x or later; v1 free-only.
- **Does not implement standards-body integration adapters** (FHIR ValueSet / NAICS / ICD adapters). Adapter pattern follows after substrate ships; specific integrations are customer-driven follow-ups.
- **Does not build AI-assisted taxonomy mapping.** Out of scope.
- **Does not build deep schema-evolution tooling beyond lineage** (e.g., automatic classification migration when a derived taxonomy Alters). Phase 2.x.

---

## Consequences

### Positive

- **Unblocks ADR 0054 Pattern E** (SignatureScope as TaxonomyClassification) — CO-approved 2026-04-29
- **Unblocks ADR 0055** Coding + CodeableConcept primitive resolution
- **Unblocks property-ops cluster** classifications (equipment classes, vendor specialties, deficiency categories, contact use-contexts)
- **Cross-vertical reuse** — future verticals (highway management, healthcare, government) ship as taxonomy bundles
- **Compliance posture** — authoritative taxonomies (FCRA, NACHA, IRS Schedule E, etc.) get stable version-pinned references
- **Tenant flexibility** — civilian regime allows free-form; corporate regime allows controlled derivation
- **Marketplace economics** — bundles distribute taxonomies; Foundation.Catalog already has the substrate
- **CRDT-backed sync** inherits from existing kernel-crdt
- **Audit trail uniform** — 8 audit record types via ADR 0049

### Negative

- **~3-5 weeks COB substrate work.** Fourth foundation-tier substrate this MVP wave (after Recovery, Schema, Forms, Rule Engine).
- **Authoring complexity.** Authoritative taxonomies need controlled-environment editing; tenant-owned taxonomies need offline-CRDT editing. Two storage modes.
- **Lineage tracking adds complexity** to Alter operations (move/split/merge/rename); migration of classifications when derived taxonomy changes structure is non-trivial.
- **Per-vertical governance configuration** adds admin surface; civilian default is right but enterprise/authoritative configurations need tooling.

### Trust impact / Security & privacy

- **Authoritative taxonomies are signed.** Foundation.Recovery primitives (PR #223) sign published snapshots; subscribers verify. Tampering detected.
- **Tenant-owned taxonomies are CRDT-merged with macaroon-authorized maintainers.** ADR 0032 capability model ensures only authorized actors edit; ADR 0049 audit emits every edit.
- **Cross-tenant isolation:** taxonomies are per-tenant by default; marketplace publication is opt-in.
- **Classification references are stable + version-pinned**, supporting historical-record audit (e.g., "this signature attests to scope X as defined by Sunfish Signature Scopes v1.0 node Y" — verifiable in 2030 even after taxonomy v3 ships).

---

## Compatibility plan

### Existing callers / consumers

No production code consumes taxonomy substrate yet. All consumers are:
- ADR 0054 (Signatures) — `TaxonomyClassification` reference for SignatureScope (CO-approved)
- ADR 0055 (Dynamic Forms) — `Coding` + `CodeableConcept` primitives reference taxonomy nodes
- Cluster modules (Equipment, Vendors, Inspections, Contacts) — `TaxonomyClassification` references for class / specialty / category

All consumers are pre-implementation; substrate ships before any consumer code.

### Affected packages

| Package | Change |
|---|---|
| `Sunfish.Foundation.Taxonomy` (new) | Primary deliverable |
| `Sunfish.Foundation.Catalog` (existing) | Modified — adds `TaxonomyProductDescriptor` + bundle-content discriminator |
| `kernel-crdt` (ADR 0028 substrate) | Consumer; tenant-owned taxonomy storage uses CRDT |
| `kernel-audit` (ADR 0049) | Modified — adds 8 typed audit record types |
| `foundation-recovery` (PR #223) | Consumer — signs authoritative-published snapshots |
| `foundation-macaroons` (ADR 0032) | Consumer — capability-bearing taxonomy authoring authorization |
| ADRs 0001 (Schema Registry), 0007 (Bundle Manifest) | Cross-references; minor amendments |
| ADR 0049 (Audit Substrate) | Addendum — 8 new event types |
| ADR 0054 (Signatures) | Consumer — SignatureScope references taxonomy |
| ADR 0055 (Dynamic Forms) | Consumer — Coding/CodeableConcept primitives reference taxonomy |
| Cluster intakes (#18 Vendors, #24 Equipment, #25 Inspections, etc.) | Cross-references; classifications use taxonomy |

### Migration

No migration of existing data — substrate is greenfield. Cluster modules in flight (Equipment first-slice, Inspections extension) will reauthor classifications as `TaxonomyClassification` references during ADR 0055 migration phase.

---

## Implementation checklist

### Phase 1 — Substrate scaffold (~1.5 weeks)

- [ ] `Sunfish.Foundation.Taxonomy` package
- [ ] `TaxonomyDefinition` + `TaxonomyId` + `SemanticVersion` + `TaxonomyStatus` types
- [ ] `TaxonomyNode` + `TaxonomyNodeStatus` + `TaxonomyNodeRef` types
- [ ] `TaxonomyClassification` reference type
- [ ] `TaxonomyLineage` + `DerivationKind` types
- [ ] `ITaxonomyRegistry` + `ITaxonomyResolver` interfaces
- [ ] InMemory reference implementations
- [ ] Tests: per-type serialization round-trips; basic CRUD

### Phase 2 — Storage + sync (~1 week)

- [ ] CRDT-backed storage for tenant-owned taxonomies (composes ADR 0028)
- [ ] Snapshot-based storage for authoritative taxonomies
- [ ] Foundation.Recovery integration for snapshot signing (composes PR #223)
- [ ] Tests: CRDT merge scenarios; snapshot verification

### Phase 3 — Derivation operations (~1 week)

- [ ] Clone (full copy with new identity + one-time lineage pointer)
- [ ] Extend (additive; inherited nodes preserved with `inheritedFrom`)
- [ ] Alter operations: Move / Split / Merge / Rename — with `mappedFrom` tracking
- [ ] Lineage queries: walk to base; find mapped-from
- [ ] Tests: derivation chains; lineage walking; classification migration safety

### Phase 4 — Governance + audit (~1 week)

- [ ] Per-tenant `taxonomy_governance_regime` configuration (Authoritative / Enterprise / Civilian)
- [ ] Macaroon-authorized authoring (composes ADR 0032)
- [ ] 8 typed audit record types (composes ADR 0049)
- [ ] Tests: regime enforcement; capability checks; audit emission

### Phase 5 — Bundle catalog integration (~0.5 week)

- [ ] `TaxonomyProductDescriptor` registered as bundle content type
- [ ] Browse + install via existing bundle-catalog API
- [ ] Tests: bundle round-trip; install + reference

### Phase 6 — 5 starter taxonomies (~1 week)

- [ ] Sunfish Signature Scopes v1.0
- [ ] Sunfish Equipment Classes v1.0
- [ ] Sunfish Vendor Specialties v1.0
- [ ] Sunfish Inspection Deficiency Categories v1.0
- [ ] Sunfish Contact Use Contexts v1.0
- [ ] Each shipped as a marketplace product
- [ ] Tests: representative-classification scenarios per taxonomy

### Phase 7 — apps/docs + governance guide (~0.5 week)

- [ ] Substrate README in `Sunfish.Foundation.Taxonomy`
- [ ] Governance guide in `apps/docs/foundation/taxonomy.md`
- [ ] Per-vertical regime configuration documentation

**Total: ~5 weeks COB.** Slots into ADR 0055 Dynamic Forms substrate's 12-13 week MVP scope.

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-T1 | Tree vs DAG enforcement in v1 | Stage 02 — recommend tree as primary shape with optional `also_categorized_under: NodeId[]` for multi-axis cases |
| OQ-T2 | Snapshot identity (content-addressed vs version-addressed) | Stage 02 — recommend version-addressed in v1; content-addressing is Phase 3+ optimization |
| OQ-T3 | Per-tenant regime configuration storage | Stage 02 — extend `tenant.metadata` field |
| OQ-T4 | Visual taxonomy editor UI timing | Phase 2.x — defer until first non-CTO admin needs to author |
| OQ-T5 | Standards-body adapter pattern (FHIR ValueSet / NAICS / ICD) | Customer-driven; ship adapters when concrete need surfaces |
| OQ-T6 | Marketplace billing posture for Phase 2.x | CEO decision; default = free-only in v1; paid is Phase 2.x or later |
| OQ-T7 | Cross-version classification queries (find-equivalent-in-version) | Stage 03 — recommend native via cross-version equivalence index using `mappedFrom` chain |
| OQ-T8 | Multi-tenant private taxonomy sharing | Phase 3+ if customer demand surfaces |
| OQ-T9 | Localization workflow for translating node displays | InternationalizedText primitive supports it; translation workflow is its own concern |
| OQ-T10 | Authoring UX for `Alter` operations (move/split/merge/rename) | Stage 02 — recommend explicit confirmation flow with classification-migration preview |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **Visual taxonomy editor UX rejected** by non-CTO admin authors — pivot to simpler authoring or richer guidance
- **Classification migration during Alter operations** breaks production records (mapped classifications become unresolvable) — strengthen migration tooling
- **Cross-tenant federation customer demand** — add federation primitives
- **Standards-body integration concrete need** — ship FHIR/NAICS/ICD adapters
- **Performance regression on classification resolution** at scale (>50ms p95 per resolution) — add caching layer; pre-compile resolvers
- **CRDT merge conflicts** on tenant-owned taxonomies create lost-update bugs in production — strengthen conflict-resolution rules
- **Regulatory compliance regime requires** stricter controls (e.g., FedRAMP, HIPAA-class) than civilian/enterprise/authoritative covers — add new regime
- **Marketplace economics** demand paid taxonomies — Phase 2.x billing work

---

## References

### Predecessor and sister ADRs

- ADR 0001 — Schema Registry Governance (cross-reference; taxonomies and schemas are governed alongside)
- ADR 0007 — Bundle Manifest Schema (composed; taxonomies distribute as bundles)
- ADR 0008 — Foundation.MultiTenancy (consumed)
- ADR 0013 — Provider neutrality (consumed; no vendor SDK leakage)
- ADR 0028 — CRDT engine (consumed; tenant-owned taxonomy editing)
- ADR 0032 — Macaroon capability model (consumed; authorization)
- ADR 0046 — Key-loss recovery (consumed; authoritative-publisher signing)
- ADR 0049 — Audit Trail Substrate (consumed + 8 new event types)
- ADR 0054 — Electronic Signature (consumer; SignatureScope Pattern E uses TaxonomyClassification)
- ADR 0055 — Dynamic Forms Substrate (consumer; Coding/CodeableConcept primitives resolve through this)

### Research artifacts

- [`taxonomy-management-substrate-intake-2026-04-29.md`](../../icm/00_intake/output/taxonomy-management-substrate-intake-2026-04-29.md) — CEO spec captured
- [`oss-primitive-types-research-2026-04-29.md`](../../icm/01_discovery/output/oss-primitive-types-research-2026-04-29.md) — Coding/CodeableConcept primitive design
- [`signature-scope-taxonomy-upf-2026-04-29.md`](../../icm/00_intake/output/signature-scope-taxonomy-upf-2026-04-29.md) — first ADR consumer

### External references

- FHIR ValueSet specification — authoritative-publisher signing precedent
- W3C SKOS (Simple Knowledge Organization System) — taxonomy modeling standard
- ETSI EN 319 122 — taxonomy / commitment-type-indicator pattern in XAdES
- iso-astm:E1762-95:2013 — signature type code system precedent

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Four options considered: full substrate (A); lightweight primitives only (B); adopt external standards directly (C); defer to v2 (D). Option A chosen with explicit rejection rationale for B (CEO directive precludes), C (external standards are integration targets not substitutes), D (v1 dependencies require this substrate).
- [x] **FAILED conditions / kill triggers.** 8 revisit triggers explicit; tied to externally-observable signals.
- [x] **Rollback strategy.** No production data depends on substrate yet (greenfield). Rollback = revert this ADR + revert package + retire 5 starter taxonomies. Cluster modules pre-implementation; no migration to undo.
- [x] **Confidence level.** **HIGH.** CEO spec is detailed; substrate composition leverages 6 existing accepted ADRs; industry alignment validated (FHIR ValueSet, SKOS, ETSI). Risk surface is in long-tail authoring UX and lineage migration tooling, not in core architecture.
- [x] **Anti-pattern scan.** None of AP-1 (10 named open questions with resolution paths), AP-3 (7 phases with binary gates), AP-9 (Stage 0 via prior intake research), AP-12 (5-week estimate with parallelization), AP-21 (every claim cites a source) apply. Critical APs clean.
- [x] **Revisit triggers.** 8 explicit conditions named.
- [x] **Cold Start Test.** Implementation checklist 7 phases × ~5 sub-tasks each = ~35 specific tasks. Fresh contributor reading this ADR + companion intake (PR #234) + ADRs 0028/0032/0046/0049 should be able to scaffold Phase 1 without further clarification.
- [x] **Sources cited.** 10 ADRs referenced; 3 research artifacts cited; 4 external standards referenced.

---

## Sign-off

CTO (research session) — 2026-04-29

**Status: Proposed.** Awaiting CEO sign-off. CTO can dispatch council-review subagent if CO requests; given substrate-class scope + cross-cutting impact, council review is recommended but not blocking acceptance if CO has bandwidth to review directly.
