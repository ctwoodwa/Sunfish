# ADR 0010 — Templates Module Boundary (Foundation.Catalog vs. blocks-templating)

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Whether template primitives (`TemplateDefinition`, `TenantTemplateOverlay`, `TemplateMerger`, `TemplateKind`) stay in `Sunfish.Foundation.Catalog` or get extracted into a new `blocks-templating` domain module.

---

## Context

ADR 0005 introduced metadata templates (forms, diligence checklists, reports, notifications, documents) as the Layer 3 customization mechanism. The implementation landed in `Sunfish.Foundation.Catalog.Templates`:

- `TemplateDefinition` — id, version, kind, data schema, UI schema.
- `TenantTemplateOverlay` — baseRef + RFC 7396 patches.
- `TemplateMerger` — applies overlay to base, returns merged definition.
- `TemplateKind` — enum (Form, DiligenceChecklist, Report, Notification, Document).

Three forces now push on the question of where these belong:

1. **"Templates are data"** — they are user-authored artifacts, not platform invariants. Arguably not Foundation material.
2. **"Templates compose into bundles"** — bundle manifests reference template ids; that smells like a domain module that bundles consume.
3. **"Templates are catalog content"** — extension fields and bundle manifests also live in Foundation.Catalog; templates fit the same category.

The practical question is not philosophical — it's **when do we pay the extraction cost**, and what criteria trigger the split.

---

## Decision

**Keep templates in `Sunfish.Foundation.Catalog.Templates` for now.** Extract to a dedicated `blocks-templating` module when one or more of the following criteria fire.

### Extraction criteria (any one triggers extraction)

1. **Three or more template kinds ship with materially different runtime behavior.** Today all five kinds share the same shape (JSON Schema + UI schema + overlay). If, say, `DocumentTemplate` grows a PDF-generation pipeline or `ReportTemplate` grows a projection/query compiler, the runtime behavior per kind stops fitting in Foundation.
2. **Template-authoring UI components ship.** Form builders, diligence-checklist designers, and report editors are domain module concerns, not Foundation concerns. Once any of these exist, templates belong in a module consumed by Bridge and kitchen-sink.
3. **Persistence grows beyond embedded seed.** If tenants begin authoring templates into a database (versioned table per kind, query surfaces per kind), the persistence contracts move into a module. Foundation.Catalog stays with the *schema* of templates but not their storage.
4. **Template validation outgrows the merge primitive.** Adding cross-template validation (e.g. "this report references a form field that no longer exists"), lifecycle state machines (draft → review → published), or workflow attachments moves templates out of pure-data territory.
5. **A fourth consumer appears beyond Foundation.Catalog + `blocks-diligence` + the (future) form renderer.** More than three first-party consumers usually justifies a dedicated module.

### Until then: what stays in Foundation.Catalog

- The four existing types (`TemplateDefinition`, `TenantTemplateOverlay`, `TemplateMerger`, `TemplateKind`).
- Reference template seed JSON (embedded like the bundle manifests; see ADR 0007).
- The `BundleManifestLoader`-style helpers when they become needed for templates.

### Migration plan (when a criterion fires)

1. New package: `packages/blocks-templating/Sunfish.Blocks.Templating.csproj`, references `Sunfish.Foundation.Catalog`.
2. **Move** the template types into the new package under namespace `Sunfish.Blocks.Templating`. Use type-forwards from `Sunfish.Foundation.Catalog.Templates.*` for source compatibility during a one-major-version deprecation window.
3. Templates surface becomes the new module's API; `Sunfish.Foundation.Catalog` retains only the template **contracts it needs at the catalog layer** (e.g. a `TemplateReference` lookup used by bundle validation), if any.
4. Bundle manifests referencing templates continue to work because they reference templates by string id.
5. Consumers migrate by `using Sunfish.Blocks.Templating;` replacing the old namespace.

### What this ADR does not decide

- The renderer. Form rendering, diligence-checklist rendering, and report rendering each require adapter-side code (Blazor, React). Those live in UI adapters regardless of where the template data types sit.
- The reporting projection pipeline (ADR-reserved for `blocks-reporting` in P2).
- Multi-language template localization — a future ADR once BCP-47 templates start shipping.

---

## Consequences

### Positive

- No churn today. Templates stay where consumers already find them.
- Clear criteria remove the "when do we split" debate from every future review.
- When extraction happens, type-forwards keep the migration non-breaking for one major version.

### Negative

- Foundation.Catalog will feel slightly bigger than "just catalog" until extraction. Acceptable while template surface stays minimal.
- New contributors may reach for Foundation.Catalog as the home for template-authoring UI or persistence and need to be redirected to `blocks-templating` (once it exists).

### Follow-ups

- Track extraction-criterion triggers in roadmap notes; the first trigger opens an ADR that flips this decision.
- When `blocks-diligence` begins shipping (P2), re-evaluate whether its checklist template needs behavior that triggers criterion #1 or #4.

---

## References

- ADR 0005 — Type-Customization Model (establishes templates as Layer 3).
- ADR 0007 — Bundle Manifest Schema (bundles reference templates by id).
- `packages/foundation-catalog/Templates/*` — current home of the template types.
