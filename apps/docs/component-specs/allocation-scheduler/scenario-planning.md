---
title: Scenario Planning
page_title: AllocationScheduler Scenario Planning
description: How to use baseline and scenario sets in the AllocationScheduler component for Blazor — create named what-if plans, compare them, and promote a winner to the committed baseline.
slug: allocation-scheduler-scenario-planning
tags: sunfish,blazor,allocation-scheduler,scenario-planning,baseline,what-if
published: True
position: 4
components: ["allocation-scheduler"]
---

# AllocationScheduler Scenario Planning

The AllocationScheduler supports **named allocation sets** so planners can model what-if alternatives without disturbing the committed plan. A *baseline* is the locked, authoritative set of allocations. A *scenario* is a divergent branch that inherits from the baseline and records only the changes — so no data is duplicated.

This pattern is particularly useful for staffing reviews, budget negotiations, and scope-change assessments: every stakeholder can see the same timeline while comparing multiple resourcing strategies side by side.


## Baseline Labeling

Baselines are labeled using the format **"Baseline As of [date]"**, where the date reflects when the set was finalized. This replaces opaque version numbers (e.g., "v3") with a date that is meaningful to the user.

The label is derived automatically from `AllocationSet.FinalizedDate` when `Type == AllocationSetType.Baseline` and `IsLocked == true`. The formatted label uses the host application's ambient culture for date formatting.

```
Baseline As of Apr 3, 2026     ← displayed in the Scenario Strip and comparison panel
Scenario: Optimistic Hire       ← user-supplied name; no date suffix
Scenario: Reduced Budget        ← user-supplied name; no date suffix
```

To override the generated label, set `AllocationSet.DisplayLabel` explicitly. When `DisplayLabel` is non-null it takes precedence over the auto-formatted label.


## Data Model

Scenario planning introduces two new entities alongside the existing `AllocationRecord` and `AllocationTarget`.

### AllocationSet

```csharp
public class AllocationSet
{
    public Guid   SetId            { get; set; }
    /// <summary>User-facing label. When null, the component generates
    /// "Baseline As of [FinalizedDate]" for locked baselines.</summary>
    public string DisplayLabel     { get; set; }
    public string Name             { get; set; }
    public AllocationSetType Type  { get; set; }   // Baseline | Scenario
    public Guid?  ParentBaselineId { get; set; }   // null for the baseline itself
    public string CreatedBy        { get; set; }
    public DateTime CreatedDate    { get; set; }
    public DateTime? FinalizedDate { get; set; }   // set when IsLocked = true
    public bool   IsLocked         { get; set; }
    public ScenarioStatus Status { get; set; }
    public string Description      { get; set; }
}
```

### ScenarioOverride

A scenario stores **only its deltas** relative to the baseline. The component merges the baseline allocations with the active scenario's overrides at render time — no full copy of the baseline is made.

```csharp
public class ScenarioOverride
{
    public Guid   OverrideId              { get; set; }
    public Guid   SetId                   { get; set; }
    public Guid?  OriginalAllocationId    { get; set; }  // null for new additions
    public AllocationRecord Override      { get; set; }  // full replacement record
    public bool   IsDeleted               { get; set; }  // tombstone for removed records
    public string OverrideReason          { get; set; }
}
```

Three override types cover all mutations:

| Override type | `OriginalAllocationId` | `IsDeleted` | Meaning |
| --- | --- | --- | --- |
| Modify existing | set | `false` | Replace a baseline allocation with new values |
| Add new | `null` | `false` | Allocation exists only in this scenario |
| Remove existing | set | `true` | Tombstone — baseline allocation is hidden in this scenario |


## Enumerations

### AllocationSetType

```csharp
public enum AllocationSetType
{
    Baseline,
    Scenario
}
```

### ScenarioStatus

```csharp
public enum ScenarioStatus
{
    Draft,      // Being edited; visible only to the creator
    Shared,     // Visible to the project team for review
    Approved,   // Frozen for stakeholder sign-off; no further edits
    Promoted,   // Merged into baseline; archived
    Rejected    // Archived without promotion
}
```


## Parameters

The following parameters extend the base component to support scenario planning.

| Parameter | Type | Default | Description |
| --- | --- | --- | --- |
| `AllocationSets` | `IEnumerable<AllocationSet>` | `null` | The collection of baselines and scenarios available for selection. When `null`, the component operates in single-plan mode with no scenario strip. |
| `ScenarioOverrides` | `IEnumerable<ScenarioOverride>` | `null` | The full set of delta records for all scenarios. The component filters to the active scenario at render time. |
| `ActiveSetId` | `Guid` | baseline id | The `SetId` of the currently displayed set. Supports two-way binding. |
| `CompareSetId` | `Guid?` | `null` | When set, enables diff overlay mode. The baseline ghost bars are rendered alongside the active scenario bars. |
| `ShowBaselineDiff` | `bool` | `false` | Renders baseline allocation bars as ghost/outline bars behind the active scenario bars. Requires `CompareSetId` or falls back to the locked baseline. |
| `BaselineDateFormat` | `string` | `null` | Format string for auto-generated baseline labels (e.g., `"MMM d, yyyy"`). When `null`, the component uses the ambient culture's short date format. |


## Events

| Event | Args Type | Description |
| --- | --- | --- |
| `OnScenarioChanged` | `ScenarioChangedArgs` | Fires when the user switches the active scenario in the Scenario Strip. |
| `OnScenarioCreated` | `ScenarioCreatedArgs` | Fires when a new scenario is created from the strip. |
| `OnAllocationOverridden` | `AllocationOverriddenArgs` | Fires when an edit in a scenario produces a new or updated `ScenarioOverride`. |
| `OnScenarioStatusChanged` | `ScenarioStatusChangedArgs` | Fires when a scenario's `Status` changes (e.g., Draft → Shared). |
| `OnScenarioPromoted` | `ScenarioPromotedArgs` | Fires when a scenario is promoted to become the new locked baseline. |


## Scenario Strip

When `AllocationSets` is bound, a **Scenario Strip** renders above the timeline header. Each set appears as a tab-style chip:

```
[ Baseline As of Apr 3, 2026 🔒 ]  [ Optimistic Hire ]  [ Reduced Budget ]  [ + New Scenario ]
```

- The active set is highlighted with a primary accent border.
- The locked baseline shows a lock icon; it cannot be edited directly.
- Clicking **+ New Scenario** fires `OnScenarioCreated` with a pre-populated `AllocationSet` derived from the current baseline.
- Right-clicking any scenario chip opens a context menu with **Rename**, **Share**, **Approve**, **Promote to Baseline**, and **Delete** commands.

The strip is not rendered when `AllocationSets` is `null` or contains a single entry.


## Diff Overlay Mode

Setting `ShowBaselineDiff = true` (or supplying `CompareSetId`) activates diff overlay mode. In this mode:

- Baseline allocation bars render as transparent ghost bars with a dashed outline.
- Active scenario bars render at full opacity in front.
- Cells where the scenario value differs from the baseline show the delta inline (e.g., `+8h` or `−$2,400`) using the `DeltaDisplayMode` setting.
- Rows with no override show no ghost bar — only the baseline bar, indistinguishable from normal mode.

Diff overlay mode is toggled by the user via the toolbar **Show Baseline Diff** button or set programmatically via the `ShowBaselineDiff` parameter.


## Scenario Comparison Panel

The comparison panel is a collapsible panel below the main grid (or in a side drawer depending on the host layout). It displays per-scenario outcome rollups so stakeholders can evaluate trade-offs without switching between scenarios individually.

| Scenario | Total Hours | Total Cost | Projected Outcome Score |
| --- | --- | --- | --- |
| Baseline As of Apr 3, 2026 | 1,200 h | $84,000 | 72 / 100 |
| Optimistic Hire | 1,450 h | $101,500 | 88 / 100 |
| Reduced Budget | 950 h | $66,500 | 61 / 100 |

The **Projected Outcome Score** column is populated from `AllocationTarget` records attached to each set. It reflects the proportion of targets met across all WBS nodes in the scenario. When no `AllocationTarget` records are present the column is hidden.

Toggle the panel via the `ShowComparisonPanel` parameter or the **Compare Scenarios** toolbar button.


## WBS Integration

Scenario overrides respect WBS hierarchy constraints defined on the resource or task:

- **Control Account cap** — Each WBS node can carry a `MaxValue` (hours or dollars). If a scenario override causes the node's rollup to exceed its cap, the row header shows a warning badge in the Scenario Strip chip and in the resource grid.
- **Deliverable dependencies** — If an override delays a task that is a dependency of a WBS deliverable, the component cascades a visual "at risk in this scenario" indicator to the deliverable row.
- **Per-scenario critical path** — The critical path can differ between scenarios. Enable per-scenario critical path highlighting via `ShowCriticalPath = true`; the component resolves the path using only the effective allocations of the active scenario.


## Scenario Lifecycle

| Status | Who can see it | Editable | Notes |
| --- | --- | --- | --- |
| `Draft` | Creator only | Yes | Default status on creation |
| `Shared` | Project team | Yes | Set when ready for team review |
| `Approved` | All | No | Frozen for stakeholder sign-off |
| `Promoted` | All (archived) | No | Merged into baseline; the old baseline is archived |
| `Rejected` | All (archived) | No | Archived without promotion |

Promoting a scenario to baseline:
1. Fires `OnScenarioPromoted`.
2. The host application merges all `ScenarioOverride` records for the promoted set into the `AllocationRecord` collection.
3. A new `AllocationSet` of type `Baseline` is created with `IsLocked = true` and `FinalizedDate = DateTime.UtcNow`.
4. The component auto-generates the label `"Baseline As of [FinalizedDate]"` unless `DisplayLabel` is set explicitly.
5. The previous baseline's status is set to `Promoted` and it moves to the archive.


## Example — Multi-Scenario Staffing Plan

The following example shows a component with one locked baseline and two active scenarios. The host owns `ActiveSetId` state and persists override changes via `OnAllocationOverridden`.

````RAZOR
<SunfishAllocationScheduler Resources="@Team"
                             Allocations="@BaselineAllocations"
                             AllocationSets="@Sets"
                             ScenarioOverrides="@Overrides"
                             ActiveSetId="@ActiveId"
                             ActiveSetIdChanged="@(id => ActiveId = id)"
                             ShowBaselineDiff="true"
                             AuthoritativeLevel="TimeGranularity.Week"
                             ViewGrain="TimeGranularity.Week"
                             ValueMode="AllocationValueMode.Hours"
                             OnAllocationOverridden="@HandleOverride"
                             OnScenarioPromoted="@HandlePromotion">
    <AllocationResourceColumns>
        <AllocationResourceColumn Field="Name"  Title="Resource" Width="200px" />
        <AllocationResourceColumn Field="Role"  Title="Role"     Width="150px" />
    </AllocationResourceColumns>
</SunfishAllocationScheduler>

@code {
    private Guid ActiveId { get; set; }

    private List<AllocationSet> Sets { get; set; } = new()
    {
        new AllocationSet
        {
            SetId         = Guid.Parse("..."),
            Type          = AllocationSetType.Baseline,
            IsLocked      = true,
            FinalizedDate = new DateTime(2026, 4, 3),
            // DisplayLabel is null → component renders "Baseline As of Apr 3, 2026"
        },
        new AllocationSet
        {
            SetId            = Guid.Parse("..."),
            Name             = "Optimistic Hire",
            Type             = AllocationSetType.Scenario,
            Status           = ScenarioStatus.Shared,
            ParentBaselineId = Guid.Parse("..."),
        },
        new AllocationSet
        {
            SetId            = Guid.Parse("..."),
            Name             = "Reduced Budget",
            Type             = AllocationSetType.Scenario,
            Status           = ScenarioStatus.Draft,
            ParentBaselineId = Guid.Parse("..."),
        }
    };

    private List<ScenarioOverride> Overrides { get; set; } = new();

    private async Task HandleOverride(AllocationOverriddenArgs args)
    {
        Overrides = await ScenarioService.SaveOverrideAsync(args.Override);
    }

    private async Task HandlePromotion(ScenarioPromotedArgs args)
    {
        // Merge overrides into baseline allocations in the host data source
        await ScenarioService.PromoteAsync(args.PromotedSetId);
        // Rebind with fresh baseline records
        BaselineAllocations = await AllocationService.LoadAsync();
        Sets = await ScenarioService.LoadSetsAsync();
        await Scheduler.Rebind();
    }
}
````


## Demo Scenarios

The following scenarios extend the base demo coverage for scenario planning.

1. **Baseline label** — Bind a locked baseline with `FinalizedDate` set; confirm the Scenario Strip renders `"Baseline As of [date]"`, not a version number.
2. **Custom label override** — Set `DisplayLabel = "Board-Approved Plan"` on the baseline; confirm the custom label renders instead of the auto-generated one.
3. **Create scenario** — Click **+ New Scenario** in the strip, name it, and confirm a `Draft` scenario appears with zero overrides.
4. **Edit in scenario** — With the optimistic scenario active, increase a resource's weekly hours; confirm a `ScenarioOverride` is created and the baseline bars remain unchanged.
5. **Diff overlay** — Enable `ShowBaselineDiff`; confirm ghost baseline bars appear behind modified scenario cells.
6. **Tombstone deletion** — Delete an allocation in a scenario; confirm the row is hidden only in that scenario and the baseline record is intact.
7. **Comparison panel** — Open the comparison panel with two scenarios bound; confirm per-scenario hour totals and outcome scores render correctly.
8. **Promote to baseline** — Approve a scenario and promote it; confirm a new `AllocationSet` with `IsLocked = true` is created and its auto-generated label shows today's date.
9. **Control account cap warning** — Exceed a WBS node's `MaxValue` in a scenario; confirm the warning badge appears in the Scenario Strip chip without affecting the baseline.


## See Also

* [AllocationScheduler Overview](slug:allocation-scheduler-overview)
* [Analysis and Targets](slug:allocation-scheduler-analysis-targets)
* [Editing Grain Design Decision](slug:allocation-scheduler-editing-grain)
* [Context Menu Commands](slug:allocation-scheduler-context-menu)
