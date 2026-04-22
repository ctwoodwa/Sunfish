# Intake Note — Compat Package Expansion (Syncfusion / DevExpress / Infragistics)

**Date:** 2026-04-22
**Requestor:** Christopher Wood (BDFL)
**Request:** Extend the proven `compat-telerik` migration-off-ramp pattern to the three remaining major commercial Blazor vendor libraries — **Syncfusion** (`Sf*`), **DevExpress** (`Dx*`), and **Infragistics Ignite UI** (`Igb*`) — matching the 12-component surface already shipped for Telerik. Resolve four architecture decisions at intake so downstream stages dispatch without blocking.

---

## 1. Request Summary

Ship three new compat packages that each provide a source-shape-compatible migration surface for one commercial Blazor vendor, delegating to canonical Sunfish components (`Sunfish.UIAdapters.Blazor`) under the hood. Target surface per vendor matches the `compat-telerik` baseline: **Button, Icon, CheckBox, TextBox, DropDownList, ComboBox, DatePicker, Form, Grid, Window, Tooltip, Notification** (12 components).

The pattern is already validated by [`packages/compat-telerik`](../../../packages/compat-telerik/) — 12 wrappers, policy-gated under [`POLICY.md`](../../../packages/compat-telerik/POLICY.md), with the audit trail in [`docs/compat-telerik-mapping.md`](../../../docs/compat-telerik-mapping.md). Consumers flip `using Telerik.Blazor.Components` to `using Sunfish.Compat.Telerik` and keep markup intact. This workstream replicates that surface for the other three vendors.

---

## 2. Motivation

Customers migrating from **Syncfusion**, **DevExpress**, or **Infragistics Ignite UI** currently have no supported on-ramp to Sunfish — only Telerik migrators benefit from the existing compat shim. Together, the four named vendors dominate the commercial Blazor component market; closing the remaining three gates turns "Sunfish is a viable replacement target" from a Telerik-only claim into a credible multi-vendor claim.

Three signals make now the right moment:

1. **compat-telerik has proven the pattern is viable.** 12 wrappers are shipping, the policy gate works, and the divergence log format has held up under review. The cost of replicating the shape is now known, not speculative.
2. **Style-parity workstream is landing.** Phase 2+3 of the P0 style-parity remediation (see memory `project_style_audit_synthesis_2026_04.md`) closes the visual-fidelity confidence gap. Once components look right, the migration story can credibly claim "flip your usings and your app keeps working."
3. **Compat-telerik known gaps are a shared blocker.** `GridColumn`, `ValidationSummary`, `ValidationMessage`, and vendor `EventArgs` types are unresolved on compat-telerik today. These shapes recur across all four vendors. Solving them generically now is cheaper than re-solving three more times.

---

## 3. Affected Sunfish Areas

Impact markers are approximate — Stage 01 Discovery will refine.

| Area | Impact | Note |
|---|---|---|
| `packages/compat-syncfusion` (or `packages/compat-commercial/Syncfusion/`) | **new** | Syncfusion `Sf*` wrappers. Shape TBD by Decision 1. |
| `packages/compat-devexpress` (or `packages/compat-commercial/DevExpress/`) | **new** | DevExpress `Dx*` wrappers. Shape TBD by Decision 1. |
| `packages/compat-infragistics` (or `packages/compat-commercial/Infragistics/`) | **new** | Ignite UI `Igb*` wrappers. Web Components-backed; architecture TBD by Decision 2. |
| `packages/compat-telerik` | **affected** | Gap-closure prerequisite (Decision 4): `TelerikGridColumn`, `TelerikValidationSummary`, `TelerikValidationMessage`, EventArgs shims land here first so the generic solution is validated against the existing baseline before replication. |
| `packages/ui-adapters-blazor` | **possible** | New wrappers may surface missing delegation targets (e.g. Syncfusion has a `SfChip` and `SfBadge` that Sunfish may need; scope decision deferred to Stage 01). Intent: wrappers delegate only; no new adapter components are scaffolded as part of this workstream. |
| `packages/foundation` / `packages/ui-core` | **not affected** | No new contracts. Vendor parameter mappings are translation concerns, not contract concerns. |
| `tooling/scaffolding-cli` | **possible** | A Roslyn analyzer (Decision 3) likely ships adjacent to the compat packages rather than inside the CLI. Exact home TBD at Stage 02 Architecture. |
| `apps/kitchen-sink` | **affected** | Migration demo page per vendor — "here's Syncfusion markup, here it is under compat-syncfusion, here's what migration actually looked like." |
| `apps/docs` | **affected** | New mapping docs: `docs/compat-syncfusion-mapping.md`, `docs/compat-devexpress-mapping.md`, `docs/compat-infragistics-mapping.md` (paths depend on Decision 1). |
| `Sunfish.slnx` | **affected** | 3 new projects + 3 new test projects (or 1 + 1 if umbrella wins Decision 1). |
| `Directory.Packages.props` | **affected** | Roslyn analyzer scaffolding pulls in `Microsoft.CodeAnalysis.*` packages (Decision 3). **No** vendor NuGet references per compat-telerik's [POLICY.md §Hard Invariant 1](../../../packages/compat-telerik/POLICY.md) ("No Telerik NuGet dependency") — extended to all three new vendors. |

---

## 4. Selected Pipeline Variant

- [x] **`sunfish-feature-change`** — three new first-class packages are the primary deliverable. Default variant.

See [`icm/pipelines/sunfish-feature-change/README.md`](../../pipelines/sunfish-feature-change/README.md) and [`routing.md`](../../pipelines/sunfish-feature-change/routing.md). Each new vendor package is a feature in the "new adapter support / extending the scaffold CLI with new templates" category listed in that README.

**Not `sunfish-api-change`** — no existing public contracts break. Three new packages + one closing gap-patch to `compat-telerik`; all compat-telerik gap-closure work is additive (new wrappers for `GridColumn`, `ValidationSummary`, etc.; existing wrappers' parameter maps unchanged).

**Not `sunfish-gap-analysis`** — the capabilities are already scoped. `sunfish-gap-analysis` would be right if we were *finding* which vendors to cover; we already know. Stage 01 Discovery will nonetheless do per-vendor API-surface inventory, which has gap-analysis shape — flag that as a mixed-variant moment at 01 if needed (see `ui-architecture-migration-intake-2026-04-20.md` for prior art on mixed-variant routing).

**Not `sunfish-scaffolding`** — the Roslyn analyzer (Decision 3) is adjacent tooling, not the primary deliverable. If Decision 3 flips to "analyzer first," this reclassifies.

---

## 5. Four Open Architecture Decisions

Each decision is framed: **Question → Options → Recommendation → Rationale → Implication if reversed later.**

### Decision 1 — Package layout: per-vendor vs umbrella

**Question:** Ship three separate packages (`packages/compat-syncfusion`, `packages/compat-devexpress`, `packages/compat-infragistics`), or one umbrella (`packages/compat-commercial/` with per-vendor subfolders and one `.csproj`)?

**Options:**
- **A (per-vendor packages).** Each vendor gets its own `.csproj`, its own NuGet, its own CODEOWNERS, its own mapping doc.
- **B (umbrella package).** One `Sunfish.Compat.Commercial.csproj` contains Syncfusion, DevExpress, and Infragistics folders; consumers reference one package and `using` the vendor they care about.

**Recommendation:** **A — per-vendor packages.**

**Rationale:**
- Each vendor wrapper set is logically independent; a Syncfusion migrator does not want DevExpress types clogging IntelliSense.
- Dependency closure is clean: if we later need a vendor-specific NuGet for introspection tooling (non-runtime, see [POLICY.md §Hard Invariant 1](../../../packages/compat-telerik/POLICY.md)), it is scoped to one package.
- Mirrors the existing `packages/compat-telerik` shape — consistency with the prior art.
- Mapping docs stay distinct: `docs/compat-syncfusion-mapping.md`, `docs/compat-devexpress-mapping.md`, `docs/compat-infragistics-mapping.md`.
- Policy gate (CODEOWNERS approval per change) remains scoped and reviewable per vendor.

**Implication if reversed later:** Moving from per-vendor to umbrella is a one-way door for consumers — their `using Sunfish.Compat.Syncfusion` becomes `using Sunfish.Compat.Commercial.Syncfusion` — which is a consumer-visible breaking change. Keep optionality: start per-vendor, and only consolidate if maintenance cost actually grows linearly (which compat-telerik evidence does not suggest).

---

### Decision 2 — Ignite UI architecture handling: spike first vs scaffold and discover

**Question:** Infragistics Ignite UI Blazor wraps Web Components, not native Blazor components. The JS-interop boundary differs from Telerik, Syncfusion, and DevExpress (all native Blazor). Do we run a spike before scaffolding `compat-infragistics`, or scaffold and discover the friction?

**Options:**
- **A (spike first).** 1-day time-boxed Stage 01 sub-task: attempt a `Sunfish.Compat.Infragistics.IgbButton` wrapper that wraps `<IgbButton>` WC-backed behavior, determine whether the shim pattern (wrapper delegates to `SunfishButton`) still works, or whether WC encapsulation forces a different approach (e.g. rendering the WC tag directly from the wrapper, losing delegation).
- **B (scaffold and discover).** Scaffold all 12 Infragistics wrappers against the same template used for Telerik/Syncfusion/DevExpress; fix what breaks.

**Recommendation:** **A — 1-day spike first.**

**Rationale:**
- The WC boundary is a materially different architecture from the other three vendors. If the shim pattern does not map, discovering that after scaffolding 12 wrappers is 12× the rework of discovering it once.
- The spike has a clear binary outcome: either the existing delegation-to-canonical-Sunfish pattern works under WC wrapping, or it does not and we need a different shape (e.g. `IgbButton` wrapper that preserves the WC tag and forwards parameters via JS interop, without delegating to `SunfishButton`).
- A 1-day spike cost is tiny compared to the 12-component scaffolding cost.

**Implication if reversed later:** If we scaffold and discover halfway through that the pattern does not fit, we throw away the scaffolding and redo. Spike cost is bounded (1 day). Scaffold-and-discover cost is unbounded (potentially 12 wrappers of rework + a third shim pattern invented under schedule pressure).

---

### Decision 3 — Roslyn analyzer sequencing: before or after first new vendor lands

**Question:** A cross-cutting "vendor-usings-flagger" Roslyn analyzer (detects `using Telerik.Blazor.Components`, `using Syncfusion.Blazor.*`, `using DevExpress.Blazor`, `using IgniteUI.Blazor.*` and suggests the compat replacement) is a known gap even for compat-telerik today — it has never been built. Do we build it first, or after the first new vendor (likely compat-syncfusion) lands?

**Options:**
- **A (analyzer first).** Build the analyzer against the two known namespaces today (`Telerik.Blazor.*`, and speculatively `Syncfusion.Blazor.*`), ship it, then scaffold compat-syncfusion against the analyzer's detection patterns.
- **B (analyzer after first new vendor).** Scaffold compat-syncfusion first (most similar in shape to Telerik). With two concrete data points (compat-telerik + compat-syncfusion), design the analyzer's pattern detection with less speculation.

**Recommendation:** **B — analyzer after compat-syncfusion lands.**

**Rationale:**
- Pattern detection benefits from data points. One vendor (Telerik) is a sample size of 1; designing a "vendor usings" detector against a single example invites over-fitting or under-fitting.
- The analyzer's value is cross-vendor. Shipping it with only Telerik support provides a fraction of the value; shipping it with two vendors (Telerik + Syncfusion) reveals the *shape* of the rule, which will carry to DevExpress and Infragistics.
- compat-syncfusion is the most similar to compat-telerik in shape (both native Blazor, both use `<Component Parameter="value">` markup). Landing it first validates the replication pattern without introducing WC complexity (Decision 2) or new architecture questions.
- Consumers are not currently blocked on the analyzer — migration today works with manual using-swap and compiler errors pointing the way. The analyzer is ergonomic polish, not correctness-critical.

**Implication if reversed later:** If we land the analyzer first and the Syncfusion namespaces differ enough to require significant refactoring of the detection rules, we spend analyzer-building time twice. The recommended order avoids that.

---

### Decision 4 — Generic child-component shimming: generically first vs per-vendor

**Question:** `TelerikGridColumn` child-markup, `TelerikValidationSummary`, `TelerikValidationMessage`, and Telerik `EventArgs` types (e.g. `GridRowClickEventArgs`) are known unresolved gaps in compat-telerik. These same shapes recur in Syncfusion (`SfGrid` has `GridColumn` children + `GridRowClickEventArgs`), DevExpress (`DxGrid` same shape), and Infragistics (`IgbGrid` same shape). Do we solve these generically against compat-telerik first (as a prerequisite to this workstream), or per-vendor inside each new compat package?

**Options:**
- **A (generic first, prerequisite).** Close the four compat-telerik gaps (GridColumn child, ValidationSummary, ValidationMessage, EventArgs) as a prerequisite to this workstream. The solution shapes become templates for the new vendor packages.
- **B (per-vendor).** Solve these inside each of the three new compat packages, with an implicit expectation to backport the pattern to compat-telerik afterwards.

**Recommendation:** **A — solve generically first, as a prerequisite.**

**Rationale:**
- These shapes are shared across all four vendors. Solving per-vendor triplicates the work and risks three slightly-different solutions to the same problem, making future maintenance harder.
- compat-telerik is the pattern-reference. If it has unresolved gaps, the new compat packages will replicate those gaps. Better to raise the pattern bar once.
- Solving these against compat-telerik validates the generic approach against a working baseline before replicating.
- The gap-closure work is bounded and well-scoped (four known shapes: GridColumn child, ValidationSummary, ValidationMessage, EventArgs types). It is not an open-ended research project.

**Implication if reversed later:** If we launch the new vendors first and solve gaps per-vendor, we will have three slightly-different GridColumn shim designs before compat-telerik is updated — and any later attempt to unify them will be a breaking change for all three.

---

## 6. Prerequisites (must land BEFORE new vendor packages)

Derived from Decisions 2 and 4:

1. **compat-telerik gap closure (Decision 4).** Land generic shims for:
   - `TelerikGridColumn` child-markup
   - `TelerikValidationSummary` / `TelerikValidationMessage`
   - Telerik `EventArgs` types (`GridRowClickEventArgs`, etc.)
   Each lands under existing compat-telerik [POLICY.md](../../../packages/compat-telerik/POLICY.md) policy gate (one wrapper per PR, CODEOWNER approval, mapping doc update same PR).

2. **Ignite UI Web Components architecture spike (Decision 2).** 1-day time-boxed investigation during Stage 01 Discovery. Binary outcome: "delegation pattern works under WC wrapping" or "different shim pattern required." If the latter, Stage 02 Architecture explicitly designs the alternative before compat-infragistics scaffolding starts.

---

## 7. Out of Scope

Explicitly declared to prevent scope creep at Stage 01:

- **Vendors without a commercial Blazor library.** Material UI Blazor (community project, not commercial), MudBlazor (MIT, not commercial), Blazorise (dual-license but not commercial SKU-gated like the four named vendors), Radzen Blazor (free, not a migration concern). If a vendor doesn't charge for Blazor components, migration off their stack is not a commercial priority this workstream addresses.
- **UI-only commercial component libraries not primarily targeting Blazor.** Example: Kendo UI (Telerik's JS stack) has a Blazor presence but the primary market is JS; we cover the Blazor subset only, and it overlaps with the Telerik Blazor namespace already covered by compat-telerik.
- **Trial / student / non-production SKUs of vendor libraries.** Compat must work against production vendor SKUs — trial/student editions may have API surface differences we do not commit to track.
- **Visual / behavioral parity with the vendor libraries.** Per compat-telerik [POLICY.md](../../../packages/compat-telerik/POLICY.md) ("does NOT provide visual or behavioral parity"), this workstream ships source-code shape parity only. Visual parity is the separate style-parity workstream.
- **Non-Blazor adapter coverage.** React and Web Components adapters are on the ADR 0017 roadmap but orthogonal to the compat surface, which is Blazor-only.

---

## 8. Risk and Assumption Log

| # | Assumption | Validate by | Impact if wrong |
|---|---|---|---|
| 1 | Per-vendor API surface allows a uniform 12-component target (Button, Icon, CheckBox, TextBox, DropDownList, ComboBox, DatePicker, Form, Grid, Window, Tooltip, Notification). | Stage 01 Discovery: per-vendor doc search against each vendor's component catalog. | Some vendors may not have a 1:1 match for every target component (e.g. "Window" may map to `SfDialog` vs `DxPopup` vs `IgbDialog` — all roughly dialog-shaped but not identically named). Impact: mapping doc grows "divergence" sections; 12-count target may slip by 1–2 for a given vendor. Not a blocker; reduce target per-vendor if needed. |
| 2 | The shim pattern (wrapper delegates to canonical Sunfish component, passes parameters through, LogAndFallback on unrecognized values) is architecture-agnostic. | Decision 2 spike against Infragistics. | If the WC boundary forces a fundamentally different shim pattern, compat-infragistics scope expands and may need its own POLICY.md section. Decision 2 catches this. |
| 3 | Vendor license terms permit shipping a compat shim that *does not* reference their NuGet but names their types and parameters in source. | Legal / license review at Stage 02 Architecture (spawn `sunfish-api-change` if blocking). | compat-telerik has precedent (no Telerik NuGet reference, type names match); other vendors' license terms may differ. Impact: if any vendor's license prohibits the pattern, that vendor's compat package is descoped. Low probability based on compat-telerik's precedent holding for ~6 months. |
| 4 | Roslyn analyzer design is pattern-stable once two vendors are implemented (Decision 3 rationale). | After compat-syncfusion lands, prototype the analyzer against Telerik + Syncfusion namespaces. | If two data points are still insufficient and the analyzer needs to be redesigned after DevExpress lands too, analyzer delivery slips. Manageable — analyzer is ergonomics, not correctness. |
| 5 | Compat-telerik's known gaps (Decision 4) can be solved generically rather than per-vendor. | Stage 02 Architecture: design the generic solution (e.g. a shared `CompatChildComponent<TParent>` base). | If each vendor's child-component shape is genuinely different (unlikely — Grid-with-Columns is a near-universal idiom), the work triples. Stage 01 Discovery validates shape overlap. |
| 6 | Style-parity workstream lands before this one starts, making the migration story credible at release time. | Check that [style-audit synthesis tracker](../07_review/output/style-audits/SYNTHESIS.md) remediation is complete (see memory `project_style_audit_synthesis_2026_04.md`). | If style-parity is incomplete, the compat-expansion release message weakens ("flip your usings, but expect visual differences"). Not a blocker but affects release notes framing. |

---

## 9. Acceptance Gate (Intake → Discovery)

Before Stage 01 Discovery dispatches agents against the three vendors' documentation, the four decisions in §5 must be explicitly approved (or redirected) by the BDFL. The decisions gate:

- Whether Stage 01 scaffolds three intake files or one (Decision 1).
- Whether Stage 01 includes an Infragistics architecture spike (Decision 2).
- Whether Stage 01 researches Roslyn analyzer requirements in depth or defers (Decision 3).
- Whether Stage 02 Architecture plans compat-telerik gap closure as a prerequisite milestone (Decision 4).

**Sign-off format:** BDFL approves §5 decisions en bloc (or redirects specific decisions with rationale). On approval, Stage 01 Discovery dispatches.

---

## 10. Discovery Doc Sources (per vendor)

Stage 01 will consult these canonical sources:

**Syncfusion:**
- `https://blazor.syncfusion.com/documentation/introduction` — component overview
- `https://help.syncfusion.com/cr/blazor/Syncfusion.Blazor.html` — API reference
- `https://blazor.syncfusion.com/demos/` — demo gallery (for parameter-pattern discovery)

**DevExpress:**
- `https://docs.devexpress.com/Blazor/400725/blazor-components` — component catalog
- `https://demos.devexpress.com/blazor/` — demo gallery

**Ignite UI (Infragistics):**
- `https://www.infragistics.com/products/ignite-ui-blazor` — product overview
- `https://github.com/IgniteUI/igniteui-blazor` — source (reveals WC wrapping architecture for Decision 2 spike)
- `https://www.infragistics.com/products/ignite-ui-blazor/blazor/components/general-getting-started-blazor-web-app` — getting started (WC interop boundary)

---

## 11. Task IDs (existing in TaskList)

- **#101 (parent)** — Compat package expansion workstream
- **#102 (this doc)** — Intake
- **#103** — Stage 01 Discovery
- **#104** — compat-telerik gap closure (Decision 4 prerequisite; GridColumn / ValidationSummary / ValidationMessage / EventArgs shims)
- **#105** — Roslyn vendor-usings-flagger analyzer (after compat-syncfusion lands per Decision 3)
- **#106** — compat-syncfusion package (first, most similar to compat-telerik)
- **#107** — compat-devexpress package
- **#108** — compat-infragistics package (requires Decision 2 spike outcome first)

---

## Cross-References

- [`packages/compat-telerik/POLICY.md`](../../../packages/compat-telerik/POLICY.md) — pattern-reference policy gate; extended unmodified to new vendors.
- [`docs/compat-telerik-mapping.md`](../../../docs/compat-telerik-mapping.md) — audit-trail format; replicated per new vendor.
- [`icm/pipelines/sunfish-feature-change/README.md`](../../pipelines/sunfish-feature-change/README.md) — selected variant.
- [`icm/pipelines/sunfish-feature-change/routing.md`](../../pipelines/sunfish-feature-change/routing.md) — stage routing used here.
- [`icm/00_intake/output/p1-blocks-intake-2026-04-20.md`](p1-blocks-intake-2026-04-20.md) — format reference.
- Memory: `project_compat_expansion_workstream.md` — workstream scope pointer.
- Memory: `project_style_audit_synthesis_2026_04.md` — style-parity workstream this one queues behind.
- `.claude/rules/universal-planning.md` — UPF principles apply to Stage 02 Architecture (four-decision framing above is a UPF "Assumption → Validate → Impact" shape).

---

## Next Steps

On BDFL approval of §5 decisions:

1. **Prerequisite milestone** — close compat-telerik gaps per Decision 4 (one PR per shim under existing compat-telerik policy gate; output lands in `packages/compat-telerik/` + `docs/compat-telerik-mapping.md`).
2. **Stage 01 Discovery** — dispatch three parallel doc-ingest agents (Syncfusion, DevExpress, Infragistics) using sources in §10. Include the Decision 2 Infragistics WC spike as a Stage 01 sub-task. Output: per-vendor component-surface inventory + mapping-doc skeletons + spike outcome.
3. **Stage 02 Architecture** — design the per-vendor wrapper shape, parameter-mapping approach, and (if WC spike requires it) the Infragistics-specific shim pattern.
4. **Stages 03–08** — follow `sunfish-feature-change` routing per-vendor, in order: compat-syncfusion → compat-devexpress → compat-infragistics. Roslyn analyzer (task #105) dispatches after compat-syncfusion lands.
