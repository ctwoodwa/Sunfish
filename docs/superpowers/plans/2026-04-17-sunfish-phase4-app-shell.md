# Phase 4: App Shell — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate the seven shell components from `Marilo.Components.Shell` into the existing `packages/ui-adapters-blazor` Razor class library as a new `Shell/` subfolder under namespace `Sunfish.Components.Blazor.Shell`, and back them with smoke tests that bring the adapter suite to **46 tests**.

**Architecture:** Shell components live in the same package as the 11 migrated component categories (Buttons, Charts, DataDisplay, DataGrid, Editors, Feedback, Forms, Layout, Navigation, Overlays, Utility). The Marilo project split `Marilo.Components.Shell` into a separate NuGet package, but per the master plan (D-PACKAGE) we merge it in: shell components share the same dependency closure (`foundation` → `ui-core` → Blazor) and splitting them adds packaging complexity with no upside. The Shell namespace is a **sibling of `Components`** — not nested beneath it — because shell components sit structurally above the category tree and compose them.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), bUnit 2.7.x, xUnit 2.9.x

---

## Scope

**In scope** — 15 files from `C:/Projects/Marilo/src/Marilo.Components.Shell/AppShell/`:

- **7 components (.razor):** `MariloAppShell`, `MariloAppShellNavGroup`, `MariloAppShellNavLink`, `MariloAppShellSlideOver`, `MariloAccountMenu`, `MariloUserMenu`, `MariloNotificationBell`
- **4 scoped CSS (.razor.css):** `MariloAppShell` (18 KB), `MariloAccountMenu` (14 KB), `MariloUserMenu` (4 KB), `MariloNotificationBell` (5.6 KB)
- **4 C# types (.cs):** `AccountMenuItemModel.cs`, `AccountMenuOptions.cs` (contains `AccountMenuItemOptions` + `AccountMenuOptions`), `AccountMenuTemplateContexts.cs` (three `record` contexts), `PopupMenuItem.cs` (contains `PopupMenuItem` + `NotificationItem`)

**Out of scope:**
- Deep behavioural tests of shell components (DI-heavy — auth context, notification state). Those land in Phase 7 kitchen-sink. Phase 4 ships **type-existence smoke tests** only, matching the pattern established by Phase 3b category tests (see `tests/Components/ButtonsTests.cs`).
- `Marilo.Components.Shell.csproj` does NOT carry over as a separate Sunfish project. Its `<PackageReference Include="Microsoft.AspNetCore.Components.Web" />` is already covered transitively by the adapter's `<FrameworkReference Include="Microsoft.AspNetCore.App" />`.
- Any notification pipeline wiring (no `ISunfishNotificationService` reference exists in Marilo Shell — `MariloNotificationBell` is a pure presentation component whose `Items` come from a parent `[Parameter]`).

---

## Key Decisions

**D-NAMESPACE:** Sunfish shell components use `Sunfish.Components.Blazor.Shell`. The Marilo source used `Marilo.Components.Layout.AppShell` (note: NOT `Marilo.Components.Shell` — the namespace disagrees with the package name). The migration script's rule `Marilo.Components. → Sunfish.Components.Blazor.Components.` is **wrong for Shell** because it would produce `Sunfish.Components.Blazor.Components.Layout.AppShell`. We must not reuse the script's sed pass unmodified — we perform a manual migration with a targeted sed recipe below.

**D-IMPORTS:** Append `@using Sunfish.Components.Blazor.Shell` to `packages/ui-adapters-blazor/_Imports.razor` alongside the existing category usings. This is the only consumer-visible surface change in the root imports file.

**D-SCRIPT-REUSE:** The migration script at `scripts/migrate-marilo-category.sh` hard-codes `packages/ui-adapters-blazor/Components/<Category>/` as the destination and assumes source `Marilo.Components/<Category>/`. Shell has a different source path (`Marilo.Components.Shell/AppShell/`) and a different destination path (`packages/ui-adapters-blazor/Shell/`). Three options considered:

- **(a)** Create a parallel `migrate-marilo-shell.sh` — overkill for a one-shot migration.
- **(b)** Add a `--target-dir` + `--source-root` flag to the existing script — investment without payoff; Shell is the only non-category migration.
- **(c)** Migrate manually with a targeted inline sed recipe, following the Phase 3a Interop-style manual copy pattern.

**Decision: (c).** Shell is a one-shot, not a reusable pattern. If a later phase needs category-shaped migration from another Marilo package, extend the script then.

**D-DEPENDENCIES:** Verified by grep — Marilo Shell components do **not** reference `IMariloNotificationService` or any `IMarilo*` service. Only `NavigationManager` (standard Blazor) is injected, in `MariloAppShell.razor` and `MariloAccountMenu.razor`. No new `ProjectReference` or DI wiring is required in `Sunfish.Components.Blazor.csproj`.

**D-ICON-DEPENDENCY:** `MariloNotificationBell` renders icons as **inline SVG markup** in its Razor template (hard-coded gear/trash SVG paths for the "more options" menu). It does **not** go through `IMariloIconProvider` / `ISunfishIconProvider`. This means Phase 3d (icon provider work) is **not a prerequisite** for Phase 4. Document this explicitly so a future consumer who wants theme-aware icons in the bell understands the pre-existing limitation. If Phase 3d later introduces an `ISunfishIconProvider`-based alternative, `SunfishNotificationBell` can be updated post-hoc — out of scope here.

**D-SHELL-TESTS:** Shell components have parameter-heavy APIs and would require substantial bUnit rigging (cascading values, `NavigationManager` fake, render-fragment stubs) to render meaningfully. Follow the Phase 3b smoke-test pattern: **one test class `ShellTests.cs`** with type-existence + namespace assertions, deferred rendering to Phase 7 (kitchen-sink). The `ShellTests` class ships 2 facts (matching the `ButtonsTests.cs` precedent), bringing the adapter suite from 45 → **47 tests**. The brief's "46" undercount assumed a single fact; plan standardises on the established 2-fact category pattern.

---

## File Structure (after Phase 4)

```
packages/ui-adapters-blazor/
  Sunfish.Components.Blazor.csproj
  _Imports.razor                        ← + @using Sunfish.Components.Blazor.Shell
  Base/
    SunfishComponentBase.cs
  SunfishThemeProvider.razor
  Internal/
    DropdownPopup.razor
  Components/                            (11 migrated categories — unchanged)
    Buttons/ ... Utility/
  Shell/                                 ← NEW in Phase 4
    SunfishAppShell.razor
    SunfishAppShell.razor.css
    SunfishAppShellNavGroup.razor
    SunfishAppShellNavLink.razor
    SunfishAppShellSlideOver.razor
    SunfishAccountMenu.razor
    SunfishAccountMenu.razor.css
    SunfishUserMenu.razor
    SunfishUserMenu.razor.css
    SunfishNotificationBell.razor
    SunfishNotificationBell.razor.css
    AccountMenuItemModel.cs
    AccountMenuOptions.cs                ← contains AccountMenuItemOptions + AccountMenuOptions
    AccountMenuTemplateContexts.cs       ← AppearanceMenuContext, LanguageMenuContext, HelpMenuContext
    PopupMenuItem.cs                     ← contains PopupMenuItem + NotificationItem
  wwwroot/
    js/                                   (unchanged)
  tests/
    tests.csproj                          (unchanged)
    Stubs.cs
    SunfishComponentBaseTests.cs
    SunfishThemeProviderTests.cs
    Components/
      ButtonsTests.cs ... UtilityTests.cs
      ShellTests.cs                       ← NEW in Phase 4 (lives under Components/ for test discovery parity)
```

**Note on test folder:** we place `ShellTests.cs` under `tests/Components/` alongside the category smoke tests, not under a parallel `tests/Shell/` folder. Rationale: it keeps the discovery pattern consistent and means the test class's namespace is `Sunfish.Components.Blazor.Tests.Components`, matching the other 11 category test classes. The class asserts types from `Sunfish.Components.Blazor.Shell` (the production namespace) — test-file location and production namespace are independent.

---

## Task 1: Create the branch and Shell folder

**Files:**
- Create: `packages/ui-adapters-blazor/Shell/` (empty directory)

- [ ] **Step 1: Branch from main (assuming Phase 3 is fully merged)**

```bash
cd "C:/Projects/Sunfish"
git fetch origin main
git checkout -b feat/migration-phase4-app-shell origin/main
```

**Fallback if Phase 3b not yet merged to main:** branch from the Phase 3b tip so all 11 categories are in tree:

```bash
git checkout -b feat/migration-phase4-app-shell feat/migration-phase3b-blazor-components
```

- [ ] **Step 2: Verify target directory does not exist, then create it**

```bash
ls "C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell" 2>&1 | head -1
# Expected: "ls: cannot access ..." — confirm absence

mkdir -p "C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell"
```

---

## Task 2: Copy, rename, and rewrite Shell files

**Files:**
- Copy: 15 files from `C:/Projects/Marilo/src/Marilo.Components.Shell/AppShell/` → `packages/ui-adapters-blazor/Shell/`
- Source namespace: `Marilo.Components.Layout.AppShell`
- Target namespace: `Sunfish.Components.Blazor.Shell`

- [ ] **Step 1: Copy all files (preserve attributes)**

```bash
SRC="C:/Projects/Marilo/src/Marilo.Components.Shell/AppShell"
DST="C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell"
cp -r "$SRC/." "$DST/"
ls "$DST"
```

Expected listing: 7 `.razor`, 4 `.razor.css`, 4 `.cs` = 15 files.

- [ ] **Step 2: Rename Marilo-prefixed files**

```bash
cd "$DST"
for f in Marilo*.razor Marilo*.razor.css Marilo*.cs; do
  [ -e "$f" ] || continue
  mv "$f" "${f/Marilo/Sunfish}"
done
ls
```

Expected: `Sunfish*.razor`, `Sunfish*.razor.css`, plus `AccountMenuItemModel.cs`, `AccountMenuOptions.cs`, `AccountMenuTemplateContexts.cs`, `PopupMenuItem.cs` (these 4 don't carry the Marilo prefix).

- [ ] **Step 3: Rewrite content (sed pass — code files)**

The recipe differs from `scripts/migrate-marilo-category.sh` in two places: namespace (`.Layout.AppShell` → `.Shell`) and data-attribute (`data-marilo-appshell` → `data-sf-appshell`). Order matters — rewrite namespaces BEFORE the generic `Marilo → Sunfish` catch-all.

```bash
cd "$DST"
find . -type f \( -name "*.razor" -o -name "*.cs" \) -exec sed -i \
  -e 's/\bMarilo\.Components\.Layout\.AppShell\b/Sunfish.Components.Blazor.Shell/g' \
  -e 's/\bdata-marilo-appshell\b/data-sf-appshell/g' \
  -e 's/\bMariloAppShellNavGroup\b/SunfishAppShellNavGroup/g' \
  -e 's/\bMariloAppShellNavLink\b/SunfishAppShellNavLink/g' \
  -e 's/\bMariloAppShellSlideOver\b/SunfishAppShellSlideOver/g' \
  -e 's/\bMariloAppShell\b/SunfishAppShell/g' \
  -e 's/\bMariloAccountMenu\b/SunfishAccountMenu/g' \
  -e 's/\bMariloUserMenu\b/SunfishUserMenu/g' \
  -e 's/\bMariloNotificationBell\b/SunfishNotificationBell/g' \
  -e 's/class="mar-/class="sf-/g' \
  -e "s/class='mar-/class='sf-/g" \
  -e 's/\bMarilo/Sunfish/g' \
  {} \;
```

- [ ] **Step 4: Rewrite content (sed pass — scoped CSS files)**

Scoped CSS files (`*.razor.css`) reference class selectors directly. The `mar-*` → `sf-*` rewrite must apply to `.mar-foo` selectors, which the class-attribute rewrite above doesn't cover.

```bash
cd "$DST"
find . -type f -name "*.razor.css" -exec sed -i \
  -e 's/\.mar-appshell/.sf-appshell/g' \
  -e 's/\.mar-usermenu/.sf-usermenu/g' \
  -e 's/\.mar-notifbell/.sf-notifbell/g' \
  -e 's/\.mar-account-menu/.sf-account-menu/g' \
  -e 's/--marilo-/--sf-/g' \
  -e 's/\bMariloAppShell\b/SunfishAppShell/g' \
  -e 's/\bMariloAccountMenu\b/SunfishAccountMenu/g' \
  -e 's/\bMariloUserMenu\b/SunfishUserMenu/g' \
  -e 's/\bMariloNotificationBell\b/SunfishNotificationBell/g' \
  {} \;
```

Note: the CSS files include a banner comment like `/* MariloAppShell — ... */` which the identifier rewrite above catches.

- [ ] **Step 5: Grep contamination gate**

```bash
cd "$DST"
if grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components)|mar-appshell|mar-usermenu|mar-notifbell|mar-account-menu|data-marilo|--marilo-' .; then
  echo "FAIL: contamination remains"
  exit 1
fi
echo "OK: Shell migration content clean"
```

Expected: no matches. If any appear, inspect and update the sed recipe.

- [ ] **Step 6: Spot-check one file**

Read `packages/ui-adapters-blazor/Shell/SunfishAppShell.razor`:
- Line 1 should read: `@namespace Sunfish.Components.Blazor.Shell`
- `data-marilo-appshell` should have become `data-sf-appshell`
- `class="mar-appshell ..."` should have become `class="sf-appshell ..."`
- `@inherits ComponentBase` stays as-is (Shell components never inherited `MariloComponentBase` — they use stock `ComponentBase`)

---

## Task 3: Handle edge cases and name collisions

**Files:**
- Inspect: `Shell/SunfishAppShell.razor`, `Shell/SunfishAccountMenu.razor`

- [ ] **Step 1: Verify no `IMarilo*` service injections leaked in**

```bash
grep -rE '\bIMarilo|@inject.*Marilo' "C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell/"
```

Expected: no matches. Shell components only inject `NavigationManager`.

- [ ] **Step 2: Verify `@inherits ComponentBase` (not `SunfishComponentBase`)**

```bash
grep -r '@inherits' "C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell/"
```

Expected: every `.razor` file shows `@inherits ComponentBase`. This is intentional — Marilo shell components never used the CSS/theme/icon-provider pipeline of `MariloComponentBase`. We preserve that choice. If a future plan wants them to participate in theming, that is a separate decision.

- [ ] **Step 3: Confirm file-internal cross-references resolved**

`SunfishUserMenu.razor` uses `List<PopupMenuItem>`. `PopupMenuItem.cs` lives in the same folder and namespace — no using directive needed.

`SunfishNotificationBell.razor` uses `List<NotificationItem>`. `NotificationItem` is defined in `PopupMenuItem.cs` (same file, same namespace) — no using needed.

`SunfishAccountMenu.razor` may reference types from `AccountMenuTemplateContexts.cs` and `AccountMenuOptions.cs` — all same namespace, no using needed.

```bash
grep -nE 'PopupMenuItem|NotificationItem|AccountMenuItemModel|AccountMenuOptions|AppearanceMenuContext|LanguageMenuContext|HelpMenuContext' \
  "C:/Projects/Sunfish/packages/ui-adapters-blazor/Shell/"*.razor
```

Expected: references exist but no `using` is required because the types live in `Sunfish.Components.Blazor.Shell` alongside the consumers.

- [ ] **Step 4: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/ui-adapters-blazor/Shell/
git commit -m "feat(ui-adapters-blazor): migrate Shell components from Marilo.Components.Shell"
```

---

## Task 4: Update _Imports.razor

**Files:**
- Modify: `packages/ui-adapters-blazor/_Imports.razor`

- [ ] **Step 1: Append the shell using directive**

Add one line to `packages/ui-adapters-blazor/_Imports.razor`, after the last `@using Sunfish.Components.Blazor.Components.Utility`:

```razor
@using Sunfish.Components.Blazor.Shell
```

Use the `Edit` tool, not sed, for precision.

- [ ] **Step 2: Stage (don't commit yet — will roll up with tests)**

```bash
cd "C:/Projects/Sunfish"
git add packages/ui-adapters-blazor/_Imports.razor
```

---

## Task 5: Build the adapter package clean

**Files:** no source changes — build verification only.

- [ ] **Step 1: Restore and build**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj 2>&1 | tail -20
```

Expected: `Build succeeded. 0 Warning(s), 0 Error(s).`

Common issues: CS0246 (`PopupMenuItem` not found) means `PopupMenuItem.cs` namespace wasn't rewritten; CS0234 (`Marilo` not found) means the sed pass missed a reference — rerun the contamination grep. BL9989 namespace/folder mismatch is benign; if it appears set `<RootNamespace>Sunfish.Components.Blazor</RootNamespace>` in the csproj.

---

## Task 6: Write smoke tests

**Files:**
- Create: `packages/ui-adapters-blazor/tests/Components/ShellTests.cs`

- [ ] **Step 1: Write the failing test**

Create `packages/ui-adapters-blazor/tests/Components/ShellTests.cs`:

```csharp
using Sunfish.Components.Blazor.Shell;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

public class ShellTests
{
    [Fact]
    public void SunfishAppShell_TypeIsPublicAndInShellNamespace()
    {
        var type = typeof(SunfishAppShell);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Components.Blazor.Shell", type.Namespace);
    }

    [Fact]
    public void AllExpectedShellTypes_AreInShellNamespace()
    {
        var expected = new[]
        {
            typeof(SunfishAppShell),
            typeof(SunfishAppShellNavGroup),
            typeof(SunfishAppShellNavLink),
            typeof(SunfishAppShellSlideOver),
            typeof(SunfishAccountMenu),
            typeof(SunfishUserMenu),
            typeof(SunfishNotificationBell),
            typeof(AccountMenuItemModel),
            typeof(AccountMenuItemOptions),
            typeof(AccountMenuOptions),
            typeof(AppearanceMenuContext),
            typeof(LanguageMenuContext),
            typeof(HelpMenuContext),
            typeof(PopupMenuItem),
            typeof(NotificationItem),
        };
        foreach (var t in expected)
        {
            Assert.Equal("Sunfish.Components.Blazor.Shell", t.Namespace);
        }
    }
}
```

Note: this asserts all 7 components + all 8 supporting types = 15 type checks across 2 facts, mirroring `ButtonsTests.cs`'s structure.

- [ ] **Step 2: Build tests (expect failure before the source is in place — but source IS in place after Tasks 2–3, so this should build)**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/tests/tests.csproj 2>&1 | tail -10
```

Expected: `Build succeeded. 0 Warning(s), 0 Error(s).`

If compile errors cite missing types, revisit Task 2 sed and confirm file-rename step ran (e.g., the test cannot resolve `SunfishAppShell` if the file is still `MariloAppShell.razor`).

- [ ] **Step 3: Run the tests**

```bash
cd "C:/Projects/Sunfish"
dotnet test packages/ui-adapters-blazor/tests/tests.csproj --no-build 2>&1 | tail -10
```

Expected: `Passed! - Failed: 0, Passed: 47, Skipped: 0, Total: 47` (45 pre-existing + 2 new `ShellTests` facts).

If the brief's expected count is 46 (45 + 1), this plan's Task 6 intentionally ships 2 facts to match the per-category smoke-test precedent from Phase 3b. Adjust the class to a single `[Fact]` if strict 46 is required; the recommended count is 47.

---

## Task 7: Full solution test and commit

- [ ] **Step 1: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx 2>&1 | tail -5
dotnet test Sunfish.slnx --no-build 2>&1 | tail -10
```

Expected: 0 errors, 0 warnings. Total test count: 3 foundation + 13 ui-core + 47 adapter = **63 tests passing**.

- [ ] **Step 2: Stage remaining changes and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/ui-adapters-blazor/_Imports.razor
git add packages/ui-adapters-blazor/tests/Components/ShellTests.cs
git commit -m "feat(ui-adapters-blazor): register Shell namespace and add ShellTests smoke tests"
git status
```

Expected: working tree clean.

- [ ] **Step 3: Push branch**

```bash
git push -u origin feat/migration-phase4-app-shell
```

---

## Self-Review Checklist

- [ ] All 15 files from `Marilo.Components.Shell/AppShell/` are present in `packages/ui-adapters-blazor/Shell/`
- [ ] All 7 `.razor` files and 4 `.razor.css` files have been renamed from `Marilo*` to `Sunfish*`
- [ ] Every file in `Shell/` uses namespace `Sunfish.Components.Blazor.Shell` (not `Sunfish.Components.Blazor.Components.Layout.AppShell`, not `Sunfish.Components.Blazor.Components.Shell`)
- [ ] `grep -rE '\bMarilo[A-Za-z]|Marilo\.(Core|Components)|mar-appshell|mar-usermenu|mar-notifbell|mar-account-menu|data-marilo|--marilo-' packages/ui-adapters-blazor/Shell/` returns nothing
- [ ] `_Imports.razor` contains `@using Sunfish.Components.Blazor.Shell` on its own line
- [ ] Shell components inherit `ComponentBase` (NOT `SunfishComponentBase`) — this matches Marilo behaviour
- [ ] No `IMarilo*` or `ISunfish*` service injections leaked into Shell components (only `NavigationManager`)
- [ ] `MariloNotificationBell` → `SunfishNotificationBell` — inline SVG icons preserved; D-ICON-DEPENDENCY documented (Phase 3d not required)
- [ ] Scoped CSS selectors (`*.razor.css`) use `.sf-*` class prefixes and `--sf-*` CSS custom properties
- [ ] `dotnet build Sunfish.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test Sunfish.slnx` = 47 adapter tests passing (45 existing + 2 new in `ShellTests`)
- [ ] Full solution test count = 63 (3 foundation + 13 ui-core + 47 adapter)
- [ ] `ShellTests.cs` lives under `tests/Components/` matching category test convention
- [ ] The contamination grep gate from Task 2 Step 5 was executed and returned clean
- [ ] Branch `feat/migration-phase4-app-shell` is pushed to origin

---

## Findings on Shell source structure (for reviewer context)

- **Namespace anomaly:** Marilo's shell package is named `Marilo.Components.Shell` (csproj `PackageId`) but its `RootNamespace` and all file-level namespaces are `Marilo.Components.Layout.AppShell`. The generic migration script's `Marilo.Components.` rule does not apply cleanly — our targeted namespace rewrite handles it.
- **Inheritance:** Shell components inherit stock `ComponentBase`, not `MariloComponentBase`. No theme/CSS-provider/icon-provider participation.
- **Service dependencies:** Only `NavigationManager` is injected. No DI for theming, icons, or notifications.
- **Icon rendering:** Inline SVG markup (not `ISunfishIconProvider`). Phase 3d is NOT a prerequisite.
- **Scoped CSS:** 4 files totalling ~42 KB using `mar-appshell__*`, `mar-usermenu__*`, `mar-notifbell__*`, `mar-account-menu__*` — all remapped to `sf-*`.
- **Data attribute:** `MariloAppShell` writes `data-marilo-appshell` → remapped to `data-sf-appshell`.
- **Cross-file types:** `PopupMenuItem.cs` hosts both `PopupMenuItem` and `NotificationItem`.
- **No test assets to copy:** Marilo's Shell package has no tests; Phase 4 tests are greenfield smoke tests.
