# Phase 3b: Blazor Component Category Migration — Implementation Plan (as shipped)

> **Status:** ✅ COMPLETE. Shipped on `feat/migration-phase3b-blazor-components`. 45 tests green total (3 foundation + 13 ui-core + 29 blazor adapter); 228 ComponentBase-derived types in the adapter DLL.
>
> **Note on this document:** The original Phase 3b plan was drafted during execution but lost during GitButler branch transitions. This document reconstructs the plan from the commit history and in-session findings so future phases (and re-reads of the migration) have a complete reference. It reflects what actually shipped, not a speculative pre-plan.
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement plans like this one. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the 12 Marilo.Components category trees (Buttons, Charts, DataDisplay, DataGrid, Editors, Feedback, Forms, Layout, Navigation, Overlays, Utility, plus the shared `Internal/Interop` services) into `packages/ui-adapters-blazor/Components/*` as `Sunfish*` types — preserving behavior, renaming identifiers and namespaces, and wiring each category into `_Imports.razor`.

**Architecture:** Phase 3a scaffold (`Sunfish.Components.Blazor` RCL, `SunfishComponentBase`, `SunfishThemeProvider`) complete. Every component in this phase inherits `SunfishComponentBase` (already done in Marilo — the sed pass renames the base class reference). Internal interop services (`IElementMeasurementService`, `IDragService`, etc.) migrate first because component categories depend on them. CSS custom property references in component styles continue to use the Marilo→Sunfish aliases emitted by `SunfishThemeProvider` per Phase 3a D-THEME-CSS-ATOMICITY; Phase 3c cleans those up.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), bUnit 2.7.x, xUnit 2.9.x, NSubstitute 5.3.x (added during this phase for test DI mocking).

---

## Scope

**In scope:**
- 12 Marilo component category folders (actual totals below — the master plan's "181 components" number counts `.razor` only; total `.razor + .cs + .razor.cs` is ~329 source files, and the DLL contains 228 ComponentBase-derived concrete public types after build)
- `Internal/Interop/` services (25 files) and `Internal/DropdownPopup.razor` (already migrated in Phase 3a)
- `_Imports.razor` updates per category
- Per-category type-existence smoke tests (2 facts per category minimum)

**Out of scope — deferred:**
- `Shell/` components (Phase 4 — app shell plan)
- Provider packages `Marilo.Providers.FluentUI/Bootstrap/Material` → `packages/ui-adapters-blazor/Providers/*` (Phase 3c)
- Icon packages `Marilo.Icons.Tabler` + legacy `Marilo.Icons` (Phase 3d)
- JS content rename — JS files in `wwwroot/js/` still reference `marilo-*` selectors and `Marilo*` identifiers internally. Phase 3c handles these alongside the SCSS rename so selector changes can be validated end-to-end.
- SCSS `.razor.scss` files ship as-is — Phase 3c renames `--marilo-*` CSS custom properties
- Visual parity / Playwright tests — deferred to Phase 7 (kitchen-sink)
- Full render tests — most components have DI-heavy dependencies (internal interop services, theme service) that make bUnit rendering a significant harness-building exercise. Phase 3b established that type-existence + namespace assertions are enough for rename validation; deeper behavior tests are Phase 7 work.

---

## Category scope (counts from Marilo source)

| # | Category | Source files | ComponentBase types in DLL | Risk |
|---|---|---|---|---|
| 1 | `Internal/Interop` (pre-req for all below) | 25 | — services only | Low |
| 2 | Utility | 3 | 3 | Low |
| 3 | Editors | 6 | 3 | Low |
| 4 | Overlays | 9 | 7 | Low |
| 5 | Buttons | 11 | 11 | Low |
| 6 | Charts | 13 | 11 | Medium |
| 7 | Navigation | 23 | 19 | Medium |
| 8 | Feedback | 24 | 24 | Medium |
| 9 | Layout | 32 | 29 | Medium |
| 10 | DataGrid | 46 | 17 | High |
| 11 | Forms | 54 | 48 (across Containers + Inputs) | High |
| 12 | DataDisplay | 81 | 54 (+ Map 5, + Scheduler 8) | Highest |

---

## Key Decisions (as resolved during execution)

**D-ORDER:** Categories migrated in increasing file-count order (Utility → DataDisplay). Rationale: the transformation is mechanical (sed rename + file rename + build), so validating the pattern on the smallest category first limits blast radius when a sed rule surprises us.

**D-INTEROP-FIRST:** `Internal/Interop/` migrated as Task 1, BEFORE any component category, because multiple categories reference services like `IElementMeasurementService`, `IDragService`, `IGraphicsInteropService`. These are pure C# services (no Razor), so the transformation is simpler than category-level migration.

**D-SED:** Per-category rename runs a batched sed pass. Final rule set (evolved through six rounds of fixes during execution):

1. `IMarilo` → `ISunfish` (interfaces — must precede class rename so `IMariloThemeService` doesn't become `ISunfishMariloThemeService`)
2. `Marilo.Core.Contracts` → `Sunfish.UICore.Contracts`
3. `Marilo.Core.` → `Sunfish.Foundation.`
4. `Marilo.Components.Internal` → `Sunfish.Components.Blazor.Internal` (special-case: Internal is at package root, not under Components/)
5. `Marilo.Components.` → `Sunfish.Components.Blazor.Components.` (preserves `.Components.` path segment so types match `Components/<Category>/` folder)
6. `namespace Marilo.Components;` → `namespace Sunfish.Components.Blazor;`
7. `@inherits MariloComponentBase` → `@inherits SunfishComponentBase`
8. `MariloResizeEdges` → `ResizeEdges` (enum rename inherited from Phase 1)
9. `\bMarilo` → `Sunfish` (catch-all for remaining identifiers)
10. `class="mar-*"` and `class="marilo-*"` (single and double quotes) → `class="sf-*"` (hardcoded CSS class literal rewrite)
11. `@inherits Sunfish.Foundation.Base.SunfishComponentBase` → `@inherits SunfishComponentBase` (POST catch-all — runs last so it sees the already-renamed fully-qualified form)
12. `\bCore.Helpers.GridReflectionHelper` → `Sunfish.Foundation.Helpers.GridReflectionHelper` (specific bare-namespace fix)

**D-CODE-BEHIND-USING:** `.razor.cs` and plain `.cs` files that inherit `SunfishComponentBase` need an explicit `using Sunfish.Components.Blazor.Base;`. Marilo had `MariloComponentBase` co-located with framework-agnostic `CssClassBuilder` in `Marilo.Core.Base` (one using covered both). Sunfish splits them: `CssClassBuilder` stays in `Sunfish.Foundation.Base`; `SunfishComponentBase` moved to `Sunfish.Components.Blazor.Base` (Blazor dependencies forbidden in Foundation per Phase 1/2 contracts). Script post-pass detects `SunfishComponentBase` references and inserts the missing using.

**D-CSS-LITERALS:** Component markup sometimes contains hardcoded CSS class literals (e.g., `<span class="mar-button__icon">`) that are NOT routed through `ISunfishCssProvider`. Renamed from `mar-` / `marilo-` to `sf-` during each category migration. Rationale: leaving them as `mar-*` creates a "half-migrated" state where the sf-prefixed theme provider emits `--sf-*` vars but component markup references `mar-*` classes — a future reader can't tell which is authoritative. Only covers `class="..."` attribute forms; hardcoded literals inside `CombineClasses("mar-...")` expressions are Phase 3c scope (provider rename).

**D-IMPORTS:** `_Imports.razor` gained one `@using Sunfish.Components.Blazor.Components.<Category>` line per category, added in the same commit as the category migration. Final state has 11 category lines plus Forms subcategories (`Forms.Containers`, `Forms.Inputs`) where Marilo nested components into subfolders. `Internal.Interop` was added for Task 1 then **removed during Task 12** because of a name collision between `Internal.Interop.PopupPlacement` (internal enum) and `Components.Overlays.PopupPlacement` (public enum). Interop services only need DI access, not Razor-level using.

**D-TEST-SMOKE:** Per category, ship type-existence + namespace assertions (2 facts typical). Rationale: 12 categories × full coverage = unbounded test burden for a mechanical rename phase. Smoke tests confirm the sed pass didn't break markup + class topology. Deeper behavior tests are Phase 3c/7 work. Some components (Editor, DataGrid) have internal DI-heavy services that would require `InternalsVisibleTo("DynamicProxyGenAssembly2")` plus a real JSRuntime harness to render meaningfully — explicitly deferred.

**D-COMMIT-PER-CATEGORY:** One commit per category on `feat/migration-phase3b-blazor-components`. Each commit: files copied + renamed + sed'd, `_Imports.razor` updated, category smoke test added, build clean, tests green.

**D-SCSS:** Component `.razor.scss` files copy across unchanged — their content references `--marilo-*` CSS variables, which the dual-emit theme provider still resolves via aliases. Phase 3c renames the variables and drops the aliases. Leaving `.scss` untouched here keeps Phase 3b diffs small and makes provider migration a focused rename.

**D-BRANCH-TOPOLOGY (resolved during setup):** Phase 3a branch was behind main and missing Phase 1 foundation source in isolation. Fix: merge `origin/main` + local `feat/migration-phase1-foundation` into Phase 3b branch. This resolved the "Sunfish.slnx references files that don't exist on the branch" issue that blocked the initial setup.

**D-GITIGNORE:** Repo `.gitignore:216` rule `**/[Pp]ackages/*` was too broad — it matched the Sunfish `packages/` source tree, silently dropping new files from `git add`. Phase 1 branch had a cleaner fix (`*/**/[Pp]ackages/*` — requires a prefix directory). Adopted Phase 1's fix during merge conflict resolution.

---

## Reusable Sed Script

Saved as `scripts/migrate-marilo-category.sh`. Final form (after 6 rounds of fixes):

```bash
#!/usr/bin/env bash
# Usage: scripts/migrate-marilo-category.sh <category-name>
# Example: scripts/migrate-marilo-category.sh Buttons
#
# Prerequisites: SUNFISH and MARILO env vars exported.

set -euo pipefail

CATEGORY="${1:?category name required, e.g. Buttons}"
SRC="$MARILO/src/Marilo.Components/$CATEGORY"
DST="$SUNFISH/packages/ui-adapters-blazor/Components/$CATEGORY"

[ -d "$SRC" ] || { echo "FAIL: Marilo category not found: $SRC"; exit 1; }
[ ! -d "$DST" ] || { echo "FAIL: Sunfish category already exists: $DST (migration re-run?)"; exit 1; }

echo "→ Copying $CATEGORY: $SRC → $DST"
mkdir -p "$DST"
cp -r "$SRC/." "$DST/"

# Drop editor/backup artifacts BEFORE processing so the contamination gate
# doesn't trip on stale Marilo content inside *.bak scratch files.
echo "→ Removing backup/scratch files (*.bak, *.orig, *~)"
find "$DST" -type f \( -name "*.bak" -o -name "*.orig" -o -name "*~" \) -delete

echo "→ Renaming Marilo-prefixed files"
find "$DST" -type f \( -name "Marilo*.razor" -o -name "Marilo*.cs" \) | while read -r f; do
  new="$(dirname "$f")/$(basename "$f" | sed 's/^Marilo/Sunfish/')"
  mv "$f" "$new"
done

echo "→ Rewriting content (sed pass — code files)"
find "$DST" -type f \( -name "*.razor" -o -name "*.cs" -o -name "*.razor.cs" \) -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMarilo\.Core\.Contracts/Sunfish.UICore.Contracts/g' \
  -e 's/\bMarilo\.Core\./Sunfish.Foundation./g' \
  -e 's/\bMarilo\.Components\.Internal\b/Sunfish.Components.Blazor.Internal/g' \
  -e 's/\bMarilo\.Components\./Sunfish.Components.Blazor.Components./g' \
  -e 's/namespace Marilo\.Components;/namespace Sunfish.Components.Blazor;/g' \
  -e 's/@inherits MariloComponentBase/@inherits SunfishComponentBase/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  -e 's/@inherits Sunfish\.Foundation\.Base\.SunfishComponentBase/@inherits SunfishComponentBase/g' \
  -e 's/\bCore\.Helpers\.GridReflectionHelper/Sunfish.Foundation.Helpers.GridReflectionHelper/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e 's/class="marilo-/class="sf-/g' \
  -e "s/class='mar-/class='sf-/g" \
  -e "s/class='marilo-/class='sf-/g" \
  {} \;

# Markdown research/gap-analysis files also ship with categories.
# Narrow sed: rename identifier references; leave prose intact.
echo "→ Rewriting content (sed pass — markdown docs)"
find "$DST" -type f -name "*.md" -exec sed -i \
  -e 's/\bIMarilo/ISunfish/g' \
  -e 's/\bMariloResizeEdges\b/ResizeEdges/g' \
  -e 's/\bMarilo/Sunfish/g' \
  {} \;

# Both .razor.cs code-behinds AND plain .cs files (e.g. SunfishColumnBase.cs
# in DataGrid) can inherit SunfishComponentBase. Both need the explicit
# using — _Imports.razor only affects Razor files, not plain C#.
echo "→ Patching code-behind / plain-cs usings for SunfishComponentBase"
find "$DST" -type f \( -name "*.razor.cs" -o -name "*.cs" \) ! -name "*.AssemblyInfo.cs" | while read -r f; do
  if grep -q "SunfishComponentBase" "$f" && ! grep -q "using Sunfish\.Components\.Blazor\.Base;" "$f"; then
    if grep -q "^using Sunfish\.Foundation\.Base;" "$f"; then
      sed -i '0,/^using Sunfish\.Foundation\.Base;/{s//using Sunfish.Foundation.Base;\nusing Sunfish.Components.Blazor.Base;/}' "$f"
    else
      sed -i '1i using Sunfish.Components.Blazor.Base;' "$f"
    fi
  fi
done

echo "→ Grepping for leftover Marilo references"
if grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components)' "$DST"; then
  echo "FAIL: 'Marilo' identifiers remain in $DST"
  exit 1
fi

echo "OK: $CATEGORY migration complete"
```

---

## Task 0 — Branch setup (shipped as commits 061d306, 6e9b436, 1816753, 78811e1)

- [x] **Step 1:** Branch `feat/migration-phase3b-blazor-components` off Phase 3a tip (`aa9bf38`)
- [x] **Step 2:** Fix `.gitignore` to allow `/packages/` source tree (`061d306`)
- [x] **Step 3:** Merge `origin/main` to bring in ui-core (`6e9b436`)
- [x] **Step 4:** Merge `feat/migration-phase1-foundation` to bring in 117 foundation files (`1816753`)
- [x] **Step 5:** Add `scripts/migrate-marilo-category.sh` (`78811e1`, evolves through subsequent commits)

**Gotcha captured:** Phase 1 foundation branch was never pushed to origin and never merged to main. Every new branch off main will hit this blocker until Phase 1 gets its own PR into main. Documented for future phases.

---

## Task 1 — Internal/Interop Services (25 files) — commit `acaebe6`

**Files:** `$MARILO/src/Marilo.Components/Internal/Interop/` (25 files) → `packages/ui-adapters-blazor/Internal/Interop/`

- [x] **Step 1:** Manual copy + rename + sed (not via script — script targets `Components/<Category>/`, Interop is under `Internal/`)
- [x] **Step 2:** Build adapter → 0 errors
- [x] **Step 3:** Add `@using Sunfish.Components.Blazor.Internal.Interop` to `_Imports.razor` *(later removed in Task 12 due to name collision — see below)*
- [x] **Step 4:** Full solution test — 22 tests still passing (no Interop tests)
- [x] **Step 5:** Commit + push

---

## Tasks 2–12 — Component Categories (repeating pattern)

Each category follows the same 6-step template. Concrete commits and per-category notes:

### Task 2: Utility (3 files) — commit `b89a4e0`

- Script first-run revealed `.md` files weren't in sed pass → script fix (`5f7053b`)
- Representative smoke test: `SunfishIcon_RendersWithoutThrowing` (bUnit render, not just type check — Utility components have minimal DI)

### Task 3: Editors (6 files) — commit `41bdded`

- First category with `.cs` files (non-code-behind). Revealed script bug: namespace path was dropping `.Components.` segment, so `Sunfish.Components.Blazor.Editors` didn't match the Razor-inferred `Sunfish.Components.Blazor.Components.Editors`. Fixed in script.
- Also revealed `Internal` name collision with category rule — special-case rule added
- Test downgraded from render (bUnit) to type-existence because `SunfishEditor` injects two internal interop services requiring Castle DynamicProxy `InternalsVisibleTo` + JSRuntime harness. Documented as "defer to Phase 7 kitchen-sink"
- CS1591 (missing XML doc) errors on lifecycle overrides → added `<NoWarn>CS1591</NoWarn>` to adapter csproj (matches Phase 2 ui-core pattern)
- Added NSubstitute to `Directory.Packages.props` + adapter tests.csproj
- Added `InternalsVisibleTo Include="Sunfish.Components.Blazor.Tests"` (in `ItemGroup`, not `PropertyGroup` — original Phase 3a plan had it in the wrong group)

### Task 4: Overlays (9 files) — commit `de9a15a`

- First `.razor.cs` code-behind surfaced (`SunfishPopup.razor.cs`). Inherits `SunfishComponentBase` but didn't get the `using Sunfish.Components.Blazor.Base;` — original Marilo had `Marilo.Core.Base` covering both framework-agnostic CssClassBuilder and the Blazor-y MariloComponentBase; Sunfish split these into two namespaces
- Script post-pass added: detect `SunfishComponentBase` references in `.razor.cs` files and insert missing using

### Task 5: Buttons (11 files) — commits `c8de68d` + `cfa3922`

- First commit landed with a broken test (generic `SunfishChipSet<TItem>` referenced without type arg → CS0305). `&&` chain in the commit script didn't catch the compile error; fix commit `cfa3922` added `SunfishChipSet<>` open-generic reference

### Task 6: Charts (13 files) — commit `001068d`

- Clean first-try migration; no new fixes needed

### Task 7: Navigation (23 files) — commit `8fc3b6c`

- Revealed Blazor→Sunfish event-args bridge need: `TreeItemContextMenuEventArgs.MouseEventArgs` is typed `SunfishMouseEventArgs` (Phase 1 framework-agnostic) but Razor callback supplies Blazor `MouseEventArgs`
- Added `packages/ui-adapters-blazor/Internal/BlazorEventArgsExtensions.cs` with `ToSunfish()` bridge. One call site patched in `SunfishTreeView.razor.cs`

### Task 8: Feedback (24 files) — commit `aae15d8`

- Clean first-try migration; no new fixes needed

### Task 9: Layout (32 files) — commit `4226f38`

- Clean first-try migration; no new fixes needed

### Task 10: DataGrid (46 files) — commit `aea683b`

- First plain `.cs` file (non-`.razor.cs`) that inherits `SunfishComponentBase`: `SunfishColumnBase.cs`. Script post-pass widened to include plain `.cs` files (not just `.razor.cs`)
- `@inherits Sunfish.Foundation.Base.SunfishComponentBase` (fully-qualified form in Razor files) broke because sed ordering: `\bMarilo→Sunfish` rewrote the fully-qualified path to the wrong namespace, then the fixup rule to strip the path didn't fire because it ran BEFORE the rename. Fix: moved the fixup rule AFTER `\bMarilo→Sunfish` in sed pipeline
- `SunfishDataSheet.razor.cs` had a bare `Core.Helpers.GridReflectionHelper` reference (relying on namespace hierarchy resolution in Marilo). Added specific sed rule to rewrite to fully-qualified path

### Task 11: Forms (54 files) — commit `178f77b`

- Marilo's `Forms/Inputs/` had `.bak` editor backup files. Contamination gate caught `Marilo` references inside them
- Script fix: pre-processing step deletes `*.bak`, `*.orig`, `*~` files before sed pass
- Forms is nested in subfolders (`Containers/`, `Inputs/`) — `_Imports.razor` needed additional `@using` entries for each subfolder

### Task 12: DataDisplay (81 files) — commit `552b34f`

- Name collision: `SunfishGantt.razor` references `PopupPlacement` (expecting Overlays' public enum), but `_Imports.razor` also had `@using Sunfish.Components.Blazor.Internal.Interop` which brought the internal `PopupPlacement` into scope (same simple name)
- Fix: removed `@using Sunfish.Components.Blazor.Internal.Interop` from `_Imports.razor`. Interop services are only reached via DI injection so the ambient import was never needed
- DataDisplay has `AllocationScheduler/`, `Map/`, `Scheduler/` subfolders — but the `.cs` namespaces inside are all flat `Sunfish.Components.Blazor.Components.DataDisplay` (Marilo convention). Don't add subnamespace `@using` entries — the parent already covers everything

---

## Task 13 — Final verification (rolled into Task 12 commit)

- [x] Full solution build: `dotnet build Sunfish.slnx` → 0 errors, 0 warnings
- [x] Full solution test: `dotnet test Sunfish.slnx --no-build` → 45 tests passing (3 foundation + 13 ui-core + 29 blazor adapter)
- [x] Phase 2 invariant re-check: `dotnet test packages/ui-core/tests/tests.csproj --filter "FullyQualifiedName~HasNoBlazorDependency"` → 2 passing (ui-core still has no Blazor refs)
- [x] Contamination sweep: `grep -rE '\bMarilo' packages/ui-adapters-blazor/ | grep -v -E '(bin/|obj/|\.js$)'` → 0 C# matches (only JS content still has `Marilo` in comments/selectors — Phase 3c scope)
- [x] Branch pushed to origin

---

## Self-Review Checklist (post-execution)

**Migration hygiene**

- [x] Zero `\bMarilo[A-Za-z]|Marilo\.(Core|Components)` matches in `packages/ui-adapters-blazor/Components/` or `Internal/Interop/` source
- [x] Every Razor component inherits `SunfishComponentBase` (grep: `@inherits.*ComponentBase` → only `SunfishComponentBase`)
- [x] Every `using` directive targets `Sunfish.*`, not `Marilo.*`
- [x] `ResizeEdges` (not `SunfishResizeEdges`) used in any `ResizableContainer*` files
- [x] `.razor.scss` files still reference `--marilo-*` — **expected** until Phase 3c
- [x] JS files in `wwwroot/js/` still reference `Marilo*` identifiers in comments — **expected** until Phase 3c

**Structure**

- [x] `packages/ui-adapters-blazor/Components/` contains 11 category folders
- [x] `packages/ui-adapters-blazor/Internal/Interop/` contains 25 files
- [x] Each category has a `tests/Components/<Category>Tests.cs` smoke test
- [x] `_Imports.razor` has one `@using` per category (plus `Forms.Containers`, `Forms.Inputs` for nested subfolders)

**Build & test gates**

- [x] `dotnet build Sunfish.slnx` = 0 errors, 0 warnings
- [x] `dotnet test Sunfish.slnx` = 45 tests passing (3 + 13 + 29)
- [x] Phase 2 `HasNoBlazorDependency` filter tests still pass (2 tests)
- [x] DLL contains 228 ComponentBase-derived concrete public types (verified via reflection)

**Process**

- [x] All commits on `feat/migration-phase3b-blazor-components` (not main)
- [x] One commit per category (14 total: gitignore + merges + script + Interop + 11 categories + Buttons test-fix)
- [x] Branch pushed to origin

---

## Known gaps / parking lot (for follow-up phases)

1. **`AddMariloInteropServices` residual** — `packages/ui-adapters-blazor/Internal/Interop/InteropServiceExtensions.cs` contains a method `AddMariloInteropServices(...)` that escaped the rename because its file isn't under `Components/<Category>/` and the Interop migration was manual. Surfaced during Phase 3c plan research. Trivial fix: one-line rename + caller update.

2. **JS content** — Phase 3b only moved files + renamed file names (`marilo-*.js` → `sunfish-*.js`). JS file *contents* still reference `marilo-*` selectors and `Marilo*` identifiers in comments. Phase 3c rewrites these as part of the provider + SCSS rename.

3. **SCSS `--marilo-*` CSS custom properties** — deliberately untouched. Phase 3c renames alongside provider migration. `SunfishThemeProvider` dual-emits both prefixes during the intermediate state.

4. **Phase 3b smoke tests are shallow** — type existence + namespace. Deeper component behavior is validated via kitchen-sink (Phase 7) and Playwright visual parity (follow-up).

5. **Test project stub pattern is ad hoc** — `tests/Stubs.cs` has a full hand-written `StubCssProvider` from Phase 3a (~180 methods). Phase 3a plan had recommended NSubstitute; implementer kept the hand-written version. Phase 3b added NSubstitute alongside for new tests. Follow-up: rationalize to one approach (likely NSubstitute).

6. **Cross-category references via flat namespace** — DataDisplay subfolders (`AllocationScheduler/`, `Map/`, `Scheduler/`) share the flat `Sunfish.Components.Blazor.Components.DataDisplay` namespace because Marilo organized that way. Works correctly but surprises readers expecting folder-to-namespace parity. Document once, leave alone.

7. **.slnx IDE support** — The SLNX solution format is newer; some IDEs may not display the folder hierarchy cleanly. If this proves painful, a legacy `.sln` can be generated alongside as a fallback (files are unchanged; only the solution-manifest format differs).

---

## Execution notes for future similar migrations

Phase 3b took ~14 commits over multiple loop iterations. The per-category pattern stabilized after category 4 (Overlays) once the script had absorbed the code-behind-using post-pass. Categories 5 onward were mechanical.

Key lesson: the sed rule ordering matters a lot. Rules that need to see post-rename identifiers MUST come after the catch-all `\bMarilo→Sunfish`. Rules that need to run on pre-rename identifiers MUST come before. Build the pipeline top-down: specific-pre → catch-all → specific-post.

The six script-evolution rounds are the most reusable output of this phase — any similar category-tree migration in later phases (e.g., Phase 3c's provider packages, Phase 4's Shell) should start from this script and extend.
