# Phase 3a: Blazor Adapter Infrastructure — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up the `packages/ui-adapters-blazor` Razor class library with the Sunfish component base class, theme provider, internal popup component, and JS interop infrastructure — everything that all 181 Razor components will inherit from or depend on.

**Architecture:** `ui-adapters-blazor` is the first Blazor-containing package. It sits above `ui-core` in the dependency chain: `foundation → ui-core → ui-adapters-blazor`. This is the only package that references `Microsoft.AspNetCore.App` (via `<FrameworkReference>`). `SunfishComponentBase` injects `ISunfishCssProvider`, `ISunfishIconProvider`, and `ISunfishThemeService` — all framework-agnostic contracts from `foundation` and `ui-core`.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), bUnit 2.7.x, xUnit 2.9.x

---

## Key Decisions

**D-BASE:** `MariloComponentBase` lives in `Marilo.Core/Base/` (alongside CssClassBuilder and StyleBuilder). For Sunfish, `SunfishComponentBase.cs` moves to `packages/ui-adapters-blazor/Base/` because it has Blazor types (`ComponentBase`). The framework-agnostic `CssClassBuilder` and `StyleBuilder` stay in `packages/foundation/Base/` (they're already there from Phase 1).

**D-THEME-CSS-VARS:** CSS custom properties use the `--marilo-*` prefix in Marilo. These become `--sf-*` in Sunfish. Same for the CSS class `marilo-theme-provider` → `sf-theme-provider`, data attribute `data-marilo-theme` → `data-sf-theme`, and `mar-button__icon` → `sf-button__icon` (seen in MariloButton.razor; this will be handled per-component in Phase 3b, but the pattern is established here).

**D-JS:** The `wwwroot/js/` folder has 12 JS files named `marilo-*.js`. For Phase 3a, copy them all with `sunfish-` prefix. The actual content references CSS variables and selectors that will be updated when we test each component category in Phase 3b.

**D-IMPORTS:** `_Imports.razor` should cover all Sunfish namespaces. All 181 components will use `@inherits SunfishComponentBase` without needing explicit using directives because the base class is covered by the imports.

**D-INTERNAL:** `Internal/DropdownPopup.razor` is a shared internal component used by multiple categories (dropdowns, comboboxes, etc.). Migrate it in Phase 3a so it's available when component categories are migrated in Phase 3b.

---

## File Structure

```
packages/ui-adapters-blazor/
  Sunfish.Components.Blazor.csproj     ← new; Razor SDK; FrameworkReference AspNetCore.App
  _Imports.razor                        ← using Sunfish.* directives
  Base/
    SunfishComponentBase.cs             ← migrated from MariloComponentBase.cs
  SunfishThemeProvider.razor            ← migrated from MariloThemeProvider.razor
  Internal/
    DropdownPopup.razor                 ← migrated from Internal/DropdownPopup.razor
  wwwroot/
    js/
      sunfish-clipboard-download.js
      sunfish-datasheet.js
      sunfish-dragdrop.js
      sunfish-dropzone.js
      sunfish-gantt.js
      sunfish-graphics.js
      sunfish-map.js
      sunfish-measurement.js
      sunfish-observers.js
      sunfish-scheduler.js
      (+ remaining marilo-*.js renamed)
  tests/
    tests.csproj                        ← bUnit + xUnit
    SunfishComponentBaseTests.cs        ← verify injection, CombineClasses/CombineStyles
    SunfishThemeProviderTests.cs        ← verify renders child content, theme cascade
```

Files to update:
- `Sunfish.slnx` — add ui-adapters-blazor and its tests

---

## Task 1: Create ui-adapters-blazor project

**Files:**
- Create: `packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj`

- [ ] **Step 1: Create Sunfish.Components.Blazor.csproj**

```xml
<!-- packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Components.Blazor</PackageId>
    <Description>Blazor component implementations for Sunfish.</Description>
    <PackageTags>blazor;components;razor-class-library;ui-framework;sunfish</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <InternalsVisibleTo Include="Sunfish.Components.Blazor.Tests" />
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Markdig" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
  </ItemGroup>
</Project>
```

Note: `Nullable` and `ImplicitUsings` are explicit here (not relying only on `Directory.Build.props`) because Razor SDK projects can sometimes skip inherited MSBuild props — being explicit avoids surprises.

- [ ] **Step 2: Restore and verify baseline build**

```bash
cd "C:/Projects/Sunfish"
dotnet restore packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
```

Expected: 0 errors. No source files yet is fine.

- [ ] **Step 3: Stage and commit**

```bash
but stage "packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj" "feat/migration-phase3a-blazor-infra"
but commit -m "feat(ui-adapters-blazor): scaffold Sunfish.Components.Blazor project" "feat/migration-phase3a-blazor-infra"
```

---

## Task 2: Create _Imports.razor

**Files:**
- Create: `packages/ui-adapters-blazor/_Imports.razor`

- [ ] **Step 1: Create _Imports.razor**

Create `packages/ui-adapters-blazor/_Imports.razor`:

```razor
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.Extensions.Configuration
@using Microsoft.JSInterop
@using Sunfish.Foundation.Base
@using Sunfish.Foundation.Configuration
@using Sunfish.Foundation.Contracts
@using Sunfish.Foundation.Data
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Models
@using Sunfish.Foundation.Services
@using Sunfish.UICore.Contracts
@using Sunfish.Components.Blazor.Base
@using Sunfish.Components.Blazor.Components.Charts
@using Sunfish.Components.Blazor.Components.DataDisplay
@using Sunfish.Components.Blazor.Components.DataGrid
@using Sunfish.Components.Blazor.Components.Editors
@using Sunfish.Components.Blazor.Components.Feedback
@using Sunfish.Components.Blazor.Components.Forms
@using Sunfish.Components.Blazor.Components.Layout
@using Sunfish.Components.Blazor.Components.Navigation
@using Sunfish.Components.Blazor.Components.Overlays
@using Sunfish.Components.Blazor.Components.Utility
@using Sunfish.Components.Blazor.Internal
```

- [ ] **Step 2: Build to verify _Imports.razor is valid**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
```

Expected: 0 errors. Some namespace usings will reference namespaces that don't exist yet (Components.Charts etc.) — this is fine at this stage; Razor doesn't error on unused `@using` directives.

Actually, Razor class libraries DO fail on unknown namespaces in _Imports.razor if they contain Razor files. Since we have no `.razor` files yet, this is fine. Once we start adding components in Phase 3b, these namespaces will exist.

If the build fails because of missing namespaces, remove the component-category `@using` lines (Charts, DataDisplay, etc.) and add them back as each category is migrated in Phase 3b.

- [ ] **Step 3: Stage and commit**

```bash
but stage "packages/ui-adapters-blazor/_Imports.razor" "feat/migration-phase3a-blazor-infra"
but commit -m "feat(ui-adapters-blazor): add _Imports.razor with Sunfish namespace usings" "feat/migration-phase3a-blazor-infra"
```

---

## Task 3: Migrate SunfishComponentBase

**Files:**
- Create: `packages/ui-adapters-blazor/Base/SunfishComponentBase.cs`
- Source: `C:/Projects/Marilo/src/Marilo.Core/Base/MariloComponentBase.cs`

- [ ] **Step 1: Create the tests project**

Create `packages/ui-adapters-blazor/tests/tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <AssemblyName>Sunfish.Components.Blazor.Tests</AssemblyName>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="bunit" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Components.Blazor.csproj" />
  </ItemGroup>
</Project>
```

Note: uses `Microsoft.NET.Sdk.Razor` (not just `Microsoft.NET.Sdk`) because bUnit tests with Razor components require it.

- [ ] **Step 2: Write the failing test**

Create `packages/ui-adapters-blazor/tests/SunfishComponentBaseTests.cs`:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Components.Blazor.Base;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;

namespace Sunfish.Components.Blazor.Tests;

/// <summary>
/// Verifies SunfishComponentBase infrastructure: CSS/style composition and disposal.
/// Uses a minimal concrete subclass to exercise the abstract base.
/// </summary>
public class SunfishComponentBaseTests : TestContext
{
    public SunfishComponentBaseTests()
    {
        // Register required services that SunfishComponentBase injects
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    [Fact]
    public void CombineClasses_AppendsUserClassToBaseClass()
    {
        var cut = RenderComponent<TestSunfishComponent>(p => p
            .Add(x => x.Class, "user-class")
            .Add(x => x.BaseClass, "base-class"));

        Assert.Contains("base-class user-class", cut.Markup);
    }

    [Fact]
    public void CombineClasses_WhenNoUserClass_ReturnsBaseClassOnly()
    {
        var cut = RenderComponent<TestSunfishComponent>(p => p
            .Add(x => x.BaseClass, "base-class"));

        Assert.Contains("base-class", cut.Markup);
        Assert.DoesNotContain("null", cut.Markup);
    }

    [Fact]
    public void AdditionalAttributes_ArePropagatedToElement()
    {
        var cut = RenderComponent<TestSunfishComponent>(p => p
            .AddUnmatched("data-testid", "my-button"));

        Assert.Contains("data-testid=\"my-button\"", cut.Markup);
    }
}

/// <summary>Minimal Sunfish component for testing base class behaviour.</summary>
public class TestSunfishComponent : SunfishComponentBase
{
    [Parameter] public string BaseClass { get; set; } = string.Empty;

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", CombineClasses(BaseClass));
        builder.AddMultipleAttributes(2, AdditionalAttributes);
        builder.CloseElement();
    }
}

// ── Stubs ────────────────────────────────────────────────────────────────────

internal sealed class StubCssProvider : ISunfishCssProvider
{
    public string ContainerClass(string? size = null) => string.Empty;
    public string GridClass() => string.Empty;
    public string RowClass() => string.Empty;
    public string ColumnClass(int? span = null, int? offset = null) => string.Empty;
    public string StackClass(Sunfish.Foundation.Enums.StackDirection orientation) => string.Empty;
    // All remaining interface methods return string.Empty
    public string DividerClass(bool vertical) => string.Empty;
    public string PanelClass() => string.Empty;
    public string DrawerClass(Sunfish.Foundation.Enums.DrawerPosition position, bool isOpen) => string.Empty;
    public string AppBarClass(Sunfish.Foundation.Enums.AppBarPosition position) => string.Empty;
    public string AccordionClass() => string.Empty;
    public string AccordionItemClass(bool isExpanded) => string.Empty;
    public string TabsClass(Sunfish.Foundation.Enums.TabPosition position, Sunfish.Foundation.Enums.TabAlignment alignment, Sunfish.Foundation.Enums.TabSize size) => string.Empty;
    public string TabClass(bool isActive, bool isDisabled) => string.Empty;
    public string TabPanelClass(bool isActive, bool persistContent) => string.Empty;
    public string StepperClass() => string.Empty;
    public string StepClass(Sunfish.Foundation.Enums.StepStatus status) => string.Empty;
    public string SplitterClass(Sunfish.Foundation.Enums.SplitterOrientation orientation) => string.Empty;
    public string DrawerOverlayClass() => string.Empty;
    public string ContextMenuClass() => string.Empty;
    public string NavBarClass() => string.Empty;
    public string NavMenuClass() => string.Empty;
    public string NavItemClass(bool isActive) => string.Empty;
    public string BreadcrumbClass() => string.Empty;
    public string BreadcrumbItemClass() => string.Empty;
    public string MenuClass() => string.Empty;
    public string MenuItemClass(bool isDisabled) => string.Empty;
    public string MenuDividerClass() => string.Empty;
    public string PaginationClass() => string.Empty;
    public string PaginationItemClass(bool isActive) => string.Empty;
    public string TreeViewClass() => string.Empty;
    public string TreeItemClass(bool isExpanded, bool isSelected) => string.Empty;
    public string ToolbarClass() => string.Empty;
    public string EnvironmentBadgeClass(string env) => string.Empty;
    public string TimeRangeSelectorClass() => string.Empty;
    public string ToolbarButtonClass(bool isDisabled = false) => string.Empty;
    public string ToolbarToggleButtonClass(bool isActive, bool isDisabled = false) => string.Empty;
    public string ToolbarSeparatorClass() => string.Empty;
    public string ToolbarGroupClass() => string.Empty;
    public string LinkClass() => string.Empty;
    public string ButtonClass(Sunfish.Foundation.Enums.ButtonVariant variant, Sunfish.Foundation.Enums.ButtonSize size, bool isOutline, bool isDisabled) => string.Empty;
    public string ButtonClass(Sunfish.Foundation.Enums.ButtonVariant variant, Sunfish.Foundation.Enums.ButtonSize size, Sunfish.Foundation.Enums.FillMode fillMode, Sunfish.Foundation.Enums.RoundedMode rounded, bool isDisabled) => string.Empty;
    public string IconButtonClass(Sunfish.Foundation.Enums.ButtonSize size) => string.Empty;
    public string ButtonGroupClass() => string.Empty;
    public string ToggleButtonClass(bool selected) => string.Empty;
    public string SplitButtonClass() => string.Empty;
    public string ChipClass(Sunfish.Foundation.Enums.ChipVariant variant, bool isSelected) => string.Empty;
    public string ChipSetClass() => string.Empty;
    public string FabClass(Sunfish.Foundation.Enums.FabSize size) => string.Empty;
    public string TextBoxClass(bool isInvalid, bool isDisabled) => string.Empty;
    public string TextAreaClass(bool isInvalid) => string.Empty;
    public string NumericInputClass() => string.Empty;
    public string SearchBoxClass() => string.Empty;
    public string AutocompleteClass() => string.Empty;
    public string AutocompleteClass(bool isOpen, bool isDisabled, bool isInvalid) => string.Empty;
    public string AutocompleteItemClass(bool isHighlighted, bool isSelected) => string.Empty;
    public string SelectClass(bool isInvalid) => string.Empty;
    public string CheckboxClass(bool isChecked) => string.Empty;
    public string RadioClass(bool isSelected) => string.Empty;
    public string RadioGroupClass() => string.Empty;
    public string SwitchClass(bool isOn) => string.Empty;
    public string SliderClass() => string.Empty;
    public string SliderClass(Sunfish.Foundation.Enums.SliderOrientation orientation) => string.Empty;
    public string RatingClass() => string.Empty;
    public string ColorPickerClass() => string.Empty;
    public string ColorPickerPopupClass() => string.Empty;
    public string ColorGradientClass() => string.Empty;
    public string ColorPaletteClass() => string.Empty;
    public string FlatColorPickerClass() => string.Empty;
    public string DatePickerClass() => string.Empty;
    public string TimePickerClass() => string.Empty;
    public string TimePickerPopupClass() => string.Empty;
    public string DateRangePickerClass() => string.Empty;
    public string DateRangePickerPopupClass() => string.Empty;
    public string DateTimePickerClass() => string.Empty;
    public string DateTimePickerPopupClass() => string.Empty;
    public string FileUploadClass() => string.Empty;
    public string FileUploadFileListClass() => string.Empty;
    public string FileUploadDropZoneClass(bool isDragOver, bool isDisabled) => string.Empty;
    public string DropDownListClass(bool isOpen, bool isDisabled, bool isInvalid) => string.Empty;
    public string DropDownListPopupClass() => string.Empty;
    public string DropDownListItemClass(bool isHighlighted, bool isSelected) => string.Empty;
    public string ComboBoxClass(bool isOpen, bool isDisabled, bool isInvalid) => string.Empty;
    public string ComboBoxPopupClass() => string.Empty;
    public string ComboBoxItemClass(bool isHighlighted, bool isSelected) => string.Empty;
    public string MultiSelectClass(bool isOpen, bool isDisabled, bool isInvalid) => string.Empty;
    public string MultiSelectPopupClass() => string.Empty;
    public string MultiSelectItemClass(bool isHighlighted, bool isSelected) => string.Empty;
    public string MultiSelectTagClass() => string.Empty;
    public string DropdownPopupClass() => string.Empty;
    public string FormClass() => string.Empty;
    public string FieldClass() => string.Empty;
    public string LabelClass() => string.Empty;
    public string InputGroupClass() => string.Empty;
    public string ValidationMessageClass(Sunfish.Foundation.Enums.ValidationSeverity severity) => string.Empty;
    public string CardClass() => string.Empty;
    public string CardHeaderClass() => string.Empty;
    public string CardBodyClass() => string.Empty;
    public string CardActionsClass() => string.Empty;
    public string CardFooterClass() => string.Empty;
    public string CardImageClass() => string.Empty;
    public string ListClass() => string.Empty;
    public string ListItemClass() => string.Empty;
    public string TableClass() => string.Empty;
    public string AvatarClass(Sunfish.Foundation.Enums.AvatarSize size) => string.Empty;
    public string BadgeClass(Sunfish.Foundation.Enums.BadgeVariant variant) => string.Empty;
    public string TooltipClass(Sunfish.Foundation.Enums.TooltipPosition position) => string.Empty;
    public string TooltipClass(Sunfish.Foundation.Enums.TooltipPosition position, Sunfish.Foundation.Enums.TooltipShowOn showOn) => string.Empty;
    public string PopoverClass() => string.Empty;
    public string TimelineClass() => string.Empty;
    public string TimelineItemClass() => string.Empty;
    public string CarouselClass() => string.Empty;
    public string TypographyClass(Sunfish.Foundation.Enums.TypographyVariant variant) => string.Empty;
    public string AlertClass(Sunfish.Foundation.Enums.AlertSeverity severity) => string.Empty;
    public string AlertStripClass() => string.Empty;
    public string ToastClass(Sunfish.Foundation.Enums.ToastSeverity severity) => string.Empty;
    public string SnackbarClass() => string.Empty;
    public string SnackbarClass(Sunfish.Foundation.Enums.NotificationVerticalPosition vertical, Sunfish.Foundation.Enums.NotificationHorizontalPosition horizontal) => string.Empty;
    public string SnackbarHostClass() => string.Empty;
    public string DialogClass() => string.Empty;
    public string DialogClass(bool isDraggable) => string.Empty;
    public string DialogOverlayClass() => string.Empty;
    public string ProgressBarClass() => string.Empty;
    public string ProgressCircleClass() => string.Empty;
    public string SpinnerClass(Sunfish.Foundation.Enums.SpinnerSize size) => string.Empty;
    public string SkeletonClass(Sunfish.Foundation.Enums.SkeletonVariant variant) => string.Empty;
    public string CalloutClass(Sunfish.Foundation.Enums.CalloutType type) => string.Empty;
    public string DataGridClass() => string.Empty;
    public string DataGridHeaderClass() => string.Empty;
    public string DataGridHeaderCellClass(bool isSortable, bool isSorted) => string.Empty;
    public string DataGridRowClass(bool isSelected, bool isStriped) => string.Empty;
    public string DataGridCellClass() => string.Empty;
    public string DataGridPagerClass() => string.Empty;
    public string DataGridToolbarClass() => string.Empty;
    public string DataGridFilterRowClass() => string.Empty;
    public string DataGridFilterCellClass() => string.Empty;
    public string DataGridGroupHeaderClass() => string.Empty;
    public string DataSheetClass(bool isLoading) => string.Empty;
    public string DataSheetCellClass(Sunfish.Foundation.Enums.CellState state, bool isActive, bool isEditable) => string.Empty;
    public string DataSheetHeaderCellClass(bool isSortable) => string.Empty;
    public string DataSheetRowClass(bool isDirty, bool isSelected, bool isDeleted) => string.Empty;
    public string DataSheetToolbarClass() => string.Empty;
    public string DataSheetBulkBarClass(bool isVisible) => string.Empty;
    public string DataSheetSaveFooterClass(int dirtyCount) => string.Empty;
    public string DataSheetAddButtonClass() => string.Empty;
    public string DataSheetSaveButtonClass() => string.Empty;
    public string DataSheetResetButtonClass() => string.Empty;
    public string DataSheetSpinnerClass() => string.Empty;
    public string DataSheetDirtyBadgeClass() => string.Empty;
    public string DataSheetSkeletonClass() => string.Empty;
    public string DataSheetSkeletonRowClass() => string.Empty;
    public string DataSheetSkeletonCellClass() => string.Empty;
    public string DataSheetLoadingTextClass() => string.Empty;
    public string DataSheetEmptyClass() => string.Empty;
    public string DataSheetSelectHeaderClass() => string.Empty;
    public string DataSheetActionsHeaderClass() => string.Empty;
    public string DataSheetAriaLiveClass() => string.Empty;
    public string DataSheetSelectCellClass() => string.Empty;
    public string DataSheetActionsCellClass() => string.Empty;
    public string DataSheetDeleteButtonClass() => string.Empty;
    public string DataSheetCellTextClass() => string.Empty;
    public string DataSheetEditorInputClass() => string.Empty;
    public string DataSheetEditorSelectClass() => string.Empty;
    public string DataSheetContentClass() => string.Empty;
    public string DataSheetScreenReaderOnlyClass() => string.Empty;
    public string ListViewClass() => string.Empty;
    public string ListViewItemClass(bool isSelected) => string.Empty;
    public string WindowClass(bool isModal) => string.Empty;
    public string WindowTitleBarClass() => string.Empty;
    public string WindowContentClass() => string.Empty;
    public string WindowActionsClass() => string.Empty;
    public string WindowOverlayClass() => string.Empty;
    public string WindowFooterClass() => string.Empty;
    public string EditorClass() => string.Empty;
    public string EditorToolbarClass() => string.Empty;
    public string EditorContentClass() => string.Empty;
    public string UploadClass() => string.Empty;
    public string UploadFileListClass() => string.Empty;
    public string UploadFileItemClass() => string.Empty;
    public string UploadDropZoneClass(bool isActive) => string.Empty;
    public string ChartContainerClass() => string.Empty;
    public string GaugeClass() => string.Empty;
    public string CalendarClass() => string.Empty;
    public string SchedulerClass() => string.Empty;
    public string AllocationSchedulerClass() => string.Empty;
    public string AllocationSchedulerToolbarClass() => string.Empty;
    public string AllocationSchedulerResourceColumnClass(bool isPinned) => string.Empty;
    public string AllocationSchedulerTimeHeaderClass(Sunfish.Foundation.Enums.TimeGranularity grain) => string.Empty;
    public string AllocationSchedulerRowClass(bool isSelected, bool isOverAllocated, bool isStriped = false) => string.Empty;
    public string AllocationSchedulerCellClass(bool isEditable, bool isSelected, bool isConflict, bool isDisabled, bool isDragTarget) => string.Empty;
    public string AllocationSchedulerCellValueClass(Sunfish.Foundation.Enums.AllocationValueMode mode) => string.Empty;
    public string AllocationSchedulerDeltaClass(Sunfish.Foundation.Enums.DeltaDisplayMode mode, bool isOver, bool isUnder) => string.Empty;
    public string AllocationSchedulerScenarioStripClass() => string.Empty;
    public string AllocationSchedulerScenarioChipClass(bool isActive, bool isLocked) => string.Empty;
    public string AllocationSchedulerGhostBarClass() => string.Empty;
    public string AllocationSchedulerContextMenuClass() => string.Empty;
    public string AllocationSchedulerEmptyClass() => string.Empty;
    public string AllocationSchedulerLoaderClass() => string.Empty;
    public string AllocationSchedulerSplitterClass(bool isDragging, bool isFocused) => string.Empty;
    public string AllocationSchedulerSplitterRestoreClass(Sunfish.Foundation.Enums.SplitterSide collapsedSide) => string.Empty;
    public string ModalClass(Sunfish.Foundation.Enums.ModalSize size) => string.Empty;
    public string ModalOverlayClass() => string.Empty;
    public string SignalRStatusClass(Sunfish.Foundation.Enums.AggregateConnectionState state, bool isCompact) => string.Empty;
    public string SignalRPopupClass() => string.Empty;
    public string SignalRRowClass(Sunfish.Foundation.Enums.ConnectionHealthState health) => string.Empty;
    public string SignalRBadgeClass(Sunfish.Foundation.Enums.ConnectionHealthState health) => string.Empty;
    public string ResizableContainerClass(bool isResizing, bool isDisabled) => string.Empty;
    public string ResizableContainerContentClass() => string.Empty;
    public string ResizableContainerHandleClass(Sunfish.Foundation.Enums.ResizeEdges edge, bool isActive, bool isFocused) => string.Empty;
    public string IconClass(string iconName, Sunfish.Foundation.Enums.IconSize size, Sunfish.Foundation.Enums.IconFlip flip = Sunfish.Foundation.Enums.IconFlip.None, Sunfish.Foundation.Enums.IconThemeColor themeColor = Sunfish.Foundation.Enums.IconThemeColor.Base) => string.Empty;
    public string DragDropClass() => string.Empty;
    public string DropZoneClass(bool isActive) => string.Empty;
    public string ScrollViewClass() => string.Empty;
}

internal sealed class StubIconProvider : ISunfishIconProvider
{
    public string GetIcon(string name, Sunfish.Foundation.Enums.IconSize size = Sunfish.Foundation.Enums.IconSize.Medium) => string.Empty;
    public string GetIconSpriteUrl() => string.Empty;
}
```

- [ ] **Step 3: Run failing test build (SunfishComponentBase doesn't exist yet)**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/tests/tests.csproj 2>&1 | tail -5
```

Expected: compile error referencing `SunfishComponentBase`.

- [ ] **Step 4: Create SunfishComponentBase.cs**

Create `packages/ui-adapters-blazor/Base/SunfishComponentBase.cs` by transforming `C:/Projects/Marilo/src/Marilo.Core/Base/MariloComponentBase.cs`:

Transformations:
- `using Marilo.Core.Contracts;` → `using Sunfish.UICore.Contracts;`
- `using Marilo.Core.Services;` → `using Sunfish.Foundation.Services;`
- `namespace Marilo.Core.Base;` → `namespace Sunfish.Components.Blazor.Base;`
- `MariloComponentBase` → `SunfishComponentBase`
- `IMariloCssProvider` → `ISunfishCssProvider`
- `IMariloIconProvider` → `ISunfishIconProvider`
- `IMariloThemeService` → `ISunfishThemeService`
- In doc comment: `"Marilo components"` → `"Sunfish components"`
- In `CombineClasses` doc comment: `IMariloCssProvider` → `ISunfishCssProvider`

Use Read + Edit tools (not sed) to make these changes precisely.

- [ ] **Step 5: Build and run tests**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
dotnet test packages/ui-adapters-blazor/tests/tests.csproj
```

Expected: 0 errors, 0 warnings. 3 tests passing.

- [ ] **Step 6: Stage and commit**

```bash
but stage "packages/ui-adapters-blazor/Base/SunfishComponentBase.cs" "feat/migration-phase3a-blazor-infra"
but stage "packages/ui-adapters-blazor/tests/tests.csproj" "feat/migration-phase3a-blazor-infra"
but stage "packages/ui-adapters-blazor/tests/SunfishComponentBaseTests.cs" "feat/migration-phase3a-blazor-infra"
but commit -m "feat(ui-adapters-blazor): add SunfishComponentBase with 3 passing bUnit tests" "feat/migration-phase3a-blazor-infra"
```

---

## Task 4: Migrate SunfishThemeProvider and DropdownPopup

**Files:**
- Create: `packages/ui-adapters-blazor/SunfishThemeProvider.razor`
- Create: `packages/ui-adapters-blazor/Internal/DropdownPopup.razor`
- Source: `C:/Projects/Marilo/src/Marilo.Components/MariloThemeProvider.razor`
- Source: `C:/Projects/Marilo/src/Marilo.Components/Internal/DropdownPopup.razor`

- [ ] **Step 1: Write the failing test**

Add `packages/ui-adapters-blazor/tests/SunfishThemeProviderTests.cs`:

```csharp
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;

namespace Sunfish.Components.Blazor.Tests;

public class SunfishThemeProviderTests : TestContext
{
    public SunfishThemeProviderTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
    }

    [Fact]
    public void SunfishThemeProvider_RendersChildContent()
    {
        var cut = RenderComponent<SunfishThemeProvider>(p => p
            .AddChildContent("<span>hello</span>"));

        Assert.Contains("hello", cut.Markup);
    }

    [Fact]
    public void SunfishThemeProvider_HasSfThemeProviderClass()
    {
        var cut = RenderComponent<SunfishThemeProvider>();
        Assert.Contains("sf-theme-provider", cut.Markup);
    }

    [Fact]
    public void SunfishThemeProvider_HasDataSfThemeAttribute()
    {
        var cut = RenderComponent<SunfishThemeProvider>();
        Assert.Contains("data-sf-theme", cut.Markup);
    }
}
```

- [ ] **Step 2: Run failing test build**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/tests/tests.csproj 2>&1 | tail -5
```

Expected: compile error (SunfishThemeProvider not found).

- [ ] **Step 3: Create SunfishThemeProvider.razor**

Create `packages/ui-adapters-blazor/SunfishThemeProvider.razor` from `C:/Projects/Marilo/src/Marilo.Components/MariloThemeProvider.razor` with these transformations:

- `@inherits MariloComponentBase` → `@inherits SunfishComponentBase`
- `class="@CombineClasses("marilo-theme-provider")"` → `class="@CombineClasses("sf-theme-provider")"`
- `data-marilo-theme` → `data-sf-theme`
- `MariloTheme` → `SunfishTheme`
- All `--marilo-*` CSS custom properties → `--sf-*` (e.g., `--marilo-color-primary` → `--sf-color-primary`)
- All `--marilo-font-*` → `--sf-font-*`
- All `--marilo-radius-*` → `--sf-radius-*`
- All `--marilo-shadow-*` → `--sf-shadow-*`
- `ThemeChangedEventArgs` — keep as-is (it lives in Foundation, already renamed in Phase 1)

Use Read + Edit (not sed) for accuracy.

- [ ] **Step 4: Create Internal/DropdownPopup.razor**

Create `packages/ui-adapters-blazor/Internal/DropdownPopup.razor` from `C:/Projects/Marilo/src/Marilo.Components/Internal/DropdownPopup.razor`:

- `@inherits MariloComponentBase` → `@inherits SunfishComponentBase`
- No other Marilo references in this file

- [ ] **Step 5: Build and run all tests**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
dotnet test packages/ui-adapters-blazor/tests/tests.csproj
```

Expected: 0 errors, 0 warnings. 6 tests passing (3 base + 3 theme provider).

- [ ] **Step 6: Stage and commit**

```bash
but stage "packages/ui-adapters-blazor/SunfishThemeProvider.razor" "feat/migration-phase3a-blazor-infra"
but stage "packages/ui-adapters-blazor/Internal/DropdownPopup.razor" "feat/migration-phase3a-blazor-infra"
but stage "packages/ui-adapters-blazor/tests/SunfishThemeProviderTests.cs" "feat/migration-phase3a-blazor-infra"
but commit -m "feat(ui-adapters-blazor): add SunfishThemeProvider, DropdownPopup; 6 tests green" "feat/migration-phase3a-blazor-infra"
```

---

## Task 5: Copy JS assets and register in solution

**Files:**
- Copy: `packages/ui-adapters-blazor/wwwroot/js/sunfish-*.js` (from Marilo `marilo-*.js`)
- Modify: `Sunfish.slnx`

- [ ] **Step 1: Create wwwroot/js and copy JS files**

```bash
mkdir -p "C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js"

# Copy all marilo-*.js files with sunfish- prefix
for f in "C:/Projects/Marilo/src/Marilo.Components/wwwroot/js/"marilo-*.js; do
  fname=$(basename "$f")
  newfname="${fname/marilo-/sunfish-}"
  cp "$f" "C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js/$newfname"
done

# Also copy non-prefixed JS files (e.g., allocation-scheduler.js)
for f in "C:/Projects/Marilo/src/Marilo.Components/wwwroot/js/"*.js; do
  fname=$(basename "$f")
  if [[ "$fname" != marilo-* ]]; then
    cp "$f" "C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js/$fname"
  fi
done

ls "C:/Projects/Sunfish/packages/ui-adapters-blazor/wwwroot/js/"
```

Note: JS content still uses `marilo-*` selectors internally — we will update JS content per-component in Phase 3b when each category is tested. For now the files just need to exist so the package builds.

- [ ] **Step 2: Build to verify JS files are included in the RCL**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj
```

Expected: 0 errors.

- [ ] **Step 3: Update Sunfish.slnx**

Edit `C:/Projects/Sunfish/Sunfish.slnx` to add the blazor adapter:

```xml
<Solution>
  <Folder Name="/foundation/">
    <Project Path="packages/foundation/Sunfish.Foundation.csproj" />
    <Project Path="packages/foundation/tests/tests.csproj" />
  </Folder>
  <Folder Name="/ui-core/">
    <Project Path="packages/ui-core/Sunfish.UICore.csproj" />
    <Project Path="packages/ui-core/tests/tests.csproj" />
  </Folder>
  <Folder Name="/ui-adapters-blazor/">
    <Project Path="packages/ui-adapters-blazor/Sunfish.Components.Blazor.csproj" />
    <Project Path="packages/ui-adapters-blazor/tests/tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 4: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. 22 tests (3 foundation + 13 ui-core + 6 blazor adapter).

- [ ] **Step 5: Stage and commit**

```bash
but stage "packages/ui-adapters-blazor/wwwroot/js/" "feat/migration-phase3a-blazor-infra"
but stage "Sunfish.slnx" "feat/migration-phase3a-blazor-infra"
but commit -m "feat(ui-adapters-blazor): add JS assets, register in Sunfish.slnx; 22 tests green" "feat/migration-phase3a-blazor-infra"
```

- [ ] **Step 6: Push branch**

```bash
git push origin feat/migration-phase3a-blazor-infra
```

---

## Self-Review Checklist

- [ ] `Sunfish.Components.Blazor.csproj` uses `Microsoft.NET.Sdk.Razor` (not plain `Microsoft.NET.Sdk`)
- [ ] `FrameworkReference Include="Microsoft.AspNetCore.App"` is present in the csproj
- [ ] `SunfishComponentBase.cs` has no `Marilo` references in code (doc comment guidance is OK)
- [ ] `SunfishThemeProvider.razor` uses `sf-theme-provider`, `data-sf-theme`, and `--sf-*` CSS vars
- [ ] No `--marilo-*` CSS variable names appear in `SunfishThemeProvider.razor`
- [ ] `Internal/DropdownPopup.razor` uses `@inherits SunfishComponentBase`
- [ ] `dotnet build Sunfish.slnx` = 0 warnings, 0 errors
- [ ] `dotnet test Sunfish.slnx` = 22 tests passing
