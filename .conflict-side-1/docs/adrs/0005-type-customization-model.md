# ADR 0005 — Type-Customization Model (Typed vs. Dynamic Balance)

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Platform question — how Sunfish balances strongly-typed contracts against tenant/bundle-level customization without devolving into pure dynamic schemas or brittle hard-coded interfaces.

---

## Context

Sunfish needs to ship templated forms, due-diligence checklists, reports, and notification templates that are immediately useful out of the box, while letting tenants customize them to fit their business. Two failure modes bound the problem:

- **Pure static interfaces.** Every field is a C# property; customization requires a code change and redeploy. Fast and queryable, but every tenant asking for a new field blocks on engineering. Refactoring APIs over time costs every consumer.
- **Pure dynamic schemas** (EAV, JSON blob per entity with no schema catalog). Infinitely flexible, but reporting, validation, indexing, IDE support, and upgradability all collapse. Tenant-specific schema drift becomes invisible.

Neither is acceptable for a multi-bundle SaaS accelerator. The prevailing industry pattern — used by Salesforce, Microsoft Dataverse, Acumatica, and ABP Framework — is a layered hybrid: a typed invariant core, typed business entities with a registered extension mechanism, metadata-first user-authored artifacts (forms, checklists, reports), and a narrowly-gated escape hatch for truly dynamic custom objects. Customization flows through a catalog so drift is visible and upgrades are safe.

Sunfish's specific constraints:

- Bundle manifests (ADR follow-up) must control what customization is available per business case.
- Three deployment modes (lite/local-first, self-hosted, hosted SaaS) must share one model; local-first cannot assume a central schema service.
- UI adapters (Blazor today, React later) must be able to render user-authored artifacts without Foundation depending on any specific renderer.
- `TreatWarningsAsErrors=true` and `GenerateDocumentationFile=true` project-wide — extension mechanisms must not undermine compile-time safety for the typed core.

---

## Decision

Adopt a **four-layer type-customization model**, with bundle manifests as the control plane and a first-class promote-to-column escape for hot dynamic fields.

### Layer 1 — Invariant core (hard-typed, no extensions)

Platform invariants — tenant identity, subscription, edition, audit envelope, domain event envelope, money/currency primitives, workflow state machine scaffolding — are C# types with no extension point. Changes require an ADR. These live in `Sunfish.Foundation.*` contracts.

### Layer 2 — Extensible typed entities (typed core + registered extension bag)

Canonical business entities (Contact, Lease, Asset, Invoice, WorkOrder, …) have a known C# shape and implement `IHasExtensionData`. Extension fields are declared through `IExtensionFieldCatalog` with a `ExtensionFieldSpec` that declares type, scope (Bundle or Tenant), storage (Json or PromotedColumn), searchability, and validation.

- **Json storage (default):** serialized into a single JSON column alongside the entity.
- **PromotedColumn storage:** mapped to a real column via an `IEntityFieldMapper<TEntity>` abstraction; EF Core implementations live in a separate adapter package.

Bundles declare extension fields for the entities they activate. Tenants can add further extension fields within a bundle-declared policy. Foundation.Catalog is the single source of truth for what fields exist and how they are stored; silent schema drift is prevented because the catalog is required before persistence.

### Layer 3 — Metadata templates with tenant overlays

User-authored artifacts — forms, diligence checklists, report definitions, notification templates, document templates — are stored as metadata conforming to **JSON Schema 2020-12** for the data shape, plus a separate **UI Schema** for rendering, plus template metadata (id, version, baseRef, locale).

Tenant customization is expressed as a `TenantTemplateOverlay` referencing a specific base template version. Overlays follow **JSON Merge Patch (RFC 7396)** semantics; base and overlay merge at resolution time. When a base template is upgraded, Sunfish can produce a three-way diff so tenants re-apply their overlay against the new base rather than losing it.

Renderers live in UI adapters; the Foundation contracts stay render-agnostic. JSONForms is the conceptual model (clean data/UI schema separation); adapters may front JSONForms, a Telerik form builder via `compat-telerik`, or a native renderer.

### Layer 4 — Ad-hoc custom objects (gated, rare)

When tenants need an entity Sunfish cannot anticipate, a `CustomObjectDefinition` in `Foundation.Catalog` declares a new entity from metadata. Contracts ship now; the runtime is deferred until a real bundle requires it. Custom objects are gated — a bundle manifest must opt a tenant in before any can be defined. This prevents custom objects from becoming the default customization path.

### Cross-cutting: reporting over mixed schemas

Reports never query extension JSON directly. `Foundation.Catalog` will expose contracts (not implementations in this ADR) for:

- **CQRS-style read-model projections** — domain events project into typed read models.
- **A semantic-model metadata surface** — dimensions, measures, and tenant-safe query scopes.
- **Promote-to-column** — the same mechanism used by Layer 2 for hot extension fields.

Reporting engines and semantic-layer integrations are chosen later.

### Control-plane rule

Every customization decision is recorded in a catalog: extension fields (`IExtensionFieldCatalog`), bundle manifests, template overlays, custom object definitions. Nothing writes to the database or renders to the UI unless the catalog has seen the shape first.

---

## Consequences

### Positive

- Customers get immediate value from canonical templates and extension-capable entities without waiting on engineering.
- Reports remain queryable because the typed core and promote-to-column paths preserve indexable columns.
- Template inheritance makes Sunfish upgrades non-destructive for tenants who customize.
- The catalog-required rule prevents silent schema drift — a long-standing failure mode of EAV and uncatalogued JSON-bag approaches.
- Bundles retain control: an enterprise bundle can expose more extension fields and custom-object authoring than a lite bundle.

### Negative

- More moving parts than either extreme. Foundation now owns an extension bag, a field catalog, a template contract, and (later) a custom-object registry.
- Promote-to-column crosses persistence boundaries and requires an EF Core adapter — another package to maintain.
- Merging overlays on template upgrades is a UX problem we will need to solve (three-way merge tooling).

### Follow-ups

1. Ship `Sunfish.Foundation.Catalog` with extension-field and template primitives (this ADR's companion code).
2. Define the bundle manifest schema (`BusinessCaseBundleManifest`) in a follow-up ADR and place reference manifests as seed JSON inside the catalog package.
3. Implement `Sunfish.Foundation.Catalog.EntityFrameworkCore` as a separate adapter package that maps promoted fields to real columns.
4. Choose Blazor form renderer (native JSON-Schema renderer vs. `compat-telerik` fronting Telerik Form) — separate ADR.
5. Define projection and semantic-model contracts in `blocks-reporting`.
6. Document the three-way merge UX for template upgrades before the first external tenant overlays exist.

---

## References

- ABP Framework, *Object Extensions* — `IHasExtraProperties`, `ObjectExtensionManager`, `MapEfCore`. <https://abp.io/docs/latest/framework/fundamentals/object-extensions>
- ABP Framework, *Module Entity Extensions*. <https://abp.io/docs/latest/framework/architecture/modularity/extending/module-entity-extensions>
- Salesforce Platform — custom fields, page layouts, flows as metadata (industry reference for metadata-first SaaS).
- Microsoft Dataverse — tables, columns, forms, business rules as metadata; Solutions for packaging.
- Acumatica Customization Projects — overlay model that survives upgrades.
- JSON Schema 2020-12 core specification.
- RFC 7396, JSON Merge Patch. <https://www.rfc-editor.org/rfc/rfc7396>
- JSONForms — schema-driven forms with separate UI schema. <https://jsonforms.io/>
- `JsonSchema.Net` 8.0.5 (MIT) — already pinned in `Directory.Packages.props`; used by tests and eventually by template validation.
- *Designing Metadata-Driven UI Customization for Multi-Tenant SaaS* (Bombe, 2025). <https://sollybombe.medium.com/designing-metadata-driven-ui-customization-for-multi-tenant-saas-b13140221e5c>
