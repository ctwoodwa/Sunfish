# Phase 5: Domain Blocks (Forms, Tasks, Scheduling, Assets) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up four domain-specific Razor Class Libraries — `Sunfish.Blocks.Forms`, `Sunfish.Blocks.Tasks`, `Sunfish.Blocks.Scheduling`, `Sunfish.Blocks.Assets` — that compose the Blazor SunfishX components from Phase 3b into opinionated, pre-wired workflows. Each block ships with one canonical, end-to-end composition plus a smoke test, establishing the `blocks-*` package shape for future expansion.

**Architecture:** The `blocks-*` packages sit above `ui-adapters-blazor` in the dependency chain: `foundation → ui-core → ui-adapters-blazor → blocks-*`. Unlike `ui-core` (framework-agnostic contracts), blocks are **intentionally framework-coupled** to Blazor because they import and render Razor components from `Sunfish.Components.Blazor`. Each block is a narrow, opinionated composition layer — it picks specific Sunfish components and wires them together with domain semantics (form orchestration, task status flow, schedule views, asset catalog). Blocks are consumed by application code; they are NOT building blocks for `ui-core`.

**Tech Stack:** .NET 10, C# 13, Blazor (Razor Class Library), xUnit 2.9.x. bUnit is optional per block (smoke tests use type-existence pattern from Phase 3b until block surface areas stabilise).

---

## Scope

### In Scope

- Four new packages under `packages/blocks-*/`, each its own Razor Class Library:
  - `blocks-forms` — form orchestration block on top of `SunfishForm` + `SunfishValidation*`
  - `blocks-tasks` — task-board state-machine block composing `SunfishDataGrid` + `SunfishCard` (greenfield)
  - `blocks-scheduling` — schedule-view orchestration block wrapping `SunfishScheduler`, `SunfishAllocationScheduler`, `SunfishCalendar`
  - `blocks-assets` — asset-catalog block composing `SunfishDataGrid` + `SunfishFileManager` (greenfield)
- One canonical "block" component per package (`FormBlock`, `TaskBoardBlock`, `ScheduleViewBlock`, `AssetCatalogBlock`)
- Minimal C# state/service types per block (status enum, state holder, or orchestration service)
- One tests project per block with one or two smoke tests (type-existence pattern)
- `Sunfish.slnx` updated with all eight new csproj entries (four blocks + four tests)
- Optional: `IBlock` marker interface in `Sunfish.Foundation` if D-BLOCK-CONTRACT is accepted

### Out of Scope — Deferred Work

This phase is **scaffolding plus one canonical block per package**. The following are explicitly out of scope and belong to follow-up work:

- Multi-variant form blocks (wizard forms, stepper forms, multi-page forms, autosave, draft persistence, server-side validators, reCAPTCHA). `FormBlock` is a single-page validated-submit form only.
- Full task-management feature set (drag-between-columns, inline edit, assignment, comments, attachments, activity log, filters, saved views). `TaskBoardBlock` is read-display with a status-machine state holder; drag/drop wiring is deferred.
- Full scheduling suite (resource load-balancing, conflict resolution UI, recurring-event editor, scenario management, optimistic updates). `ScheduleViewBlock` is a container that selects one of Day/Week/Month/Allocation view based on a parameter.
- Asset pipeline (upload flow, transformation, thumbnail generation, metadata extraction, tagging, versioning, permissions). `AssetCatalogBlock` is a read-display grid plus `SunfishFileManager`.
- Kitchen-sink demos and docs pages for blocks — these land in follow-up phases that introduce `apps/kitchen-sink` and `apps/docs`.
- React adapter parity for blocks. Blocks are Blazor-only in Phase 5; React equivalents ship only after `ui-adapters-react` exists.

This restraint is deliberate (see D-SCOPE-RESTRAINT).

---

## Key Decisions

**D-BLOCK-ARCHITECTURE** — Each block is a Razor Class Library named `Sunfish.Blocks.<Domain>` containing one canonical block `.razor` component exposing a small parameter surface, plus optional supporting types (enums, state holders, orchestration services) under `<Domain>/` subfolders for public types or `Internal/` for package-private helpers. Block components inherit from `ComponentBase`, NOT from `SunfishComponentBase` — blocks are consumers of SunfishX components, not extensions of them. Blocks hard-wire layout choices, default parameters, and domain semantics; this is deliberately opinionated, unlike the unopinionated atomic components in `ui-adapters-blazor`.

**D-SCOPE-RESTRAINT** — Phase 5 is **infrastructure scaffolding plus one canonical composition per package** as proof-of-concept. Four fully-featured blocks would be weeks of design work each; that is not this phase. Deep features (drag-drop on boards, schedule scenario editors, asset tag facets, multi-step form wizards) are deferred. If a canonical block feels thin, that is by design — it is a seed, not a product.

**D-NAMESPACE-CONVENTION** — Root namespace per package is `Sunfish.Blocks.<Domain>`. Public supporting types live under non-prefixed subnamespaces (`Sunfish.Blocks.Forms.State`, `Sunfish.Blocks.Tasks.Models`). Package-private helpers live under `Sunfish.Blocks.<Domain>.Internal` (mirrors Phase 3a's `Internal/DropdownPopup.razor`). Assembly name matches root namespace.

**D-BLOCK-CONTRACT** — **Decision: skip the `IBlock` marker interface for Phase 5.** A marker would have to live in `Sunfish.Foundation` (so blocks don't all take a "blocks-core" dependency), but Foundation is framework-agnostic and blocks are Blazor-coupled by design. The only shape every block has in common is "it's a `ComponentBase`" — already a shared base. A marker adds a discoverability hook (`GetServices<IBlock>()`) that nothing in Phase 5 uses. Adding it now is speculative; revisit when a concrete consumer exists (kitchen-sink block gallery, scaffolding-CLI discovery command).

**D-TESTS** — Each block gets a tests project with one or two smoke tests using the type-existence pattern from Phase 3b (`FormsTests.cs` style). Block surface areas are not yet stable — deep bUnit tests would calcify decisions we want to revisit as blocks gain features. Upgrade to bUnit (add `bunit` + `Microsoft.NET.Sdk.Razor` as Phase 3a did) once a block's surface stabilises.

**D-MARILO-LIFT** — Research findings (see "Marilo Research Notes" section below):

| Package | Lift-worthy Marilo code? | Decision |
|---|---|---|
| `blocks-forms` | No. Marilo's `MariloForm.razor` is a component-level primitive; there is no `MariloFormStateService`, no validation engine, no form-orchestration layer in `Marilo.Core/Services/` or `Marilo.Core/BusinessLogic/`. | **Greenfield.** `FormBlock` wraps the already-migrated `SunfishForm` with domain defaults (spacing, validation-message layout, submit button). |
| `blocks-tasks` | No Marilo task component exists. PmDemo's `Board.razor` is 76 lines of hard-coded presentational markup with zero state machine or service. | **Greenfield.** Introduce a `TaskStatus` enum and `TaskBoardState<TItem>` holder. |
| `blocks-scheduling` | Partial. The Sunfish already-migrated `SunfishScheduler.razor` (669 lines in the Marilo source) contains the view-switching logic inline. No separate orchestration service exists in `Marilo.Core/Scheduling/` — only `RecurrenceParser.cs` (already migrated in Phase 1). PmDemo does not use the scheduler at all. | **Mostly greenfield.** `ScheduleViewBlock` picks between Day/Week/Month/Allocation views by a `ScheduleBlockView` enum parameter; the underlying components already hold view state. |
| `blocks-assets` | No Marilo asset component or service. `SunfishFileManager` exists but is a file-system browser, not an asset catalog. | **Greenfield.** Introduce an `AssetRecord` model and compose `SunfishDataGrid<AssetRecord>` plus `SunfishFileManager` in a split layout. |

**Summary: Phase 5 is almost entirely greenfield.** Only `blocks-scheduling` has any sibling Marilo code worth referencing, and even then the lift is conceptual (view-switching pattern) rather than a code move.

**D-SEQUENCE** — Execute strictly in order — forms first (smallest surface, clearest pattern), then tasks (greenfield baseline), then scheduling (largest — wraps multiple heavy components), then assets (greenfield final). Each block is its own sub-task group with its own commit. The forms block sets the csproj + folder template that the other three clone with minor edits. If the forms block takes unexpectedly long, pause and reassess the pattern before scaling it to the other three.

---

## File Structure

After Phase 5, the repo layout under `packages/` is:

```
packages/
  foundation/
    Sunfish.Foundation.csproj
    tests/tests.csproj
  ui-core/
    Sunfish.UICore.csproj
    tests/tests.csproj
  ui-adapters-blazor/
    Sunfish.Components.Blazor.csproj
    _Imports.razor
    SunfishThemeProvider.razor
    Base/
    Components/
    Internal/
    wwwroot/js/
    tests/tests.csproj
  blocks-forms/                                ← NEW
    Sunfish.Blocks.Forms.csproj
    _Imports.razor
    FormBlock.razor
    State/
      FormBlockState.cs
    Internal/
      (empty until features land)
    tests/
      tests.csproj
      FormBlockTests.cs
  blocks-tasks/                                ← NEW
    Sunfish.Blocks.Tasks.csproj
    _Imports.razor
    TaskBoardBlock.razor
    Models/
      TaskItem.cs
      TaskStatus.cs
    State/
      TaskBoardState.cs
    tests/
      tests.csproj
      TaskBoardBlockTests.cs
  blocks-scheduling/                           ← NEW
    Sunfish.Blocks.Scheduling.csproj
    _Imports.razor
    ScheduleViewBlock.razor
    Models/
      ScheduleBlockView.cs                     ← enum: Day/Week/Month/Allocation
    tests/
      tests.csproj
      ScheduleViewBlockTests.cs
  blocks-assets/                               ← NEW
    Sunfish.Blocks.Assets.csproj
    _Imports.razor
    AssetCatalogBlock.razor
    Models/
      AssetRecord.cs
    tests/
      tests.csproj
      AssetCatalogBlockTests.cs
```

Files to update:
- `Sunfish.slnx` — add four blocks + four tests csproj entries (eight new projects in total)

---

## Marilo Research Notes

Findings that anchor the greenfield claims in D-MARILO-LIFT:

- **`Marilo.Core/Services/`** — 9 files, none form-related. Grep for `FormStateService|ValidationService|FormState|FormOrchestrat` across `Marilo.Core` returns zero matches.
- **`Marilo.Core/BusinessLogic/`** — generic business-rule engine (`BusinessRuleEngine`, `BusinessObjectBase`, `FieldManager`, `UndoStack`). No form-specific orchestration. Already migrated in Phase 1. Wiring it into `FormBlock` is a follow-up.
- **`Marilo.Components/Forms/`** — two form components (183 + 228 lines). Both are presentational primitives, not orchestration. Already migrated to `Sunfish.Components.Blazor.Components.Forms.*` in Phase 3b. The `Containers/MariloForm.razor` variant is the richer primitive and is what `FormBlock` delegates to.
- **`Marilo.Components/Forms/Containers/MariloValidation.razor`** — 10-line pass-through to `ISunfishCssProvider.ValidationMessageClass(Severity)`. No orchestration.
- **`Marilo.Components/DataDisplay/Scheduler/`** — view classes only (`SchedulerDayView`, `SchedulerWeekView`, `SchedulerMonthView`, `SchedulerMultiDayView`, `SchedulerTimelineView`, `SchedulerAgendaView`, `SchedulerToolbar`, `SchedulerViewBase`, `ISchedulerViewHost`). Already migrated in Phase 3b. No orchestration layer.
- **`Marilo.Components/DataDisplay/AllocationScheduler/`** — 70 KB code-behind in a single razor component. Self-contained; Phase 5 wraps it, doesn't re-implement.
- **`samples/Marilo.PmDemo/`** — presentational only. `Board.razor` is 76 lines of hard-coded markup. Grep for `MariloScheduler|MariloAllocationScheduler|MariloCalendar` across PmDemo returns zero matches — scheduler components have no demo usage.
- **`Marilo.Core/Scheduling/`** — only `RecurrenceParser.cs`. Already migrated in Phase 1.

**Conclusion:** All four blocks are greenfield. `blocks-scheduling` references the existing view infrastructure but adds no Marilo-sourced orchestration code.

## External Design References

Per the research catalog in `docs/specifications/research-notes/external-references.md`, the following external systems inform the block designs (not adopted as dependencies — used as design references):

**For `blocks-forms` (Task 1):**
- **[Typeform AI](https://help.typeform.com/hc/en-us/articles/33777155298708-AI-with-Typeform-FAQ)** — AI-assisted form authoring is a 2026 baseline expectation. Task 1 ships a hand-authored `FormBlock`; AI-assisted schema generation is added to the parking lot (see below).
- **[Formstack Workflows](https://www.formstack.com/features/workflows)** — approval-chain composition model. When `blocks-forms` combines with `blocks-tasks` in a later phase, the composition UX should match Formstack's expressiveness.
- **[Feathery conditional logic catalog](https://docs.feathery.io/platform/build-forms/logic/available-conditions)** — baseline set of conditional-logic primitives (equal, contains, regex, numeric-compare, date-compare, is-empty, cross-field). `FormBlockState` rule support should cover at minimum this set; implementation via JSON Logic atop JSON Schema.

**For `blocks-tasks` (Task 2):**
- **[Pega case lifecycles and child cases](https://academy.pega.com/topic/child-cases/v5)** — the canonical reference for the parent/child-case vocabulary Sunfish tasks adopt. `TaskBoardState` models stages; future extension adds child-task relationships with inherited context and independent lifecycles (parent inspection → child deficiencies → child repairs). Phase 5 ships only the flat board; parent/child hierarchy is a parking-lot item.
- **[Temporal durable execution](https://temporal.io/blog/durable-execution-in-distributed-systems-increasing-observability)** — durable-execution pattern for long-running workflows. The initial `blocks-tasks` implementation uses in-memory state; **Temporal .NET SDK (`Temporalio.Sdk`)** is the primary candidate for durable-execution backend when tasks cross process boundaries or span days/weeks. Alternative: Dapr Workflows (tighter .NET-ecosystem integration), Elsa Workflows 3 (.NET-native designer), Microsoft DurableTask. Evaluation is parking-lot work.

**For `blocks-scheduling` (Task 3):** No new external references beyond those already informing `blocks-tasks`.

**For `blocks-assets` (Task 4):** No new external references.

---

## Task 0: Branch setup

- [ ] **Step 1: Create phase branch**

```bash
cd "C:/Projects/Sunfish"
git switch -c feat/migration-phase5-domain-blocks
```

Expected: new branch `feat/migration-phase5-domain-blocks` based on the current workspace branch (main or the post-phase-4 integration branch, whichever is canonical at execution time).

- [ ] **Step 2: Verify baseline build is green**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. All existing tests pass. Do NOT start Phase 5 work on a red baseline — fix the baseline first or rebase onto a known-green commit.

---

## Task 1: Create blocks-forms package

**Files:**
- Create: `packages/blocks-forms/Sunfish.Blocks.Forms.csproj`
- Create: `packages/blocks-forms/_Imports.razor`
- Create: `packages/blocks-forms/FormBlock.razor`
- Create: `packages/blocks-forms/State/FormBlockState.cs`
- Create: `packages/blocks-forms/tests/tests.csproj`
- Create: `packages/blocks-forms/tests/FormBlockTests.cs`

- [ ] **Step 1: Create Sunfish.Blocks.Forms.csproj**

```xml
<!-- packages/blocks-forms/Sunfish.Blocks.Forms.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Razor">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>Sunfish.Blocks.Forms</PackageId>
    <Description>Form orchestration block for Sunfish — opinionated composition over SunfishForm and SunfishValidation.</Description>
    <PackageTags>blazor;forms;blocks;domain;sunfish</PackageTags>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <NoWarn>CS1591</NoWarn>
  </PropertyGroup>
  <ItemGroup>
    <Compile Remove="tests/**/*.cs" />
  </ItemGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="Sunfish.Blocks.Forms.Tests" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
    <ProjectReference Include="..\ui-core\Sunfish.UICore.csproj" />
    <ProjectReference Include="..\ui-adapters-blazor\Sunfish.Components.Blazor.csproj" />
  </ItemGroup>
</Project>
```

Note: `Nullable` and `ImplicitUsings` are explicit (same rationale as Phase 3a — Razor SDK can skip inherited MSBuild props). `NoWarn>CS1591` suppresses missing-XML-doc warnings on block components until docs are written.

- [ ] **Step 2: Restore and verify baseline build**

```bash
cd "C:/Projects/Sunfish"
dotnet restore packages/blocks-forms/Sunfish.Blocks.Forms.csproj
dotnet build packages/blocks-forms/Sunfish.Blocks.Forms.csproj
```

Expected: 0 errors. No source files yet is fine.

- [ ] **Step 3: Create _Imports.razor**

```razor
@* packages/blocks-forms/_Imports.razor *@
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Web
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Models
@using Sunfish.Components.Blazor.Components.Forms.Containers
@using Sunfish.Components.Blazor.Components.Forms.Inputs
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.Blocks.Forms
@using Sunfish.Blocks.Forms.State
```

- [ ] **Step 4: Create FormBlockState.cs**

```csharp
// packages/blocks-forms/State/FormBlockState.cs
namespace Sunfish.Blocks.Forms.State;

/// <summary>
/// Tracks submission state for a <see cref="FormBlock"/> instance.
/// Updated by the block; exposed to consumers via OnStateChanged callback.
/// </summary>
public sealed class FormBlockState
{
    public bool IsSubmitting { get; internal set; }
    public bool HasSubmitted { get; internal set; }
    public bool LastSubmitWasValid { get; internal set; }
    public DateTime? LastSubmitAttemptUtc { get; internal set; }
}
```

Rationale: a minimal state holder gives consumers a read-only handle on submit lifecycle without them having to wire `EditContext` events themselves. `internal set` prevents external mutation — the block owns the state.

- [ ] **Step 5: Create canonical FormBlock.razor**

```razor
@* packages/blocks-forms/FormBlock.razor *@
@* FormBlock — opinionated one-page validated form.
   Wraps SunfishForm + SunfishValidation with sensible defaults:
   - vertical layout, single column
   - inline validation messages
   - default Submit button unless FormButtons slot is provided
   - exposes a FormBlockState via OnStateChanged for consumers
   Deep features (multi-page, autosave, wizard) are deferred. *@

@typeparam TModel where TModel : class

<SunfishForm Model="@Model"
             Id="@Id"
             Orientation="FormOrientation.Vertical"
             Columns="Columns"
             ValidationMessageType="FormValidationMessageType.Inline"
             OnValidSubmit="HandleValidSubmitAsync"
             OnInvalidSubmit="HandleInvalidSubmitAsync"
             FormValidation="@(@<SunfishValidation />)">
    <FormItems>
        @FormItems
    </FormItems>
    <FormButtons>
        @if (FormButtons is not null)
        {
            @FormButtons
        }
        else
        {
            <SunfishButton Variant="ButtonVariant.Primary"
                           Type="ButtonType.Submit"
                           Disabled="@State.IsSubmitting">
                @(SubmitText ?? "Submit")
            </SunfishButton>
        }
    </FormButtons>
</SunfishForm>

@code {
    /// <summary>The form's bound model.</summary>
    [Parameter, EditorRequired] public TModel Model { get; set; } = default!;

    /// <summary>Optional HTML id for external submit-button wiring.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Number of columns. Defaults to 1 (single-column vertical layout).</summary>
    [Parameter] public int Columns { get; set; } = 1;

    /// <summary>Text for the default submit button. Ignored when FormButtons slot is provided.</summary>
    [Parameter] public string? SubmitText { get; set; }

    /// <summary>Form item render fragment — typically a list of SunfishField rows.</summary>
    [Parameter] public RenderFragment? FormItems { get; set; }

    /// <summary>Optional custom action buttons. If null, a default Submit button renders.</summary>
    [Parameter] public RenderFragment? FormButtons { get; set; }

    /// <summary>Invoked after a successful validated submit. Block hands over the model.</summary>
    [Parameter] public EventCallback<TModel> OnSubmitted { get; set; }

    /// <summary>Invoked whenever internal state changes — read-only observation.</summary>
    [Parameter] public EventCallback<FormBlockState> OnStateChanged { get; set; }

    /// <summary>Read-only view of the block's submission state.</summary>
    public FormBlockState State { get; } = new();

    private async Task HandleValidSubmitAsync(EditContext ctx)
    {
        State.IsSubmitting = true;
        State.LastSubmitAttemptUtc = DateTime.UtcNow;
        await NotifyStateAsync();

        try
        {
            if (OnSubmitted.HasDelegate)
            {
                await OnSubmitted.InvokeAsync(Model);
            }
            State.LastSubmitWasValid = true;
            State.HasSubmitted = true;
        }
        finally
        {
            State.IsSubmitting = false;
            await NotifyStateAsync();
        }
    }

    private async Task HandleInvalidSubmitAsync(EditContext ctx)
    {
        State.LastSubmitAttemptUtc = DateTime.UtcNow;
        State.LastSubmitWasValid = false;
        State.HasSubmitted = true;
        await NotifyStateAsync();
    }

    private Task NotifyStateAsync()
        => OnStateChanged.HasDelegate ? OnStateChanged.InvokeAsync(State) : Task.CompletedTask;
}
```

Scope notes:
- The block picks `Orientation.Vertical`, `Columns=1`, `ValidationMessageType.Inline` as defaults. A consumer who needs horizontal layout or four-column grids writes their own block composition.
- No draft persistence, no autosave, no server-side validation callback. Those are follow-ups.
- The `@typeparam TModel where TModel : class` constraint is inherited from `EditContext` semantics (the underlying `SunfishForm` requires a reference-type model for `DataAnnotationsValidator`).

- [ ] **Step 6: Create tests project**

```xml
<!-- packages/blocks-forms/tests/tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <AssemblyName>Sunfish.Blocks.Forms.Tests</AssemblyName>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Sunfish.Blocks.Forms.csproj" />
  </ItemGroup>
</Project>
```

Note: uses plain `Microsoft.NET.Sdk` (not `Microsoft.NET.Sdk.Razor`) — smoke tests use type-existence only, no bUnit rendering. Upgrade to Razor SDK + bUnit when a block's surface stabilises (see D-TESTS).

- [ ] **Step 7: Create FormBlockTests.cs**

```csharp
// packages/blocks-forms/tests/FormBlockTests.cs
using Sunfish.Blocks.Forms;
using Sunfish.Blocks.Forms.State;
using Xunit;

namespace Sunfish.Blocks.Forms.Tests;

public class FormBlockTests
{
    [Fact]
    public void FormBlock_TypeIsPublicAndInBlocksFormsNamespace()
    {
        var type = typeof(FormBlock<>);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Forms", type.Namespace);
    }

    [Fact]
    public void FormBlockState_TypeIsPublicAndInStateNamespace()
    {
        var type = typeof(FormBlockState);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Forms.State", type.Namespace);
    }
}
```

Two tests (one for the generic component type, one for the state holder) are sufficient smoke coverage for the block shape.

- [ ] **Step 8: Build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/blocks-forms/Sunfish.Blocks.Forms.csproj
dotnet build packages/blocks-forms/tests/tests.csproj
dotnet test packages/blocks-forms/tests/tests.csproj
```

Expected: 0 errors, 0 warnings. 2 tests passing.

- [ ] **Step 9: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/blocks-forms/
git commit -m "feat(blocks-forms): add Sunfish.Blocks.Forms with canonical FormBlock; 2 smoke tests"
```

---

## Task 2: Create blocks-tasks package (greenfield)

**Files:**
- Create: `packages/blocks-tasks/Sunfish.Blocks.Tasks.csproj`
- Create: `packages/blocks-tasks/_Imports.razor`
- Create: `packages/blocks-tasks/Models/TaskItem.cs`
- Create: `packages/blocks-tasks/Models/TaskStatus.cs`
- Create: `packages/blocks-tasks/State/TaskBoardState.cs`
- Create: `packages/blocks-tasks/TaskBoardBlock.razor`
- Create: `packages/blocks-tasks/tests/tests.csproj`
- Create: `packages/blocks-tasks/tests/TaskBoardBlockTests.cs`

- [ ] **Step 1: Create Sunfish.Blocks.Tasks.csproj**

Clone the `blocks-forms` csproj with these substitutions:
- `PackageId` → `Sunfish.Blocks.Tasks`
- `Description` → "Task-board state-machine block for Sunfish — opinionated composition over SunfishDataGrid and SunfishCard."
- `PackageTags` → `blazor;tasks;kanban;blocks;domain;sunfish`
- `InternalsVisibleTo Include` → `Sunfish.Blocks.Tasks.Tests`

ProjectReferences identical (foundation, ui-core, ui-adapters-blazor).

- [ ] **Step 2: Verify baseline build**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/blocks-tasks/Sunfish.Blocks.Tasks.csproj
```

- [ ] **Step 3: Create _Imports.razor**

```razor
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
@using Sunfish.Foundation.Enums
@using Sunfish.Components.Blazor.Components.DataDisplay
@using Sunfish.Components.Blazor.Components.Buttons
@using Sunfish.Blocks.Tasks
@using Sunfish.Blocks.Tasks.Models
@using Sunfish.Blocks.Tasks.State
```

Note: `TaskStatus` is intentionally NOT added as a top-level `@using` because System already has `System.Threading.Tasks` in scope and we don't want ambiguity with `System.Threading.Tasks.TaskStatus`. Consumers reference `Sunfish.Blocks.Tasks.Models.TaskStatus` explicitly.

- [ ] **Step 4: Create Models/TaskStatus.cs**

```csharp
// packages/blocks-tasks/Models/TaskStatus.cs
namespace Sunfish.Blocks.Tasks.Models;

/// <summary>
/// Canonical task lifecycle states. Hard-coded for the canonical board.
/// Follow-up work: make this extensible via a registry or allow consumer-defined enums.
/// </summary>
public enum TaskStatus
{
    Backlog,
    Todo,
    InProgress,
    Done
}
```

Parking-lot: extensibility. A real task system needs consumer-definable columns. Phase 5 hard-codes four canonical states — consumers who need custom columns will fork this enum in their own composition.

- [ ] **Step 5: Create Models/TaskItem.cs**

```csharp
// packages/blocks-tasks/Models/TaskItem.cs
namespace Sunfish.Blocks.Tasks.Models;

/// <summary>
/// Canonical task record. Intentionally thin — consumers can wrap their own domain
/// model via the TItem generic on TaskBoardBlock, or use this record directly.
/// </summary>
public sealed record TaskItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required TaskStatus Status { get; init; }
    public string? Assignee { get; init; }
    public DateTime? DueDateUtc { get; init; }
}
```

- [ ] **Step 6: Create State/TaskBoardState.cs**

```csharp
// packages/blocks-tasks/State/TaskBoardState.cs
using Sunfish.Blocks.Tasks.Models;

namespace Sunfish.Blocks.Tasks.State;

/// <summary>
/// Status-machine state holder. Validates transitions through the canonical lifecycle:
/// Backlog → Todo → InProgress → Done (with Todo ↔ InProgress permitted).
/// Returns false and leaves state untouched for invalid transitions.
/// </summary>
public sealed class TaskBoardState
{
    public bool TryTransition(TaskItem item, TaskStatus target, out TaskItem updated)
    {
        if (!IsValid(item.Status, target))
        {
            updated = item;
            return false;
        }
        updated = item with { Status = target };
        return true;
    }

    internal static bool IsValid(TaskStatus from, TaskStatus to) => (from, to) switch
    {
        (TaskStatus.Backlog,    TaskStatus.Todo)       => true,
        (TaskStatus.Todo,       TaskStatus.InProgress) => true,
        (TaskStatus.InProgress, TaskStatus.Done)       => true,
        (TaskStatus.InProgress, TaskStatus.Todo)       => true,  // revert
        (TaskStatus.Todo,       TaskStatus.Backlog)    => true,  // revert
        _ when from == to                              => true,
        _                                              => false,
    };
}
```

Scope notes: this is the smallest status machine that demonstrates "block = composition + state + rules." Drag/drop wiring, server persistence, undo, and optimistic updates are deferred.

- [ ] **Step 7: Create TaskBoardBlock.razor**

```razor
@* packages/blocks-tasks/TaskBoardBlock.razor *@
@* Canonical Kanban-style board. Read-display: groups items by status into columns.
   Drag/drop between columns is deferred to follow-up work. *@

<div class="sf-task-board" style="display:grid;grid-template-columns:repeat(4,1fr);gap:16px;align-items:start;">
    @foreach (var status in System.Enum.GetValues<TaskStatus>())
    {
        var itemsInStatus = Items?.Where(i => i.Status == status).ToArray() ?? System.Array.Empty<TaskItem>();
        <div class="sf-task-board__column">
            <div class="sf-task-board__column-header" style="font-size:13px;font-weight:600;text-transform:uppercase;color:#6b7280;margin-bottom:10px;">
                @status (@itemsInStatus.Length)
            </div>
            <div class="sf-task-board__column-body" style="display:flex;flex-direction:column;gap:10px;">
                @foreach (var item in itemsInStatus)
                {
                    @if (ItemTemplate is not null)
                    {
                        @ItemTemplate(item)
                    }
                    else
                    {
                        <div class="sf-task-board__card" style="padding:12px;border:1px solid #e5e7eb;border-radius:6px;background:#fff;">
                            <div style="font-weight:500;margin-bottom:6px;">@item.Title</div>
                            @if (!string.IsNullOrEmpty(item.Assignee))
                            {
                                <div style="font-size:12px;color:#6b7280;">@item.Assignee</div>
                            }
                        </div>
                    }
                }
            </div>
        </div>
    }
</div>

@code {
    /// <summary>Task items to display. Grouped by <c>Status</c> into four columns.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<TaskItem> Items { get; set; } = System.Array.Empty<TaskItem>();

    /// <summary>Optional per-item template. Falls back to a minimal card if null.</summary>
    [Parameter] public RenderFragment<TaskItem>? ItemTemplate { get; set; }
}
```

Note: this skeleton intentionally uses inline styles for the canonical composition (same pattern as PmDemo's `Board.razor`) — a proper class-based skin is a follow-up that can land once blocks get a kitchen-sink demo.

Note: the canonical `TaskBoardBlock` does NOT currently use `SunfishDataGrid` — a Kanban board is a column-of-cards layout, not a grid. The plan originally called for DataGrid + Card composition; after design review, Card-only is the correct canonical shape for a kanban board. If a list-view variant is needed, that is a second block (`TaskListBlock`) in follow-up work.

- [ ] **Step 8: Create tests project**

Clone `blocks-forms/tests/tests.csproj` with `AssemblyName` → `Sunfish.Blocks.Tasks.Tests` and ProjectReference → `..\Sunfish.Blocks.Tasks.csproj`.

- [ ] **Step 9: Create TaskBoardBlockTests.cs**

```csharp
using Sunfish.Blocks.Tasks;
using Sunfish.Blocks.Tasks.Models;
using Sunfish.Blocks.Tasks.State;
using Xunit;

namespace Sunfish.Blocks.Tasks.Tests;

public class TaskBoardBlockTests
{
    [Fact]
    public void TaskBoardBlock_TypeIsPublicAndInBlocksTasksNamespace()
    {
        var type = typeof(TaskBoardBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Tasks", type.Namespace);
    }

    [Fact]
    public void TaskBoardState_AllowsValidForwardTransition()
    {
        var state = new TaskBoardState();
        var item = new TaskItem { Id = "1", Title = "x", Status = TaskStatus.Todo };

        Assert.True(state.TryTransition(item, TaskStatus.InProgress, out var updated));
        Assert.Equal(TaskStatus.InProgress, updated.Status);
    }

    [Fact]
    public void TaskBoardState_RejectsInvalidSkipTransition()
    {
        var state = new TaskBoardState();
        var item = new TaskItem { Id = "1", Title = "x", Status = TaskStatus.Backlog };

        Assert.False(state.TryTransition(item, TaskStatus.Done, out var updated));
        Assert.Equal(TaskStatus.Backlog, updated.Status);
    }
}
```

Three tests — type existence plus two state-machine behaviour tests. The state machine is worth covering because it's the only piece of domain logic in the block; presentational markup stays type-existence-only per D-TESTS.

- [ ] **Step 10: Build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/blocks-tasks/Sunfish.Blocks.Tasks.csproj
dotnet test packages/blocks-tasks/tests/tests.csproj
```

Expected: 0 errors, 3 tests passing.

- [ ] **Step 11: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/blocks-tasks/
git commit -m "feat(blocks-tasks): add Sunfish.Blocks.Tasks with TaskBoardBlock + status machine; 3 tests"
```

---

## Task 3: Create blocks-scheduling package

**Files:**
- Create: `packages/blocks-scheduling/Sunfish.Blocks.Scheduling.csproj`
- Create: `packages/blocks-scheduling/_Imports.razor`
- Create: `packages/blocks-scheduling/Models/ScheduleBlockView.cs`
- Create: `packages/blocks-scheduling/ScheduleViewBlock.razor`
- Create: `packages/blocks-scheduling/tests/tests.csproj`
- Create: `packages/blocks-scheduling/tests/ScheduleViewBlockTests.cs`

- [ ] **Step 1: Create csproj**

Clone `blocks-forms` csproj with:
- `PackageId` → `Sunfish.Blocks.Scheduling`
- `Description` → "Schedule-view orchestration block for Sunfish — wraps SunfishScheduler, SunfishAllocationScheduler, SunfishCalendar into a single view-switcher."
- `PackageTags` → `blazor;scheduling;calendar;blocks;domain;sunfish`
- `InternalsVisibleTo` → `Sunfish.Blocks.Scheduling.Tests`

- [ ] **Step 2: Verify baseline build**

```bash
dotnet build packages/blocks-scheduling/Sunfish.Blocks.Scheduling.csproj
```

- [ ] **Step 3: Create _Imports.razor**

```razor
@using Microsoft.AspNetCore.Components
@using Sunfish.Foundation.Enums
@using Sunfish.Foundation.Models
@using Sunfish.Components.Blazor.Components.DataDisplay
@using Sunfish.Components.Blazor.Components.DataDisplay.Scheduler
@using Sunfish.Components.Blazor.Components.DataDisplay.AllocationScheduler
@using Sunfish.Blocks.Scheduling
@using Sunfish.Blocks.Scheduling.Models
```

Note: verify the `Scheduler` and `AllocationScheduler` sub-namespaces at execution time — Phase 3b may have placed the migrated view classes under slightly different sub-namespaces. If so, adjust the `@using` list accordingly and update this plan with a post-hoc note.

- [ ] **Step 4: Create Models/ScheduleBlockView.cs**

```csharp
namespace Sunfish.Blocks.Scheduling.Models;

/// <summary>
/// View mode for <see cref="Sunfish.Blocks.Scheduling.ScheduleViewBlock"/>.
/// Follow-up: add Day-agenda, timeline, and resource-timeline modes.
/// </summary>
public enum ScheduleBlockView
{
    Day,
    Week,
    Month,
    Allocation
}
```

- [ ] **Step 5: Create ScheduleViewBlock.razor**

```razor
@* packages/blocks-scheduling/ScheduleViewBlock.razor *@
@* Picks between SunfishScheduler (Day/Week/Month) and SunfishAllocationScheduler
   based on the View parameter. Heavy lifting is delegated to the underlying
   components — this block is a thin view-switcher with opinionated defaults. *@

@switch (View)
{
    case ScheduleBlockView.Allocation:
        <SunfishAllocationScheduler />
        break;

    case ScheduleBlockView.Day:
    case ScheduleBlockView.Week:
    case ScheduleBlockView.Month:
    default:
        @* SunfishScheduler handles Day/Week/Month internally; we pass View via SchedulerView
           and let the component route to the right view class. Parameter binding may require
           adjustment once the SunfishScheduler parameter names are verified at execution time. *@
        <SunfishScheduler @attributes="ForwardedAttributes" />
        break;
}

@code {
    /// <summary>Selects which underlying scheduler component to render.</summary>
    [Parameter] public ScheduleBlockView View { get; set; } = ScheduleBlockView.Week;

    /// <summary>Pass-through attributes forwarded to the underlying scheduler component.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? ForwardedAttributes { get; set; }
}
```

Parking-lot: the exact parameter surface of `SunfishScheduler` (after Phase 3b rename) is not re-verified here. When executing this task, read `packages/ui-adapters-blazor/Components/DataDisplay/SunfishScheduler.razor` and pick a small forward-surface (e.g., `CurrentDate`, `OnDateChange`, `Events`) to wire explicitly instead of relying entirely on `@attributes` splatting. For Phase 5, `CaptureUnmatchedValues` is a pragmatic placeholder.

Note on Calendar: `SunfishCalendar` is a separate component with a simpler "month picker" surface, not a schedule view — we do NOT route to it from `ScheduleViewBlock`. A follow-up `CalendarBlock` might exist in blocks-scheduling; deferred.

- [ ] **Step 6: Create tests project**

Clone `blocks-forms/tests/tests.csproj` with `AssemblyName` → `Sunfish.Blocks.Scheduling.Tests` and ProjectReference → `..\Sunfish.Blocks.Scheduling.csproj`.

- [ ] **Step 7: Create ScheduleViewBlockTests.cs**

```csharp
using Sunfish.Blocks.Scheduling;
using Sunfish.Blocks.Scheduling.Models;
using Xunit;

namespace Sunfish.Blocks.Scheduling.Tests;

public class ScheduleViewBlockTests
{
    [Fact]
    public void ScheduleViewBlock_TypeIsPublicAndInBlocksSchedulingNamespace()
    {
        var type = typeof(ScheduleViewBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Scheduling", type.Namespace);
    }

    [Fact]
    public void ScheduleBlockView_HasFourCanonicalModes()
    {
        var values = System.Enum.GetNames<ScheduleBlockView>();
        Assert.Contains("Day", values);
        Assert.Contains("Week", values);
        Assert.Contains("Month", values);
        Assert.Contains("Allocation", values);
    }
}
```

- [ ] **Step 8: Build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/blocks-scheduling/Sunfish.Blocks.Scheduling.csproj
dotnet test packages/blocks-scheduling/tests/tests.csproj
```

Expected: 0 errors, 2 tests passing.

Note on warnings: if `SunfishScheduler` or `SunfishAllocationScheduler` have required parameters without defaults, the Razor compiler may emit warnings at the usage site. Resolve by supplying minimal default parameter values in the block or by switching to conditional rendering with null guards. Do not suppress with `#pragma warning disable` — fix the root cause.

- [ ] **Step 9: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/blocks-scheduling/
git commit -m "feat(blocks-scheduling): add Sunfish.Blocks.Scheduling with ScheduleViewBlock view-switcher; 2 tests"
```

---

## Task 4: Create blocks-assets package (greenfield)

**Files:**
- Create: `packages/blocks-assets/Sunfish.Blocks.Assets.csproj`
- Create: `packages/blocks-assets/_Imports.razor`
- Create: `packages/blocks-assets/Models/AssetRecord.cs`
- Create: `packages/blocks-assets/AssetCatalogBlock.razor`
- Create: `packages/blocks-assets/tests/tests.csproj`
- Create: `packages/blocks-assets/tests/AssetCatalogBlockTests.cs`

- [ ] **Step 1: Create csproj**

Clone `blocks-forms` csproj with:
- `PackageId` → `Sunfish.Blocks.Assets`
- `Description` → "Asset-catalog block for Sunfish — composes SunfishDataGrid and SunfishFileManager for a read-display asset view."
- `PackageTags` → `blazor;assets;catalog;blocks;domain;sunfish`
- `InternalsVisibleTo` → `Sunfish.Blocks.Assets.Tests`

- [ ] **Step 2: Verify baseline build**

```bash
dotnet build packages/blocks-assets/Sunfish.Blocks.Assets.csproj
```

- [ ] **Step 3: Create _Imports.razor**

```razor
@using Microsoft.AspNetCore.Components
@using Sunfish.Foundation.Enums
@using Sunfish.Components.Blazor.Components.DataGrid
@using Sunfish.Components.Blazor.Components.Editors
@using Sunfish.Blocks.Assets
@using Sunfish.Blocks.Assets.Models
```

Note: `SunfishFileManager` lives in `Components.Editors` — verify at execution time; Phase 3b placement of FileManager is under Editors but double-check before committing.

- [ ] **Step 4: Create Models/AssetRecord.cs**

```csharp
namespace Sunfish.Blocks.Assets.Models;

/// <summary>
/// Canonical asset record. Kept thin — real asset systems have much richer metadata
/// (tags, versions, mime types, checksums, permissions); all deferred.
/// </summary>
public sealed record AssetRecord
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Path { get; init; }
    public long SizeBytes { get; init; }
    public DateTime? LastModifiedUtc { get; init; }
}
```

- [ ] **Step 5: Create AssetCatalogBlock.razor**

```razor
@* packages/blocks-assets/AssetCatalogBlock.razor *@
@* Split layout: DataGrid of asset records on the left, SunfishFileManager on the right.
   Read-display only — upload, transform, tag, version are all deferred. *@

<div class="sf-asset-catalog" style="display:grid;grid-template-columns:2fr 1fr;gap:16px;height:100%;">
    <div class="sf-asset-catalog__grid">
        <SunfishDataGrid Data="Assets" TItem="AssetRecord">
            @* Columns hardcoded for the canonical shape; consumers override with custom ChildContent in follow-up. *@
        </SunfishDataGrid>
    </div>
    <div class="sf-asset-catalog__files">
        @if (ShowFileManager)
        {
            <SunfishFileManager />
        }
    </div>
</div>

@code {
    /// <summary>Asset records to list in the grid.</summary>
    [Parameter, EditorRequired] public IReadOnlyList<AssetRecord> Assets { get; set; } = System.Array.Empty<AssetRecord>();

    /// <summary>Whether to show the file-manager pane. Defaults to true.</summary>
    [Parameter] public bool ShowFileManager { get; set; } = true;
}
```

Parking-lot: the exact `SunfishDataGrid` column-definition shape and `SunfishFileManager` parameter surface are NOT re-verified here. At execution time, open both files and wire concrete columns and a root-directory parameter so the block actually renders meaningful content. For the plan, the skeleton shape is what matters.

- [ ] **Step 6: Create tests project**

Clone `blocks-forms/tests/tests.csproj` with `AssemblyName` → `Sunfish.Blocks.Assets.Tests` and ProjectReference → `..\Sunfish.Blocks.Assets.csproj`.

- [ ] **Step 7: Create AssetCatalogBlockTests.cs**

```csharp
using Sunfish.Blocks.Assets;
using Sunfish.Blocks.Assets.Models;
using Xunit;

namespace Sunfish.Blocks.Assets.Tests;

public class AssetCatalogBlockTests
{
    [Fact]
    public void AssetCatalogBlock_TypeIsPublicAndInBlocksAssetsNamespace()
    {
        var type = typeof(AssetCatalogBlock);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Assets", type.Namespace);
    }

    [Fact]
    public void AssetRecord_TypeIsPublicAndInModelsNamespace()
    {
        var type = typeof(AssetRecord);
        Assert.True(type.IsPublic);
        Assert.Equal("Sunfish.Blocks.Assets.Models", type.Namespace);
    }
}
```

- [ ] **Step 8: Build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build packages/blocks-assets/Sunfish.Blocks.Assets.csproj
dotnet test packages/blocks-assets/tests/tests.csproj
```

Expected: 0 errors, 2 tests passing.

- [ ] **Step 9: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add packages/blocks-assets/
git commit -m "feat(blocks-assets): add Sunfish.Blocks.Assets with AssetCatalogBlock; 2 tests"
```

---

## Task 5: Register blocks in Sunfish.slnx

- [ ] **Step 1: Update Sunfish.slnx**

Edit `C:/Projects/Sunfish/Sunfish.slnx` to add the four new solution folders. Full expected file after edit:

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
  <Folder Name="/blocks-forms/">
    <Project Path="packages/blocks-forms/Sunfish.Blocks.Forms.csproj" />
    <Project Path="packages/blocks-forms/tests/tests.csproj" />
  </Folder>
  <Folder Name="/blocks-tasks/">
    <Project Path="packages/blocks-tasks/Sunfish.Blocks.Tasks.csproj" />
    <Project Path="packages/blocks-tasks/tests/tests.csproj" />
  </Folder>
  <Folder Name="/blocks-scheduling/">
    <Project Path="packages/blocks-scheduling/Sunfish.Blocks.Scheduling.csproj" />
    <Project Path="packages/blocks-scheduling/tests/tests.csproj" />
  </Folder>
  <Folder Name="/blocks-assets/">
    <Project Path="packages/blocks-assets/Sunfish.Blocks.Assets.csproj" />
    <Project Path="packages/blocks-assets/tests/tests.csproj" />
  </Folder>
</Solution>
```

- [ ] **Step 2: Full solution build and test**

```bash
cd "C:/Projects/Sunfish"
dotnet build Sunfish.slnx
dotnet test Sunfish.slnx --no-build
```

Expected: 0 errors, 0 warnings. Test counts:
- Phase 1–4 baseline: whatever was green pre-Phase-5.
- Phase 5 additions: 2 (forms) + 3 (tasks) + 2 (scheduling) + 2 (assets) = **9 new tests.**

If any block's tests fail to discover, verify the test csproj uses the plain `Microsoft.NET.Sdk` and includes `xunit.runner.visualstudio`.

- [ ] **Step 3: Stage and commit**

```bash
cd "C:/Projects/Sunfish"
git add Sunfish.slnx
git commit -m "feat(blocks): register four blocks-* projects in Sunfish.slnx; full build green"
```

- [ ] **Step 4: Push branch**

```bash
git push origin feat/migration-phase5-domain-blocks
```

---

## Self-Review Checklist

**Per-block structural checks (apply to all four blocks):**

- [ ] csproj uses `Microsoft.NET.Sdk.Razor`, has `<FrameworkReference Include="Microsoft.AspNetCore.App" />`, ProjectReferences to foundation + ui-core + ui-adapters-blazor, `InternalsVisibleTo` for the tests project, and explicit `<Nullable>enable</Nullable>` / `<ImplicitUsings>enable</ImplicitUsings>`.
- [ ] `tests/tests.csproj` uses plain `Microsoft.NET.Sdk`, has explicit `AssemblyName` matching `Sunfish.Blocks.<Domain>.Tests`, and does NOT reference bUnit (type-existence pattern per D-TESTS).
- [ ] `_Imports.razor` includes `Microsoft.AspNetCore.Components` plus the ui-adapters-blazor sub-namespaces the block consumes.

**Namespace conventions (D-NAMESPACE-CONVENTION):**

- [ ] Each block component is in `Sunfish.Blocks.<Domain>`.
- [ ] Models are under `Sunfish.Blocks.<Domain>.Models`; state holders under `Sunfish.Blocks.<Domain>.State`.
- [ ] `Sunfish.Blocks.Tasks.Models.TaskStatus` is referenced with fully qualified name where needed to avoid ambiguity with `System.Threading.Tasks.TaskStatus`.

**Scope restraint (D-SCOPE-RESTRAINT):**

- [ ] No block adds more than ONE canonical composition component.
- [ ] No block ships drag-drop, autosave, server persistence, or multi-step flows.
- [ ] No block references React or `ui-adapters-react` (Blazor-only in Phase 5).
- [ ] No block depends on a sibling block — blocks are independent peer packages.

**Marker interface (D-BLOCK-CONTRACT):**

- [ ] No `IBlock` interface added to `Sunfish.Foundation` and no block implements such a marker.

**Build and tests:**

- [ ] `dotnet build Sunfish.slnx` = 0 errors, 0 warnings.
- [ ] `dotnet test Sunfish.slnx` = all prior tests PLUS 9 new tests (2+3+2+2).
- [ ] No test project references bUnit — all smoke tests use `typeof(...)` + namespace assertions.

**Git hygiene:**

- [ ] 5 commits total on the phase branch (one per block + Sunfish.slnx registration).
- [ ] Branch name is `feat/migration-phase5-domain-blocks`.
- [ ] No commits touch files outside `packages/blocks-*/` or `Sunfish.slnx`.

---

## Parking Lot — Follow-up Phases

Tracked here so future phases don't lose sight of deferred work:

1. **Block discoverability** — `IBlock` marker interface in Foundation, driven by a concrete consumer (kitchen-sink block gallery or a scaffolding-CLI command).
2. **Drag/drop on TaskBoardBlock** — once Sunfish ships a DragDrop primitive in `ui-adapters-blazor` that cleanly supports inter-column moves.
3. **Form draft persistence** — a `FormBlockDraftStore` service with localStorage / IndexedDB / server backends.
4. **Scheduling scenario editor** — wraps the existing `AllocationScheduler` scenario-strip component into a first-class block.
5. **Asset upload pipeline** — a second `blocks-assets` component (`AssetUploaderBlock`) that composes `SunfishFileUpload` + metadata form.
6. **React adapter parity** — `blocks-react-*` equivalents once `ui-adapters-react` exists. Requires generalising D-BLOCK-ARCHITECTURE away from `Razor Class Library`.
7. **Kitchen-sink demos** — one page per block in `apps/kitchen-sink` once that app is introduced.
8. **Block versioning policy** — how blocks version relative to ui-adapters-blazor (they depend on it, so semver cascading rules need nailing down).
9. **AI-assisted form authoring for `blocks-forms`** — Typeform-AI-style flow: user describes a form in natural language; system drafts a JSON Schema + layout. Depends on an LLM integration point in Sunfish; out of scope for initial block.
10. **Form+workflow composition UX** — Formstack-Workflows-style editor for combining `blocks-forms` + `blocks-tasks` into approval chains with branching, parallelism, and escalation. Requires the `blocks-tasks` runtime to mature first.
11. **Parent/child task hierarchy in `blocks-tasks`** — Pega-style child-case model: a parent inspection task spawns child deficiency tasks with inherited context and independent lifecycles. Phase 5 ships only the flat board.
12. **Durable-execution backend for `blocks-tasks`** — evaluation between Temporal (`Temporalio.Sdk`), Dapr Workflows, Elsa Workflows 3, Microsoft DurableTask. Needed when tasks cross process boundaries or span long timescales. Initial Phase 5 uses in-memory state.

Each of these deserves its own intake ticket at the time it's picked up — don't let them creep into Phase 5.
