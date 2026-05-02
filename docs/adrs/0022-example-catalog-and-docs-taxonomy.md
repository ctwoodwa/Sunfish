---
id: 22
title: Canonical Example Catalog, Documentation Taxonomy, and the Demo-Page Panel
status: Accepted
date: 2026-04-21
tier: foundation
concern:
  - dev-experience
composes:
  - 14
  - 17
  - 21
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0022 — Canonical Example Catalog, Documentation Taxonomy, and the Demo-Page Panel

**Status:** Accepted
**Date:** 2026-04-21
**Resolves:** Establish a single canonical shape for Sunfish demos and docs — a shared example catalog that every provider library (kitchen-sink, compat-telerik, future React/WC demo hosts, future third-party providers) implements; a taxonomy for the six kinds of documentation Sunfish ships; and the interactive demo-page panel every component demo uses. Today's kitchen-sink has 122 component folders of uneven depth authored against an ad-hoc `DemoSection` / `PageSection` convention. This ADR sets the target shape so fan-out work has one template to aim at.

---

## Context

The kitchen-sink app (`apps/kitchen-sink/`) is already Sunfish's primary component showcase, with ~122 component folders and an existing lightweight convention (`DemoSection` + `Code=` string + `AccessibilityInfo`). That convention works but falls short of the verbose, interactive standard consumers expect from mature component libraries (Telerik, Syncfusion, MudBlazor). The BDFL's stated preference is **Telerik-verbose depth** — richer narratives, live theme switching in-page, multi-file source viewing, copy-to-clipboard, "edit on GitHub" affordances, and a visible parity contract for what a complete demo catalog should contain.

Three separate problems compound:

1. **No shared catalog.** There is no single source of truth listing every component × feature demo that *should* exist. Today's 122 folders are the de facto list; aspirational components (AIPrompt, ChatUI, Heatmap, Sankey, PromptBox, SpeechToTextButton, SmartPasteButton) have no placeholder and their absence is invisible in the UI.
2. **No parity contract for providers.** `compat-telerik` is a separate provider library with its own demo surface; a future `ui-adapters-react` provider adapter and the `ui-components-web` consumption track ([ADR 0017 revised 2026-04-21](0017-web-components-lit-technical-basis.md)) will each want a demo host. Without a shared catalog, each provider drifts on what it showcases.
3. **Documentation surface is uneven.** Components have demos but no `apps/docs/` narrative pages. Blocks (`packages/blocks-*`) have READMEs but no rendered documentation. Non-UI libraries named in [ADR 0021](0021-reporting-pipeline-policy.md) (Spread Processing, Words Processing, Zip Library, reporting pipeline adapters) have no documentation home at all. Foundation contracts and accelerator guides are scattered.

The migration this ADR guides is not a ground-up rewrite — existing kitchen-sink demos continue to work and upgrade in-place. It is a **shape change** plus **coverage fan-out**.

---

## Decision

### 1. Canonical example catalog as parity contract

A single YAML artifact at `_shared/product/example-catalog.yaml` enumerates every demo node that *should* exist across all provider libraries. This becomes a parity contract — the sibling of [ADR 0014](0014-adapter-parity-policy.md)'s adapter-parity policy, but for demos rather than rendering.

```yaml
version: 1

components:
  editors:
    autocomplete:
      status: sunfish-implemented        # sunfish-implemented | sunfish-partial | aspirational
      sunfish-component: SunfishAutoComplete
      telerik-equivalent: TelerikAutoComplete
      examples:
        - overview
        - virtualization
        - templates
        - grouping
        - events
        - validation
        - keyboard-navigation
        - rtl

  smart-ai:
    aiprompt:
      status: aspirational
      sunfish-component: null
      telerik-equivalent: TelerikAIPrompt
      examples:
        - overview
        - templates
        - commands
        - events
        - localization
        - keyboard-navigation

non-ui-libraries:
  spread-processing:
    status: aspirational
    sunfish-package: null
    telerik-equivalent: Telerik.Documents.Spreadsheet
    docs:
      - overview
      - formulas
      - styling
      - templates
      - import-export

blocks:
  leases:
    status: sunfish-implemented
    sunfish-package: Sunfish.Blocks.Leases
    docs:
      - overview
      - entity-model
      - service-contract
      - demo-lease-list

accelerators:
  bridge:
    status: sunfish-implemented
    docs:
      - overview
      - shell-model
      - bundle-provisioning
      - tenant-admin
```

**Status values:**

- `sunfish-implemented` — component/package exists in Sunfish; demo/docs required.
- `sunfish-partial` — component exists but not all catalog examples apply (e.g. no RTL support yet).
- `aspirational` — catalog reserves the node; no Sunfish component/package yet. Renders as placeholder.

Every provider library MUST list its coverage against this catalog. A missing-but-sunfish-implemented node is a bug against this ADR. An `aspirational` node renders a placeholder page pointing at the tracked intake or upstream parity reference.

### 2. Documentation taxonomy

Sunfish ships six distinct kinds of docs, each with its own home and conventions:

| Type | Home | Shape |
|---|---|---|
| **Component demos** | `apps/kitchen-sink/Pages/Components/<family>/<component>/<feature>/` | Live demo + narrative + multi-file source viewer + API link |
| **Block guides** | `apps/docs/blocks/<block>/` | Purpose + contract + DI wiring + live example (DocFX) |
| **Non-UI library guides** | `apps/docs/libraries/<library>/` | How-to prose + code snippets + integration patterns (DocFX) |
| **Accelerator guides** | `apps/docs/accelerators/<accelerator>/` | When to use + what you get + how to start (DocFX) |
| **Foundation / contract reference** | `apps/docs/foundation/` + DocFX auto-gen | ADR links + contract shape + consumer examples |
| **Getting started / tutorials** | `apps/docs/getting-started/` | Path-based onboarding (DocFX) |

Provider-library hosts (kitchen-sink for Sunfish; compat-telerik's demo; future React demo; future WC demo) are responsible only for the **component demos** row. The other five live in `apps/docs/` and are framework-agnostic narrative documentation authored once.

### 3. Demo page shape — `SunfishExamplePanel`

Every component-demo leaf renders through a single `SunfishExamplePanel` component that lives in `ui-adapters-blazor` so all provider libraries reuse it. The panel specification is directly inspired by Telerik's Blazor demos site; every element below maps to a Telerik feature or an explicit Sunfish-specific adaptation.

**Panel layout:**

```
┌─────────────────────────────────────────────────────────────────────────┐
│ Breadcrumb: Components > Editors > AutoComplete > Virtualization        │
├─────────────────────────────────────────────────────────────────────────┤
│ # AutoComplete / Virtualization                                         │
│ Short narrative paragraph — what this demo shows, when to use it.       │
├─────────────────────────────────────────────────────────────────────────┤
│ [ EXAMPLE ] [ VIEW SOURCE ] [ EDIT ON GITHUB ↗ ]  [ COPY ]  Theme [ ◐ ] │
│                                                          [Provider ▼ ]  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│   (EXAMPLE tab, default): live rendered component, dynamic height,      │
│   optional <Configurator> sub-toolbar for runtime prop changes.         │
│                                                                         │
│   (VIEW SOURCE tab): horizontal sub-tabs — Demo.razor, *.cs files;      │
│   syntax-highlighted (ColorCode) dark-background code viewer; COPY      │
│   button active in this tab.                                            │
│                                                                         │
├─────────────────────────────────────────────────────────────────────────┤
│ Style with design tokens →  (footer: link to tokens-guidelines.md)      │
└─────────────────────────────────────────────────────────────────────────┘
```

**Toolbar controls, left → right:**

- **EXAMPLE tab** — default active; renders the live demo in a sandboxed content area.
- **VIEW SOURCE tab** — swaps the content area for the multi-file source viewer.
- **EDIT ON GITHUB ↗** — opens the primary `.razor` file on GitHub in a new tab. Zero infra, one-click to clone and run. Replaces Telerik's "EDIT IN TELERIK REPL" (decision D1 in the companion planning turn).
- **COPY** — copies the current source tab's contents to the clipboard. Always visible on VIEW SOURCE tab; hidden on EXAMPLE tab. The second half of decision D1.
- **Theme** (right-aligned) — dark/light toggle wired to `ISunfishThemeService`. Persists per-session via localStorage.
- **Provider selector** (right-aligned, ▼) — Bootstrap / FluentUI / Material. Invokes `ProviderSwitcher`. Per decision D3, providers are the *primary* selector; palette variants within a provider are aspirational (placeholder-only for now).

**Footer bar** — single link: `Style with design tokens →` pointing at `_shared/design/tokens-guidelines.md` (decision D2). Replaces Telerik's "Style in ThemeBuilder" affordance with the actual-today answer — tokens are the styling surface.

**Feedback widget** — deferred until public repo release. Placeholder hooks stay in the panel but render nothing until a GitHub Issues integration or equivalent lands.

**Aspirational nodes** render a distinct placeholder panel:

```
┌─────────────────────────────────────────────────────────────────────────┐
│ Breadcrumb: Components > Smart AI > AIPrompt > Overview                 │
├─────────────────────────────────────────────────────────────────────────┤
│ # AIPrompt / Overview  [ ASPIRATIONAL ]                                 │
│                                                                         │
│ Sunfish component: not yet built                                        │
│ Tracked under: (intake doc link, if one exists)                         │
│ Parity reference: Telerik AIPrompt — https://demos.telerik.com/...      │
│                                                                         │
│ When this lands, the demo will show: ...                                │
└─────────────────────────────────────────────────────────────────────────┘
```

### 4. Per-demo folder convention

Each leaf demo is a folder under `apps/kitchen-sink/Pages/Components/<family>/<component>/<feature>/` containing:

```
Demo.razor                ← primary @page directive + live example + @code
ProductService.cs         ← optional companion service
ProductDto.cs             ← optional companion DTO
Narrative.md              ← optional prose shown above the panel
```

A build-time MSBuild task scans each demo folder and emits a `Sources.g.cs` partial class that embeds the file contents as static data, consumed by `SunfishExamplePanel` without runtime file I/O. Authors never register files manually — drop a companion file in the folder, it appears as a VIEW SOURCE sub-tab.

Legacy demos already authored in the flat `<Component>/Overview.razor` shape (today's convention) continue to work. Migration to the folder convention is opportunistic — a demo upgrades when it's next edited or when its feature gets deepened.

### 5. Routing convention

`/components/<family>/<component>/<feature>` replaces today's `/components/<component>/overview` pattern. Example routes:

- `/components/editors/autocomplete/overview`
- `/components/editors/autocomplete/virtualization`
- `/components/data-display/grid/columns-frozen`
- `/components/smart-ai/aiprompt/overview` (aspirational placeholder)

Legacy routes under `/components/<component>` or `/components/<component>/overview` stay as redirects to the new `/components/<family>/<component>/overview` shape until the full migration lands.

### 6. Provider-library parity responsibility

Every Sunfish-hosted provider library (starting with `compat-telerik`; later `ui-adapters-react` and `ui-components-web` per [ADR 0017](0017-web-components-lit-technical-basis.md)) is responsible for its own coverage against the catalog. A CI check compares each provider's demo page list to the catalog and reports missing nodes; third-party provider libraries (future community providers) may opt into the same check via a shared GitHub Action.

Parity is **coverage, not pixel-equivalence** at this layer. Pixel parity is ADR 0014's territory; catalog parity is about "does a demo page exist for this node in this provider library."

---

## Consequences

### Positive

- **Coverage becomes visible.** Aspirational placeholder pages surface missing components in the UI itself, not in a stale roadmap doc.
- **Every demo looks like Telerik-verbose.** The panel template enforces narrative + live + source + theme controls without per-demo work.
- **Providers have a shared contract.** Compat-telerik, future React, future WC demo hosts implement the same tree. Drift is detectable in CI.
- **Docs taxonomy is clear.** A block gets a `/docs/blocks/` page. A non-UI library gets `/docs/libraries/`. A component demo stays in kitchen-sink. Contributors know where to put what.
- **Existing demos upgrade opportunistically.** No mass rewrite. Demos move to the folder convention when they're next edited or deepened.

### Negative

- **Catalog drift risk.** If the catalog isn't tended, status tags lag reality. Mitigated by making the catalog the CI gate — provider libraries reference it, so drift surfaces as a missing-demo error.
- **SunfishExamplePanel is load-bearing.** The whole kitchen-sink rides on this one component once fan-out starts. Changes ripple across every demo page; versioning the panel is important.
- **Aspirational placeholders pile up.** Your list includes ~30+ aspirational components. The UI will show a lot of "not yet built" pages until backfill lands. This is honest but may read as vaporware — offset by linking each placeholder to its tracked intake when one exists.
- **MSBuild source-file generator adds build complexity.** A simple walk of each demo folder isn't hard, but it's another piece of the build pipeline. Offset by zero runtime cost and zero authoring ceremony.
- **~920 leaf pages total when complete.** The fan-out is months of parallel-agent work. Realistic expectation: overview pages for every sunfish-implemented component land first (weeks), feature depth fills over months.

### Rejected alternatives

- **Auto-generate docs from XML doc comments alone.** Rejected — XML docs give API reference, not narrative demos. DocFX auto-gen covers the API-reference *slot* in the taxonomy (row 5), not the component-demo row.
- **Markdown-only catalog file.** Rejected — the catalog needs machine-readable structure for the CI parity check. YAML reads well enough for humans and parses trivially.
- **Per-component catalog files** (one file per component). Rejected — harder to review coverage holistically and easier to miss adding a new node than in a single file.
- **Panel component lives in `kitchen-sink-shared/` instead of `ui-adapters-blazor`.** Rejected — putting the panel in `ui-adapters-blazor` lets compat-telerik's demo host and any future provider-library demo host reuse it. The panel is a general Sunfish component, not a kitchen-sink-local widget.

---

## Implementation sequencing

1. **Land this ADR + the catalog skeleton** (`_shared/product/example-catalog.yaml`). The catalog starts empty — Phase 1 inventory fills it.
2. **Phase 1 — Inventory** via parallel subagents:
   - Existing kitchen-sink coverage → status: `sunfish-implemented`.
   - `packages/ui-adapters-blazor/Components/**/*.razor` → cross-check against kitchen-sink coverage; flag gaps.
   - `packages/blocks-*` → blocks catalog.
   - User's Telerik-shaped list → aspirational nodes + component-family classification.
3. **Phase 2 — Build `SunfishExamplePanel`** in `ui-adapters-blazor` (basic tabs + theme toggle + provider switcher + narrative slot, sources as static `[Parameter]` for the first pass). ColorCode for syntax highlighting.
4. **Phase 3 — Build the per-demo-folder source generator** (MSBuild task or T4 template — whichever is simplest). Picks up any file in the demo folder and embeds contents.
5. **Phase 4 — Upgrade Button/Overview as the proof-point demo.** First folder-convention demo, exercises the full panel end-to-end.
6. **Phase 5 — Establish routing convention + landing page navigation.** The new URL shape + sidebar tree rendered from the catalog.
7. **Phase 6 — Wave 1 fan-out:** `Overview` pages for every `sunfish-implemented` component, one subagent per family. Legacy single-file demos migrate to folder convention in this pass.
8. **Phase 7 — Aspirational placeholder pages** generated as a single batch, one per `aspirational` catalog node.
9. **Phase 8+ — Feature-depth waves** (Events, Validation, Templates, Keyboard Navigation, etc.) across components, long-tail.
10. **Phase 9 — Block docs** (`apps/docs/blocks/`), non-UI library docs (`apps/docs/libraries/`), accelerator docs (`apps/docs/accelerators/`). Separate track, parallel to component fan-out.

---

## Related ADRs

- [ADR 0014](0014-adapter-parity-policy.md) — Adapter Parity Policy. This ADR's catalog is to demos what 0014 is to rendering.
- [ADR 0017 (revised 2026-04-21)](0017-web-components-lit-technical-basis.md) — Spec-First UI Contracts. Future React and WC provider-library demo hosts will implement against this same catalog.
- [ADR 0021](0021-reporting-pipeline-policy.md) — Reporting Pipeline Policy. The non-UI libraries on the user's list (Spread Processing, Words Processing, Zip Library) fit this ADR's contract-and-adapter model; their docs pages cite back to ADR 0021.

## References

- Telerik UI for Blazor Demos — [demos.telerik.com/blazor-ui](https://demos.telerik.com/blazor-ui/) — the verbosity reference this ADR targets.
- [ColorCode](https://github.com/CommunityToolkit/ColorCode-Universal) — .NET-native syntax highlighter selected for the VIEW SOURCE viewer (zero JS interop).
- [`_shared/design/tokens-guidelines.md`](../../_shared/design/tokens-guidelines.md) — the "Style with design tokens" footer target (decision D2).
