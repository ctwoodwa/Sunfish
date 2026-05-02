---
id: 41
title: Dual-Namespace Components by Design (Rich vs. MVP)
status: Accepted
date: 2026-04-26
tier: ui-core
concern:
  - dev-experience
  - ui
composes:
  - 22
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0041 — Dual-Namespace Components by Design (Rich vs. MVP)

**Status:** Accepted (2026-04-26)
**Date:** 2026-04-26
**Resolves:** A structural pattern that exists across `packages/ui-adapters-blazor/Components/` — `SunfishGantt`, `SunfishScheduler`, `SunfishSpreadsheet`, and `SunfishPdfViewer` each appear in **two** folders under different namespaces. The pattern was created intentionally per [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md) Tier 3 (rich-vs-MVP scheduling family) but was not separately captured as an ADR. PR #117 (`docs(a11y): correct cascade-batch report — Gantt/Scheduler not duplicates`) corrected an incorrect "duplicate" flag from the cascade-batch a11y report and aborted a dedup investigation that would have destroyed one half of each pair. This ADR backfills the explicit policy: the pairs are **not duplicates**, they serve different consumer needs, and they may not be deduped without a formal `sunfish-api-change` ICM pipeline.

---

## Context

Four component pairs exist today in `packages/ui-adapters-blazor/Components/`:

| Component | "Rich" location (kitchen-sink demo target) | "MVP" location (canonical leaf) |
|---|---|---|
| `SunfishGantt` | `DataDisplay/Gantt/SunfishGantt.razor` | `Scheduling/SunfishGantt.razor` |
| `SunfishScheduler` | `DataDisplay/Scheduler/SunfishScheduler.razor` (rich variant) | `Scheduling/SunfishScheduler.razor` |
| `SunfishSpreadsheet` | `DataDisplay/Spreadsheet/SunfishSpreadsheet.razor` | `Editors/SunfishSpreadsheet.razor` |
| `SunfishPdfViewer` | `DataDisplay/PdfViewer/SunfishPdfViewer.razor` | `Media/SunfishPdfViewer.razor` |

Each pair shares the type name `Sunfish*` but lives in two distinct namespaces. Each pair has **active callers under both namespaces** — kitchen-sink demos consume the rich `DataDisplay.*` versions; the catalog entries (`pdfviewer`, `spreadsheet`, `scheduler`, `gantt`) under the [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md) example-catalog point at the MVP `Scheduling.*` / `Editors.*` / `Media.*` variants.

The pattern was introduced when the rich variants were authored to satisfy kitchen-sink's Telerik-verbose demo standard while the MVP variants remained as the canonical small-surface contract under the framework-agnostic taxonomy. Both are intentional; both serve different roles.

The implicit policy ("don't dedupe these") was visible only in:

1. XML doc comments on the type declarations (e.g., `Scheduling/SunfishGantt.razor.cs:23-27` names the sibling and explains the namespace coexistence).
2. The aborted dedup investigation by subagent `aa304586d15820a26` (cited in PR #117), which correctly halted before destructive action because the discovery surfaced the dual-purpose design.
3. The user's memory note `project_dual_namespace_components`.

PR #117 corrected the cascade-batch a11y report's incorrect "duplicate" flag for these four pairs. But the rule itself was not yet written as an ADR. Without one, the next batch tool, refactor pass, or new contributor will see what looks like duplication and propose the same dedup — and the next subagent might not abort.

The question this ADR answers: **what is the explicit policy that tells future maintainers (human or agent) to leave these pairs alone, and what is the process if a future change really does need to consolidate them?**

---

## Decision drivers

- **Two consumer paths exist by design.** kitchen-sink demos need Telerik-verbose richness — multi-feature panels, in-page theme switching, multi-file source viewing, the full demo-page-shell pattern from [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md). The canonical leaf surfaces (per the example catalog) need a smaller, more stable API shape that downstream apps can consume without dragging in demo-specific dependencies. Different audiences, different APIs.
- **Type-name reuse is intentional.** Both folders export the same C# type name (`SunfishGantt`) under different namespaces. Consumers `using` the namespace they want; the type name is consistent for cognitive-load reasons (one mental model, two depths). This is a deliberate convention, not an oversight.
- **Dedup is destructive.** Picking either half and deleting the other breaks one of the consumer paths. The rich variant has demo-specific surface area; the MVP variant has stability constraints from the catalog contract. Neither is a strict superset.
- **Periodic refactor pressure is high.** Every code-quality scan, every dedup tool, every "let's clean up the components folder" pass will flag these pairs as duplicates. Without an explicit policy, the pressure accumulates and eventually wins — usually destructively.
- **An api-change ICM pipeline already exists for breaking changes.** Per the project's CLAUDE.md routing, deduping a public component IS a breaking API change (consumers using one namespace would need to migrate). The `sunfish-api-change` pipeline variant is the right process for that change. This ADR points dedup proposals at that pipeline rather than at ad-hoc cleanup.
- **Pre-release posture allows the dual-namespace pattern.** Until v1 ships, "two namespaces, one type name" is a maintainable tradeoff. Post-v1 it becomes harder to reshape without coordinated migration; the api-change pipeline is the gate.

---

## Considered options

### Option A — Status quo: dual namespaces, no formal policy

Leave the dual-namespace pattern documented only via XML doc comments and the aborted-dedup record.

- **Pro:** Zero work.
- **Con:** Every new code-quality tool, batch report, or fresh-eyes contributor re-discovers the apparent duplication and proposes dedup. The PR-#117 correction will need to be repeated.
- **Con:** A subagent without the discovery context might not abort (the aa304586 case was lucky — the subagent surfaced the doc comments and saw the intent; another might not).
- **Rejected.**

### Option B — Dedup the pairs (consolidate to one location each)

Pick the rich or MVP variant per pair, delete the other, update consumers.

- **Pro:** Surface area shrinks; "one component, one location" mental model.
- **Con:** Breaks one of the two consumer paths. Either kitchen-sink loses its demo-specific richness, or the catalog leaf loses its small-stable surface.
- **Con:** Per CLAUDE.md, this is a breaking API change requiring the `sunfish-api-change` ICM pipeline (intake → discovery → architecture → package-design → ... → release with migration guide). Not appropriate as a dedup pass.
- **Con:** The rich variants exist BECAUSE the MVP shape was insufficient for kitchen-sink. Reverting that decision needs the same depth of analysis as the original split.
- **Rejected** as an ad-hoc cleanup. NOT rejected as a future-direction option, but if pursued must go through the api-change pipeline. See "Process if a future change DOES require consolidation" below.

### Option C — Explicit dual-namespace policy ADR (this ADR)

Write the policy down: dual namespaces are intentional; do not dedupe; if dedup is genuinely needed, route through `sunfish-api-change` ICM pipeline.

- **Pro:** Visible to every future contributor (human or agent) on the first ADR scan.
- **Pro:** The rule has a name; "ADR 0041 says don't dedupe these" is a single citation that ends the conversation in code review.
- **Pro:** Defines the legitimate process for the rare case where consolidation IS the right call.
- **Pro:** Cross-referenced from [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md) (the rich-vs-MVP catalog tier system) so the relationship is bidirectional.
- **Con:** One more ADR to maintain. Modest cost.
- **Adopted.**

### Option D — Rename one half of each pair to disambiguate at the type level

Rename rich variants to e.g. `SunfishGanttRich`, leaving MVPs as `SunfishGantt`. No more shared type names.

- **Pro:** Name resolves the apparent-duplication signal that scanners trip on.
- **Pro:** Consumers see the difference at the type level, not just the namespace level.
- **Con:** Breaks every existing consumer of the rich variants (kitchen-sink demos). Migration cost.
- **Con:** Discards the deliberate "one type name, two depths" cognitive convention.
- **Con:** `SunfishGanttRich` is awkward; what about `SunfishGanttPro`? `SunfishGanttDemo`? Naming bikeshed.
- **Rejected.** The shared type name is a feature, not a bug.

### Option E — Move rich variants to a separate package (e.g., `ui-adapters-blazor-rich` / `ui-adapters-blazor-demos`)

Physically separate the rich variants into a new package. Kitchen-sink takes the new package; MVPs stay in the main adapter package.

- **Pro:** Clean physical separation; main adapter package shrinks.
- **Pro:** Downstream apps that don't need rich variants can avoid pulling them in.
- **Con:** Significant refactor — package-boundary change, NuGet package proliferation, dependency-graph reshuffling.
- **Con:** Solves a problem we don't have yet — main adapter package size is not currently a complaint.
- **Defer.** This is the right answer if a future v1+ scoping pass decides the rich variants belong in a separate package. Until then, dual-namespace within one package is fine.

---

## Decision

**Adopt Option C: ratify the dual-namespace pattern as intentional. Document the explicit policy: do not dedupe these four pairs; if a future change genuinely requires consolidation, route the proposal through the `sunfish-api-change` ICM pipeline.**

The four pairs covered by this policy are:

| Pair | Rich (kitchen-sink target) | MVP (catalog leaf) |
|---|---|---|
| Gantt | `Sunfish.Adapters.Blazor.Components.DataDisplay.Gantt.SunfishGantt` | `Sunfish.Adapters.Blazor.Components.Scheduling.SunfishGantt` |
| Scheduler | `Sunfish.Adapters.Blazor.Components.DataDisplay.Scheduler.SunfishScheduler` | `Sunfish.Adapters.Blazor.Components.Scheduling.SunfishScheduler` |
| Spreadsheet | `Sunfish.Adapters.Blazor.Components.DataDisplay.Spreadsheet.SunfishSpreadsheet` | `Sunfish.Adapters.Blazor.Components.Editors.SunfishSpreadsheet` |
| PdfViewer | `Sunfish.Adapters.Blazor.Components.DataDisplay.PdfViewer.SunfishPdfViewer` | `Sunfish.Adapters.Blazor.Components.Media.SunfishPdfViewer` |

### Rules

1. **Both halves of each pair MUST exist.** Bug fixes that touch one half should evaluate whether the other half needs the same fix. Many fixes will need both touches.
2. **Type-name reuse across namespaces IS intentional.** Do not propose disambiguating renames (Option D).
3. **No dedup PR may proceed without an api-change pipeline.** The pipeline must include a migration guide for both consumer paths and explicit sign-off on which path "wins" (or whether they merge into a third superset shape).
4. **Code-quality scanners and batch tools that flag these as duplicates are wrong.** Update the tool's allowlist or add an inline suppression with a citation to this ADR. PR #117 is the canonical example of correcting such a flag.
5. **XML doc comments on each type SHOULD name its sibling.** This is the existing convention (e.g., `Scheduling/SunfishGantt.razor.cs:23-27`). New components added to the dual-namespace family inherit the convention.

### Process if a future change DOES require consolidation

A genuine consolidation request might arise from:

- A v1 surface-area review concluding both shapes can be unified.
- A breaking refactor (e.g., a new base class) where maintaining two parallel implementations is no longer cost-effective.
- A new package boundary (Option E) being introduced for unrelated reasons.

In any such case, follow the `sunfish-api-change` pipeline:

1. **00_intake** — describe the consolidation, identify both consumer paths.
2. **01_discovery** — enumerate all callers under both namespaces, identify migration cost per call site.
3. **02_architecture** — design the unified surface (or the package-split, per Option E).
4. **03_package-design** — define the new public API shape; mark the deprecated path with `[Obsolete]` for one MAJOR cycle before deletion.
5. **05_implementation-plan** — task list including consumer-side migrations.
6. **06_build** — implement.
7. **07_review** — verify both consumer paths still work via the migration shim before deprecation removal.
8. **08_release** — MAJOR version bump; migration guide in release notes.

Skipping this pipeline for an ad-hoc dedup is a process violation.

### What is NOT covered by this ADR

- **Other components that happen to share names across namespaces** for unrelated reasons (e.g., `SunfishButton` in both `Buttons/` and a hypothetical `Forms/Buttons/` subfolder) are NOT automatically covered. Each such case stands on its own merits; this ADR specifically governs the four rich-vs-MVP pairs above.
- **Adapter-level parity** (Blazor ↔ React per [ADR 0014](./0014-adapter-parity-policy.md)) is orthogonal. The React adapter may legitimately have a single shape if its consumer needs are different, OR it may mirror the dual-namespace pattern. That decision is per-adapter and not constrained by this ADR.

---

## Consequences

### Positive

- The pattern has a name and a citable rule. Dedup proposals can be closed in one comment ("see ADR 0041") rather than re-litigating the rationale each time.
- The rich-vs-MVP design intent (per [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md)) is now bidirectionally documented — ADR 0022 establishes the catalog tier; ADR 0041 establishes the implementation pattern that serves it.
- Future subagents that scan for duplicates have an explicit allowlist anchor: "if the type pair appears in ADR 0041's table, don't dedupe."
- The api-change pipeline is named as the legitimate route for the rare case where consolidation IS appropriate, so the door isn't permanently closed — it's just appropriately gated.
- Bug fixes routinely touch both halves of a pair; the convention of "evaluate sibling for the same fix" becomes explicit rather than tribal.

### Negative

- Maintenance cost is real: a bug fix often needs two PRs or one PR touching two locations. Velocity tax is accepted as the cost of the dual-consumer-path design.
- The XML-doc-comments-name-the-sibling convention is informal and easy to miss when adding a new component to the family. No automated enforcement; maintained by code-review.
- New contributors have a moderate learning curve — "why are there two `SunfishGantt`s?" is a first-time question that didn't exist before. Mitigation: this ADR plus the XML doc comments are the answer.
- Code-quality tools (analyzers, batch reports, dedup scanners) need ongoing allowlist maintenance for each new tool that ships. Documented as part of the tool's setup runbook when added.
- If a fifth or sixth component pair joins the family (e.g., a future `SunfishKanban` rich + MVP), this ADR's table needs updating. Lightweight maintenance but not zero.

---

## Revisit triggers

This ADR should be re-opened when **any one** of the following occurs:

1. **A genuine consolidation case is identified** that the api-change pipeline approves. Not "I want to clean up the folder structure" but "the rich variant has absorbed all of the MVP's surface area and now the MVP is dead code." When this happens, follow the process; that PR's release notes should also amend or supersede this ADR.
2. **A fifth pair joins the family.** Update the table; refresh the policy.
3. **A dedup attempt is approved** under the api-change pipeline (not rejected at intake). The pipeline's outcome may justify changing this ADR's posture from "do not dedupe" to "dedupe per the approved migration plan."
4. **Option E (separate-package split) becomes attractive.** When the main adapter package size or dependency footprint creates real consumer pain, the split into `ui-adapters-blazor-rich` (or `ui-adapters-blazor-demos`) becomes worth its refactor cost. Open a follow-up ADR.
5. **Adapter parity ([ADR 0014](./0014-adapter-parity-policy.md)) for the React adapter requires a cross-adapter consolidation.** If React's parity contract is harder to satisfy with two namespaces, the policy may need a per-adapter clause.

---

## References

- **PR that this ADR ratifies:**
  - PR #117 — `docs(a11y): correct cascade-batch report — Gantt/Scheduler not duplicates` — corrected the incorrect "two-location duplicates" flag from the cascade-batch a11y report; aborted the dedup investigation (subagent `aa304586d15820a26`) before destructive action; established the doc-comments-name-the-sibling convention.
- **Related ADRs:**
  - [ADR 0022](./0022-example-catalog-and-docs-taxonomy.md) — the canonical example catalog and Tier 3 scheduling family that establishes the rich-vs-MVP catalog framing this ADR implements at the component level.
  - [ADR 0014](./0014-adapter-parity-policy.md) — adapter parity (Blazor ↔ React); orthogonal to this ADR but relevant when extending the family to React.
- **Files this ADR governs:**
  - `packages/ui-adapters-blazor/Components/DataDisplay/Gantt/` (rich) + `packages/ui-adapters-blazor/Components/Scheduling/SunfishGantt.razor` (MVP).
  - `packages/ui-adapters-blazor/Components/DataDisplay/Scheduler/` (rich) + `packages/ui-adapters-blazor/Components/Scheduling/SunfishScheduler.razor` (MVP).
  - `packages/ui-adapters-blazor/Components/DataDisplay/Spreadsheet/` (rich) + `packages/ui-adapters-blazor/Components/Editors/SunfishSpreadsheet.razor` (MVP).
  - `packages/ui-adapters-blazor/Components/DataDisplay/PdfViewer/` (rich) + `packages/ui-adapters-blazor/Components/Media/SunfishPdfViewer.razor` (MVP).
- **Memory:**
  - User's `project_dual_namespace_components` — the global rule that these four pairs are intentional and should never be deduped without an api-change pipeline. ADR 0041 is the canonical writedown of that memory rule.
- **Process:**
  - [`/icm/pipelines/sunfish-api-change/routing.md`](../../icm/pipelines/sunfish-api-change/routing.md) — the api-change pipeline that any consolidation proposal must follow.
- **Related ADR (process):**
  - [ADR 0042](./0042-subagent-driven-development-for-high-velocity.md) — the subagent-dispatch pattern; PR #117's aborted-dedup case is one of the example "subagent surfaced enough context to halt destructive action" patterns ADR 0042 names.
