---
id: 55
title: Dynamic Forms Substrate
status: Proposed
date: 2026-04-29
tier: foundation
concern:
  - persistence
  - configuration
  - dev-experience
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0055 — Dynamic Forms Substrate

**Status:** Proposed
**Date:** 2026-04-29
**Resolves:** Synthesizes a multi-turn architectural conversation 2026-04-29 (CEO directive establishing dynamic forms as the load-bearing MVP feature) into a coherent substrate ADR. Composes 4 prior research artifacts: [`oss-primitive-types-research-2026-04-29.md`](../../icm/01_discovery/output/oss-primitive-types-research-2026-04-29.md) (PR #231), [`dynamic-forms-authorization-permissions-upf-2026-04-29.md`](../../icm/00_intake/output/dynamic-forms-authorization-permissions-upf-2026-04-29.md) (PR #230), [`contact-use-enum-upf-2026-04-29.md`](../../icm/00_intake/output/contact-use-enum-upf-2026-04-29.md) + [`taxonomy-management-substrate-intake-2026-04-29.md`](../../icm/00_intake/output/taxonomy-management-substrate-intake-2026-04-29.md) (PR #234), [`cross-field-rules-engine-upf-2026-04-29.md`](../../icm/00_intake/output/cross-field-rules-engine-upf-2026-04-29.md) (PR #235).

---

## Context

Sunfish was conceived as a no-code platform for collaborative software, but the property-operations cluster (workstreams #16–#30, drafted 2026-04-28) treated Property + Equipment + Inspection + Lease as fixed C# records — a substrate-amnesia anti-pattern that ignored Sunfish's actual platform substrate (ADR 0001 Schema Registry, ADR 0005 Type Customization, `blocks-forms`, `blocks-businesscases`).

CEO redirected 2026-04-29: **dynamic forms is load-bearing for MVP.** Specifically:
- Properties + Equipment + Address + Inspection are not hard-coded entities; they are *type definitions* registered in the schema registry
- Asset structure is a tree node hierarchy (type-tree + instance-tree)
- Properties/value-objects compose: Address can be USAddress | EUAddress | MXAddress (variant), each with custom fields, optionally with LatLng
- Admin-defined types in v1; JSONB storage required from day 1
- Form authorization needs subform/section-level permissions (vendor sees vendor's section; not whole form)
- Cross-field rules need their own substrate (conditional / validation / computed)
- Cross-device sync of both schemas and entity instances is mandatory

The cluster intakes' fixed-schema assumption is wrong. The platform Sunfish *actually is* requires this dynamic-forms substrate to be foundational, with shipped Property/Equipment/Inspection records becoming *seed type definitions* registered through the substrate (or retiring entirely in favor of JSONB-stored instances).

This ADR specifies the substrate. It is the largest single architectural commit of the cluster work because it touches schema definition, type taxonomy, form rendering, cross-field rules, permissions, storage, and sync — all simultaneously.

---

## Decision drivers

- **CEO directive 2026-04-29:** dynamic forms is load-bearing; admin-defined types in v1; JSONB required; section-based permissions; type tree + instance tree; cross-field rules separate substrate
- **Cluster impact:** 14 cluster intakes (workstreams #16–#30) reframe from typed-records to schema-registry-driven; ~5 cluster ADRs (0051, 0052, 0053, 0054, this one) compose
- **Substrate composition:** ADR 0001 (Schema Registry), 0005 (Type Customization), 0007 (Bundle Manifest), 0028 (CRDT), 0032 (Macaroons), 0049 (Audit), 0046 (Recovery — for tenant-key-encrypted blob storage) — all already in flight; this ADR composes them
- **Multi-platform requirement:** rules + forms must run on Web (Photino+Blazor or browser), .NET Anchor desktop, future iOS/Swift — eliminates code-as-rule patterns
- **Local-first + sync:** schemas and entity instances must CRDT-sync across devices per paper §6; schema versioning per paper §7 epoch model
- **Admin authoring:** non-developer authors compose types + rules + permissions; UX must be approachable without enterprise-RBAC training overhead
- **Provider-neutrality:** ADR 0013 enforcement gate active; substrate must not couple to vendor SDKs
- **Storage migration risk:** typed-records (Property, Equipment, Inspection) shipped today; reframing them as schema-registered types must be non-breaking or have a clean migration

---

## Considered options

### Option A — Schema-registry-driven dynamic substrate (CTO recommendation)

Build a foundation-tier substrate that:
- Defines types as data via JSON Schema 2020-12 + Sunfish overlay
- Stores instances as JSONB documents in `Sunfish.Foundation.Assets` IEntityStore
- Renders forms automatically from schemas
- Evaluates cross-field rules via three-tier engine (per cross-field-rules UPF)
- Enforces section-based permissions via macaroons (per permissions UPF)
- Supports admin authoring of new types
- Composes existing CRDT, audit, recovery, taxonomy substrates

Existing typed records (Property, Equipment, Inspection, Lease) become **starter type definitions** registered through the schema registry; instances persist via JSONB; typed records remain as developer-ergonomic projections.

- **Pro:** Aligns with paper's no-code-platform vision
- **Pro:** Admin-defined types in v1 unblocked
- **Pro:** Composes existing substrate without major new primitives
- **Con:** 12-16 weeks MVP work; substantial
- **Con:** Existing typed-record persistence migrates to JSONB; non-trivial

### Option B — Keep fixed schemas; add dynamic forms as v2

Ship MVP on shipped C# records (Property, Equipment, Inspection); defer dynamic forms substrate to v2.

- **Pro:** Faster MVP; ~3-5 weeks for cluster typed-record completion
- **Con:** Direct conflict with CEO directive ("dynamic forms is load-bearing for MVP")
- **Con:** Defers the no-code platform vision; rebuilds typed records later

**Verdict:** Rejected. CEO directive precludes.

### Option C — Hybrid: typed records + parallel JSONB path

Existing typed records persist; new admin-defined types use JSONB. Two storage paths in parallel.

- **Pro:** Preserves shipped work
- **Pro:** Faster than full Option A
- **Con:** Split-brain UX; admin authors don't know which path their type is on
- **Con:** Cross-type queries (e.g., "all entities classified as Equipment") become complex
- **Con:** Migration of typed records to JSONB later is the same work that Option A does now

**Verdict:** Rejected. Defers the migration cost without saving total work.

### Option D — Full event-sourced substrate

Every entity is an append-only event log; current state is a CRDT projection.

- **Pro:** Sync-friendly; audit-trail-native
- **Pro:** Schema evolution handled via lenses (paper §7.3)
- **Con:** Read-path complexity; projection management overhead
- **Con:** All cluster modules and form-rendering re-architect around events
- **Con:** Substantial additional work over Option A; not warranted at SMB-scale Phase 2

**Verdict:** Rejected. Reserved as v3+ if scale or audit-fidelity requires.

---

## Decision

**Adopt Option A.** Foundation-tier dynamic forms substrate composing existing primitives. Specific architectural commitments:

### Substrate components

```
┌─────────────────────────────────────────────────────────────────┐
│                    Dynamic Forms Substrate                      │
│                                                                  │
│  ┌──────────────────┐  ┌──────────────────────────────────────┐ │
│  │ Schema Registry  │  │ Form Engine                          │ │
│  │ (type defs as    │  │ (renders forms; reads schemas;       │ │
│  │  data; CRDT-sync;│  │  evaluates rules; enforces perms)    │ │
│  │  versioned)      │  │                                      │ │
│  └────────┬─────────┘  └──────────┬───────────────────────────┘ │
│           │                       │                              │
│  ┌────────▼─────────┐  ┌──────────▼───────────────────────────┐ │
│  │ Type Taxonomy    │  │ Three-Tier Rules Engine              │ │
│  │ (20 primitives + │  │ Tier 1: JSON Schema if/then/else     │ │
│  │  Variant +       │  │ Tier 2: JsonLogic + custom operators │ │
│  │  Reference)      │  │ Tier 3: Power Fx (computed; v2)      │ │
│  └────────┬─────────┘  └──────────────────────────────────────┘ │
│           │                                                      │
│  ┌────────▼──────────────────────────────────────────────────┐  │
│  │ JSONB Entity Store (foundation-assets-postgres extension) │  │
│  │ - per-tenant                                              │  │
│  │ - schema-validated on write                               │  │
│  │ - tree-hierarchy support (parent-child references)        │  │
│  │ - tenant-key-encrypted PII fields                         │  │
│  └───────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
                              │
   ┌──────────────────────────┼──────────────────────────────┐
   │ Composes existing substrates                            │
   │ - ADR 0001 Schema Registry Governance                   │
   │ - ADR 0005 Type Customization Model                     │
   │ - ADR 0007 Bundle Manifest                              │
   │ - ADR 0028 CRDT engine (schema + instance sync)         │
   │ - ADR 0032 Macaroon capability model (permissions)      │
   │ - ADR 0049 Audit Trail Substrate                        │
   │ - Foundation.Recovery (tenant-key encryption)           │
   │ - Forthcoming Taxonomy Substrate (Coding/CodeableConcept │
   │   refs to versioned taxonomies)                         │
   └─────────────────────────────────────────────────────────┘
```

### Initial contract surface (sketch)

**Schema Registry**

```csharp
namespace Sunfish.Foundation.Schema;

public sealed record SchemaDefinition
{
    public required SchemaId Id { get; init; }
    public required SemanticVersion Version { get; init; }
    public required SchemaStatus Status { get; init; }
    public required IdentityRef Owner { get; init; }
    public required JsonSchemaDocument JsonSchema { get; init; }       // validation core (JSON Schema 2020-12)
    public required SunfishOverlay Overlay { get; init; }              // UI hints + i18n + permissions + rules
    public required TenantId Tenant { get; init; }
    public SchemaLineage? Lineage { get; init; }                       // optional; set if extends another schema
}

public enum SchemaStatus { Draft, Published, Deprecated, Withdrawn }

public interface ISchemaRegistry
{
    Task<SchemaDefinition?> GetAsync(SchemaId id, SemanticVersion? version, CancellationToken ct);
    Task<SchemaDefinition> RegisterAsync(SchemaDefinition definition, CancellationToken ct);
    Task<IReadOnlyList<SchemaDefinition>> ListByTenantAsync(TenantId tenant, CancellationToken ct);
    Task<SchemaDefinition> PublishAsync(SchemaId id, CancellationToken ct);
    Task<SchemaDefinition> DeprecateAsync(SchemaId id, SemanticVersion version, CancellationToken ct);
}

public sealed record SunfishOverlay
{
    public required IReadOnlyDictionary<string, FieldOverlay> Fields { get; init; }
    public required IReadOnlyList<FormSection> Sections { get; init; }    // permissions UPF Approach F
    public required IReadOnlyList<RuleDefinition> Rules { get; init; }    // cross-field rules per UPF
    public required InternationalizedText? Title { get; init; }
    public required InternationalizedText? Description { get; init; }
}
```

**Type Taxonomy** — 20 compound primitives + Variant + Reference + meta-types per OSS primitives research (PR #231).

**Form Section + Permissions** — section-based with macaroon scope per Permissions UPF Approach F.

```csharp
public sealed record FormSection
{
    public required string Id { get; init; }
    public required InternationalizedText Title { get; init; }
    public required IReadOnlyList<string> Fields { get; init; }
    public required SectionAccess Access { get; init; }
}

public sealed record SectionAccess
{
    public required IReadOnlyList<string> ReadRoles { get; init; }
    public required IReadOnlyList<string> WriteRoles { get; init; }
    public string? ReadConditionExpression { get; init; }              // Tier 1 conditional
}
```

**Rules** — three-tier engine per cross-field rules UPF.

```csharp
public sealed record RuleDefinition
{
    public required string Id { get; init; }
    public required RuleTier Tier { get; init; }
    public required RuleScope Scope { get; init; }                     // Field | Section | Schema
    public required string Expression { get; init; }                   // JSON Schema, JsonLogic, or Power Fx (per tier)
    public required RuleActionKind Action { get; init; }               // Visibility | Required | ReadOnly | Validate | Compute
    public InternationalizedText? ErrorMessage { get; init; }
}

public enum RuleTier { JsonSchema, JsonLogic, PowerFx }
public enum RuleActionKind { Visibility, Required, ReadOnly, Validate, Compute }
```

**Form Engine**

```csharp
public interface IFormEngine
{
    Task<FormView> RenderAsync(SchemaId schema, EntityInstance? instance, CapabilityToken token, CancellationToken ct);
    Task<ValidationResult> ValidateAsync(SchemaId schema, EntityInstance candidate, CapabilityToken token, CancellationToken ct);
    Task<EntityInstance> SaveAsync(SchemaId schema, EntityInstance candidate, CapabilityToken token, CancellationToken ct);
}
```

**Entity Store (JSONB-backed)**

```csharp
public sealed record EntityInstance
{
    public required EntityId Id { get; init; }
    public required SchemaId Schema { get; init; }
    public required SemanticVersion SchemaVersion { get; init; }       // version-pinned at save time
    public required TenantId Tenant { get; init; }
    public required JsonDocument Payload { get; init; }                // JSONB column
    public required IReadOnlyList<EntityClassification> Classifications { get; init; }   // taxonomy refs
    public EntityId? ParentId { get; init; }                            // tree hierarchy
    public required DateTimeOffset CreatedAt { get; init; }
    public required IdentityRef CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
    public IdentityRef? UpdatedBy { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }                    // soft-delete
}

public interface IEntityInstanceStore
{
    Task<EntityInstance?> GetAsync(TenantId tenant, EntityId id, CancellationToken ct);
    Task<IReadOnlyList<EntityInstance>> QueryAsync(EntityQuery query, CancellationToken ct);
    Task<EntityInstance> UpsertAsync(EntityInstance instance, CancellationToken ct);
    Task<IReadOnlyList<EntityInstance>> ListChildrenAsync(TenantId tenant, EntityId parent, CancellationToken ct);
}
```

### Key architectural commitments

**1. Storage path: JSONB primary; typed records become projections.** Existing `blocks-properties.Property` + `blocks-property-equipment.Equipment` + `blocks-inspections.Inspection` move from typed-record persistence to JSONB-backed instances. Their C# record shapes remain as developer-ergonomic read projections; writes go through the form engine; storage is JSONB. Per-record-class CP/AP classification per paper §6.3 still applies.

**2. Schema registry as canonical type system.** Property + Equipment + Address (US) + Inspection + Lease + Receipt + Vendor + WorkOrder + remaining cluster types all become schemas registered in the registry. Cluster's `EquipmentClass` enum becomes a Coding-references against a Taxonomy (per Taxonomy Substrate intake). Property's `PostalAddress` becomes the Address compound primitive (USAddress variant in v1).

**3. 20-primitive catalog (per OSS research):** MonetaryAmount, QuantitativeValue, Percentage, Progress, Period, Duration, RecurrenceRule, PersonName, PersonReference, ContactPoint (Pattern G — Role/Type + UseContext map per Contact use-enum UPF), Address (variant), GeoPoint, Identifier, Coding, CodeableConcept (refs to Taxonomy), Rating, Tag/TagList, InternationalizedText, AutoNumber, Attachment. Plus meta-concepts Variant + Reference (with cardinality + parent-child).

**4. Form rendering via schema overlay.** UI controls map by primitive type (date picker for Period.start, address card for Address, map pin for GeoPoint, etc.). Composition is automatic (a value object renders as a sub-form; a list of value objects renders as add/remove table). Variant fields show discriminator dropdown switching the visible sub-form. i18n labels resolve via Foundation.I18n.

**5. Section-based permissions (Approach F per UPF).** Each schema has FormSection list; each section declares ReadRoles + WriteRoles. Form engine intersects section list with actor's macaroon scope (per ADR 0032). Field-level annotations as escape hatch (rare). Vendor magic-link works naturally — token grants section access.

**6. Three-tier rules (per cross-field UPF).** Tier 1 (JSON Schema if/then/else) for visibility/required/readonly. Tier 2 (JsonLogic + custom operators) for cross-field validation. Tier 3 (Power Fx) for computed fields **deferred to v2** per aggressive scope cut.

**7. Cross-device sync via CRDT (ADR 0028).** Schema definitions sync as CRDT documents; entity instances sync as CRDT documents; conflict resolution per paper §6.3 + §7. Schema epoch handling for breaking changes deferred to v2 (additive-only changes in v1).

**8. Admin authoring UX in v1.** Schema editor in Anchor (or Bridge) where admin defines: type name; field list with primitive type picker; sections + role mappings; rules (Tier 1 + 2). Power Fx formula authoring is v2.

**9. Provider-neutrality preserved.** Form engine + schema registry + rule engine are vendor-SDK-free per ADR 0013. JSONB column writes via EF Core Npgsql (already in use per `foundation-assets-postgres`); no provider-specific dependencies.

**10. Migration of shipped cluster work.** Existing typed records (Property, Equipment, Inspection) reframe as schemas:

```
1. Schema definition for "Property" published from existing record shape
2. Existing tests reframe as schema-validated tests
3. Storage: ALTER TABLE adds jsonb column; data migration writes existing typed-record fields as JSONB; reads project back via the schema's read-projection
4. Eventually: typed-record IRepository contract retires; consumers shift to IEntityInstanceStore + schema-aware projection helpers
5. Phase 2.1 cluster modules (Vendors EXTEND, Work Orders EXTEND, Leases EXTEND) ship as schemas from the start, never as typed records
```

This is a multi-PR migration; sequenced after this ADR Accepted.

### What this ADR does NOT do

- **Does not define the Taxonomy Management Substrate** — separate intake (PR #234); referenced as future ADR
- **Does not implement Power Fx (Tier 3 computed fields)** — deferred to v2 per cross-field-rules UPF aggressive scope cut
- **Does not define schema epoch handling for breaking changes** — paper §7.4 reserved for v2; v1 supports additive changes only
- **Does not define schema marketplace UX** — bundle-distributed via existing Foundation.Catalog; full marketplace UX is v2+
- **Does not define schema lenses** (paper §7.3) — v3+ if epoch handling proves insufficient
- **Does not redesign existing ADRs 0001 / 0005** — composes with them; references their substrates

---

## Consequences

### Positive

- **Aligns with platform vision.** Sunfish's no-code-platform vision becomes load-bearing day 1
- **Admin-defined types in v1.** BDFL can author new types (custom equipment classes, custom property attributes, etc.) without code changes
- **Cluster module reframing simplifies.** 14 cluster intakes shift from "ship typed records + UI" to "ship schemas + seed data" — generally less code
- **Cross-platform parity natural.** Schema + rules are data; same engine on Mac + Windows + (future) iOS
- **Compatibility forward.** New verticals (highway management, healthcare, etc.) ship as schema bundles without forking the platform
- **Storage flexibility.** JSONB allows additive schema evolution without migrations
- **Cross-device sync inherits.** ADR 0028 CRDT substrate handles schema + instance sync uniformly
- **Audit-trail uniform.** Every schema change + entity change emits to ADR 0049 substrate
- **Permissions structurally aligned.** Section-based (Approach F) composes ADR 0032 macaroons natively
- **Multi-platform rules.** JsonLogic interpreter exists for .NET, JS, Swift; rules travel + execute everywhere
- **Provider-neutrality preserved.** No vendor SDK leakage in form/schema/rules; ADR 0013 enforcement gate satisfied

### Negative

- **12-16 week MVP timeline.** Substantial; longest ADR commitment to date
- **Storage migration of shipped cluster work.** Property + Equipment + Inspection records persist via JSONB after migration; non-trivial multi-PR work
- **JSONB query ergonomics weaker than typed.** Strongly-typed LINQ queries lose; consumers shift to JSONB indexes + JsonPath where needed
- **Three substrates in flight simultaneously** — schema registry + form engine + rules engine; each has its own complexity
- **Authoring UX is significant scope.** Schema editor + section/role editor + rule builder is itself a multi-week feature
- **Aggressive scope cut on Tier 3 (Power Fx)** means computed fields wait for v2; rules with arithmetic on Money compounds limited in v1

### Trust impact / Security & privacy

- **Schema definitions sync globally.** A malicious or buggy schema change could break form rendering across all devices. Mitigation: schema status (Draft/Published/Deprecated/Withdrawn) gates publication; only Published schemas affect production rendering; CRDT sync respects status field.
- **JSONB payload validation must be schema-aware.** Invalid payloads cannot persist; schema validation is mandatory at write boundary.
- **Section-based permissions are macaroon-enforced** — cryptographically verifiable per ADR 0032; no policy-engine round-trip.
- **Audit emission for every schema change + entity change** — comprehensive audit trail per ADR 0049 supports compliance.
- **Tenant-key-encrypted PII fields** — fields tagged with PII sensitivity classification persist encrypted at rest using Foundation.Recovery primitives (PR #223).
- **Schema editor authoring UX requires CTO-equivalent role by default.** Admin-tier (per-tenant) capability that's elevated above standard user — issue via macaroon caveat.

---

## Compatibility plan

### Existing callers / consumers

The shipped cluster (Properties #17, Property-Equipment #24, Inspections-extension #25, Foundation.Recovery #15) was implemented as typed records. Migration plan:

| Module | Migration strategy |
|---|---|
| `blocks-properties.Property` (PR #210) | Schema "Property" registers; instances migrate to JSONB; typed Property record retains as read-projection only |
| `blocks-property-equipment.Equipment` (PR #213 + #216) | Same pattern; Equipment schema registers; JSONB instances; typed Equipment becomes read-projection |
| `blocks-inspections` (existing + PR #222 extension) | Schema "Inspection" registers; existing rich domain types map to nested schema sections |
| `blocks-leases.Lease` | Schema "Lease" registers; typed Lease retains as read-projection |
| `blocks-rent-collection.Payment` (existing) | Schema "Payment" registers; defers to ADR 0051 Money compound migration |
| `blocks-maintenance.Vendor + WorkOrder + ...` | Schema definitions register; defer typed-record retirement until cluster Vendor + WorkOrder EXTENDs ship |

Migration is multi-PR over ~3-5 weeks of COB time, sequenced after ADR Accepted.

### Validation from COB demo-readiness audit (2026-04-29)

The COB session conducted a demo-readiness audit + Razor-block portability audit (memory notes `project_anchor_demo_readiness_assessment_2026_04_29` + `project_block_ui_component_audit_2026_04_29`; PR #232) that validates this ADR's plan:

- **Mac build is green today.** `dotnet build accelerators/anchor/` succeeds; 3 documented landmines (Xcode 26.3 license, xcode_select_link, Settings.plist AppleSdkRoot) all clean; `.app` bundle materializes. The substrate ships into a working host.
- **Anchor host wiring for the property cluster is at zero.** csproj has no `blocks-properties` / `blocks-property-equipment` / `blocks-inspections` ProjectReference; MauiProgram.cs calls no `AddInMemoryX()` for any cluster module; no nav pages; no kitchen-sink seed. G6 host integration is a **clean slate** when this ADR lands — no rework of existing wiring.
- **Razor blocks are framework-agnostic.** All 7 audited Razor blocks (`InspectionListBlock`, `LeaseListBlock`, `WorkOrderListBlock`, `TaskBoardBlock`, `FormBlock`, `ScheduleViewBlock`, `AssetCatalogBlock`) reference only `Microsoft.NET.Sdk.Razor` + `foundation` + `ui-core` + `ui-adapters-blazor`. **Zero MAUI dependency in any block.** Photino+Blazor port = host-shell rewrite (MauiProgram.cs + Platforms/) only; not per-block.
- **Razor coverage is asymmetric.** `blocks-inspections` ships UI (`InspectionListBlock`); `blocks-properties` + `blocks-property-equipment` ship entity + repo + DI + EF entity config only — no UI. **The dynamic-forms substrate's form engine is the right path** to fill the Properties + Equipment UI gap (vs authoring bespoke Razor blocks per type) since admin-defined types in v1 means many future types won't have bespoke UI either.

These findings confirm the substrate plan: the migration risk is bounded (no incumbent UI to rewrite for cluster modules); the form engine pays off by serving both shipped cluster types and future admin-defined types from the same substrate.

### Affected packages

| Package | Change |
|---|---|
| `Sunfish.Foundation.Schema` (new) | Primary deliverable: schema registry + type taxonomy + overlay |
| `Sunfish.Foundation.Forms` (new) | Form rendering engine; section-based permissions enforcement; JSONB persistence orchestration |
| `Sunfish.Foundation.RuleEngine` (new — extends existing rule-engine substrate) | Three-tier rules engine; JsonLogic + custom operators; JSON Schema if/then/else evaluation |
| `Sunfish.Foundation.Compounds` (new) | 20 compound primitives implementing the OSS-research catalog |
| `Sunfish.Foundation.Taxonomy` (new — separate intake; this ADR references) | Taxonomy substrate for `Coding` + `CodeableConcept` references |
| `foundation-assets-postgres` (existing) | Modified — adds JSONB entity storage path; tree-hierarchy queries; tenant-key-encrypted PII fields |
| `blocks-properties` (shipped) | Modified — Property record retires as primary persistence; becomes read-projection |
| `blocks-property-equipment` (shipped) | Same pattern |
| `blocks-inspections` (shipped + extended) | Same pattern; richer extension surface preserved as schema sections |
| `accelerators/anchor` (existing) | Modified — Schema Editor UI for admin authoring |
| `accelerators/bridge` (existing) | Same |

### ADR amendments triggered

- **ADR 0001 Schema Registry Governance** — confirm + extend; this ADR's `ISchemaRegistry` is the v1 implementation
- **ADR 0005 Type Customization Model** — confirm + extend; `SchemaLineage` formalizes type customization
- **ADR 0007 Bundle Manifest Schema** — addendum; bundles can declare type-pack contents (schemas + rules + sections + taxonomies)
- **ADR 0028 CRDT engine** — confirm; schema + entity instances sync via existing CRDT substrate
- **ADR 0049 Audit Trail Substrate** — addendum; new audit record types: SchemaRegistered, SchemaPublished, SchemaDeprecated, EntityCreated, EntityUpdated, EntityDeleted, FormSectionAccessGranted, FormSectionAccessDenied, RuleEvaluated, RuleEvaluationFailed
- **ADR 0032 Multi-team Anchor + macaroon model** — confirm; section-scope caveat extension per Permissions UPF
- **Cluster intake INDEX (`property-ops-INDEX-intake-2026-04-28.md`)** — substantial revision; per-intake disposition shifts to schema-driven

---

## Implementation checklist

### Phase 1 — Schema Registry foundation (~3 weeks)

- [ ] `Sunfish.Foundation.Schema` package scaffolded
- [ ] `SchemaDefinition` + `SchemaId` + `SemanticVersion` + `SchemaStatus` types
- [ ] `ISchemaRegistry` interface + InMemorySchemaRegistry reference impl
- [ ] JSON Schema 2020-12 validation library integrated (NJsonSchema or alternative; CTO selects)
- [ ] CRDT-backed persistence for schema definitions (composes ADR 0028)
- [ ] Schema versioning (SemVer) + status lifecycle (Draft → Published → Deprecated → Withdrawn)
- [ ] Audit emission for schema lifecycle events
- [ ] Tests: schema registration, version conflicts, status transitions, CRDT sync

### Phase 2 — 20-primitive catalog (~2 weeks)

- [ ] `Sunfish.Foundation.Compounds` package scaffolded
- [ ] All 20 compound primitives implemented (per OSS research)
- [ ] `Variant` + `Reference` meta-concepts
- [ ] String formats (email, phone-e164, url, uuid, ISO codes)
- [ ] JSON serialization round-trips for all types
- [ ] Tests: per-primitive serialization + validation + edge cases

### Phase 3 — Entity Store (JSONB) (~2 weeks)

- [ ] `foundation-assets-postgres` extended with JSONB entity storage
- [ ] `EntityInstance` + `IEntityInstanceStore` interface + Postgres impl
- [ ] Tree-hierarchy support (parent-child references)
- [ ] Schema-validated writes (reject invalid payloads)
- [ ] Tenant-key-encrypted PII fields (composes Foundation.Recovery)
- [ ] CRDT sync for entity instances (composes ADR 0028)
- [ ] JSONB query helpers (JsonPath-based; bounded query API)
- [ ] Tests: storage round-trips; schema-validation rejection; tenant isolation; tree queries

### Phase 4 — Three-tier Rules Engine (~3 weeks; Tier 3 deferred to v2)

- [ ] `Sunfish.Foundation.RuleEngine` package
- [ ] Tier 1: JSON Schema if/then/else evaluator integrated
- [ ] Tier 2: JsonLogic interpreter (.NET) + 5-10 custom operators (path, compound, today, audit_event_count_since, etc.)
- [ ] Tier 2 cross-platform parity test suite (.NET + JS via Blazor WebAssembly OR JsonLogic JS interpreter)
- [ ] Rule definition serialization
- [ ] Audit emission for rule evaluations + failures
- [ ] Tests: per-tier evaluation; cross-platform parity; performance (50ms p95 target)

### Phase 5 — Form Engine (~3 weeks)

- [ ] `Sunfish.Foundation.Forms` package
- [ ] `IFormEngine` interface + reference implementation
- [ ] Field-type → control mapping (date picker, address card, map pin, etc.)
- [ ] Composition (sub-forms for value objects; add/remove tables for lists)
- [ ] Variant rendering (discriminator dropdown + sub-form switching)
- [ ] i18n integration (Foundation.I18n)
- [ ] Read vs Edit modes
- [ ] Section-based permissions (per Permissions UPF Approach F)
- [ ] Macaroon-based section-scope verification (composes ADR 0032)
- [ ] Tests: representative form renders; permission filtering; multi-platform parity (Photino + .NET)

### Phase 6 — Admin Authoring UX (~3 weeks)

- [ ] Schema editor UI in Anchor (and Bridge)
- [ ] Field-list authoring with primitive type picker
- [ ] Section + role mapping authoring
- [ ] Rule builder (visual condition builder for Tier 1; rule-list editor for Tier 2)
- [ ] Inline preview with sample data
- [ ] Schema publish + deprecate workflow
- [ ] Tests: authoring UX walkthrough (BDFL composes 3 representative schemas in <15 min)

### Phase 7 — Migration of shipped cluster work (~3 weeks)

- [ ] Property schema definition + JSONB storage migration
- [ ] Equipment schema definition + JSONB storage migration
- [ ] Inspection schema definition + JSONB storage migration (Inspection extension preserves)
- [ ] Lease schema definition (existing typed retains as read-projection)
- [ ] Test migration: existing data round-trips through schema
- [ ] Cluster intakes (Vendors, WorkOrders, Receipts, etc.) reauthor as schema specs

### Phase 8 — Audit + observability + documentation (~1 week)

- [ ] All audit record types added to kernel-audit per ADR 0049 amendment
- [ ] Performance monitoring: form-load latency, rule-evaluation latency
- [ ] apps/docs entry covering substrate + authoring guide + admin patterns

**Total: ~16 weeks** (Phases 1-3 + 4-5 in parallel + 6-7 in parallel + 8). Aggressive scope cut deferring Tier 3 saves 3-4 weeks → **~12-13 weeks effective MVP timeline.**

---

## Open questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-DF1 | JSON Schema library — NJsonSchema vs JsonSchema.Net vs custom wrapper? | Stage 02 design; recommend NJsonSchema (mature .NET; broad operator support) |
| OQ-DF2 | JsonLogic interpreter — community lib vs handroll? | Stage 02 design; recommend community lib with Sunfish-specific operator extensions |
| OQ-DF3 | Schema versioning conflict resolution — what happens when device A and device B both publish v2.0.0? | CRDT substrate's standard resolution; version + tenant-id forms the unique key; conflicts surface to admin for review |
| OQ-DF4 | Entity tree queries: how deep is recursion in v1? | Stage 02 design; recommend explicit-depth queries (no unbounded recursion) |
| OQ-DF5 | Schema editor authoring UX in Anchor or Bridge first? | CEO decision; default Anchor first (admin uses desktop) |
| OQ-DF6 | Bundled vs separate ADRs for Taxonomy Substrate? | Already separate intake; this ADR references; that ADR drafts after this lands |
| OQ-DF7 | Power Fx (Tier 3) cross-platform feasibility for v2 — research before committing | Defer to Phase 2.2 separate research |
| OQ-DF8 | Initial type-pack starter — which schemas ship in `Property Management v1.0`? | Property + Unit + Equipment + Address + Lease + Party + Inspection + Receipt + Vendor + WorkOrder. Confirm with CEO. |

---

## Revisit triggers

This ADR should be re-evaluated when any of the following fire:

- **Schema authoring UX rejected by BDFL on usability grounds** — consolidate or simplify
- **JSONB query performance regressions** in production at typical Phase 2 scale (6 LLCs × ~500 entities) — pivot to typed projections + read-replica
- **Tier 3 (Power Fx) demand exceeds tolerance** — admin authors push for computed fields before v2 timeline
- **Schema epoch handling becomes urgent** — breaking schema changes accumulate; lenses (paper §7.3) need to ship
- **Cross-tenant schema sharing** crosses customer-pull threshold — marketplace v2 work
- **Performance regression on form rendering** (>200ms p95) — rule evaluator caching; schema pre-compilation
- **Compliance regime requires audit-trail not currently captured** (e.g., FCRA full-record-of-edits) — extend audit emission
- **iOS Swift port** of rule interpreter doesn't reach parity — cross-platform rule eval gap

---

## References

### Predecessor and sister ADRs

- ADR 0001 — Schema Registry Governance (foundation; this ADR is the v1 implementation)
- ADR 0005 — Type Customization Model (extends; `SchemaLineage` formalizes)
- ADR 0007 — Bundle Manifest Schema (extends; bundles include type-pack contents)
- ADR 0008 — Foundation.MultiTenancy (consumed)
- ADR 0013 — Provider neutrality (consumed; no vendor SDK leakage)
- ADR 0028 — CRDT engine (consumed; schema + instance sync)
- ADR 0032 — Macaroon capability model (consumed; section-scope per Permissions UPF)
- ADR 0046 — Key-loss recovery (consumed; tenant-key-encrypted PII fields)
- ADR 0049 — Audit Trail Substrate (consumed + extended with new event types)
- ADR 0051 — Payments (Proposed; consumes Money compound primitive)
- ADR 0052 — Bidirectional Messaging (Proposed; references InternationalizedText + Attachment primitives)
- ADR 0053 — Work Order Domain Model (Proposed; reframed as schema in cluster reconciliation)
- ADR 0054 — Electronic Signature (Proposed; references AuditCorrelation)

### Research artifacts (this ADR's load-bearing inputs)

- [`oss-primitive-types-research-2026-04-29.md`](../../icm/01_discovery/output/oss-primitive-types-research-2026-04-29.md) — 11 sources; 20-primitive catalog
- [`dynamic-forms-authorization-permissions-upf-2026-04-29.md`](../../icm/00_intake/output/dynamic-forms-authorization-permissions-upf-2026-04-29.md) — Approach F (section-based + macaroon)
- [`contact-use-enum-upf-2026-04-29.md`](../../icm/00_intake/output/contact-use-enum-upf-2026-04-29.md) — Pattern G (Role/Type + UseContext map)
- [`taxonomy-management-substrate-intake-2026-04-29.md`](../../icm/00_intake/output/taxonomy-management-substrate-intake-2026-04-29.md) — separate substrate; this ADR references
- [`cross-field-rules-engine-upf-2026-04-29.md`](../../icm/00_intake/output/cross-field-rules-engine-upf-2026-04-29.md) — three-tier rules engine

### Roadmap and specifications

- Local-Node Architecture paper §6 (sync) + §7 (schema epochs)
- [`property-ops-INDEX-intake-2026-04-28.md`](../../icm/00_intake/output/property-ops-INDEX-intake-2026-04-28.md) — cluster INDEX; reframes per this ADR

### External

- JSON Schema Draft 2020-12 specification
- JsonLogic specification
- Microsoft Power Fx (open-sourced; reference impl)
- FHIR HumanName, Address, ContactPoint, Quantity, Period, Identifier, Coding (composite type designs)
- Schema.org Person, PostalAddress, MonetaryAmount, QuantitativeValue
- iCalendar RFC 5545 (RecurrenceRule)
- W3C Personal Names Around the World + ICU CLDR
- GeoJSON RFC 7946

---

## Pre-acceptance audit (5-minute self-check)

- [x] **AHA pass.** Four options considered: schema-registry-driven (A); fixed-schemas-defer-dynamic (B); hybrid typed+JSONB (C); full event-sourced (D). A chosen with explicit rejection rationale for B (CEO directive precludes), C (defers cost; same total work), D (premature; v3+).
- [x] **FAILED conditions / kill triggers.** 8 revisit triggers explicit; tied to externally-observable signals.
- [x] **Rollback strategy.** No production data depends on substrate yet (cluster Phase 2 not in production). Rollback = revert this ADR + revert substrate packages + restore typed-records as primary persistence. Migration is one-way once data accumulates; pre-migration the rollback is clean.
- [x] **Confidence level.** **HIGH.** Composes existing substrates (ADR 0001, 0005, 0028, 0032, 0049 all accepted); 5 prior research artifacts ground the design; industry alignment validated. Risk surface is in long-tail authoring UX + JSONB query performance, not in core architecture.
- [x] **Anti-pattern scan.** None of AP-1 (4 named assumptions with validation), AP-3 (8 phases with binary gates), AP-9 (Stage 0 explicitly via prior research), AP-12 (12-16 week estimate with parallelization caveat), AP-21 (every claim cites a source) apply. Critical APs clean.
- [x] **Revisit triggers.** 8 explicit conditions named.
- [x] **Cold Start Test.** Implementation checklist 8 phases × ~5 sub-tasks each = ~40 specific tasks. Fresh contributor reading this ADR + 5 prior research artifacts + ADRs 0001/0028/0032/0049 should be able to scaffold Phase 1 (schema registry foundation) without further clarification.
- [x] **Sources cited.** 13 ADRs referenced; 5 research artifacts cited; 7 external standards referenced.

---

## Sign-off

CTO (research session) — 2026-04-29

**Status: Proposed.** Awaiting CEO sign-off after council-review subagent dispatch (CTO will dispatch as next step before flipping Status: Accepted, given this ADR's substantial scope + cross-cutting impact).
