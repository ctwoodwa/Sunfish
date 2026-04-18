---
title: Templates
page_title: AllocationScheduler Templates
description: How to use ResourceTemplate, ItemTemplate, CellTemplate, and other RenderFragment slots in the AllocationScheduler.
slug: allocation-scheduler-templates
tags: sunfish,blazor,allocation-scheduler,templates,render-fragment
published: True
position: 5
components: ["allocation-scheduler"]
---

# AllocationScheduler Templates

## AllocationResourceColumns

Define resource metadata columns using child `AllocationResourceColumn` tags.

```razor
<SunfishAllocationScheduler TResource="StaffResource" ...>
    <AllocationResourceColumns>
        <AllocationResourceColumn TResource="StaffResource" Field="Name" Title="Resource" Width="200px" />
        <AllocationResourceColumn TResource="StaffResource" Field="Role" Title="Role" Width="150px" />
    </AllocationResourceColumns>
</SunfishAllocationScheduler>
```

## Column Template

Each `AllocationResourceColumn` accepts a `Template` RenderFragment for custom cell rendering.

```razor
<AllocationResourceColumn TResource="StaffResource" Field="Name" Title="Resource" Width="200px">
    <Template>
        <div style="display:flex;align-items:center;gap:0.5rem">
            <img src="@context.AvatarUrl" style="width:24px;height:24px;border-radius:50%" />
            <span>@context.Name</span>
        </div>
    </Template>
</AllocationResourceColumn>
```

## CellTemplate

Customize the rendering of individual allocation cells.

```razor
<SunfishAllocationScheduler TResource="StaffResource" ...>
    <CellTemplate>
        @if (context.Record is not null)
        {
            <div class="custom-cell @(context.IsConflict ? "conflict" : "")">
                <span>@context.Record.Value h</span>
            </div>
        }
    </CellTemplate>
</SunfishAllocationScheduler>
```

## EmptyTemplate

Shown when no allocations are bound.

```razor
<SunfishAllocationScheduler TResource="StaffResource" ...>
    <EmptyTemplate>
        <p>No allocations yet. Click a cell to start planning.</p>
    </EmptyTemplate>
</SunfishAllocationScheduler>
```

## ResourceRowTemplate

Fallback template for resource metadata cells when no column-level `Template` is set. Receives the `TResource` instance as `context`.

```razor
<SunfishAllocationScheduler TResource="StaffResource" ...>
    <ResourceRowTemplate>
        <div class="resource-badge">
            <strong>@context.Name</strong>
            <span class="role-tag">@context.Role</span>
        </div>
    </ResourceRowTemplate>
</SunfishAllocationScheduler>
```

> When both `ResourceRowTemplate` and a column-level `Template` are set, the column-level template wins for that column.

## Grouped Headers

When the view grain is coarser than Day, the timeline renders a **two-row header**: a top group row and a bottom leaf row. The group row labels the parent period (e.g., "Apr 2026" when viewing weeks, "2026" when viewing months). The leaf row labels individual time buckets.

### BEM Classes

| Class | Description |
|---|---|
| `mar-allocation-scheduler__header-group-row` | The `<tr>` for the top group header row. |
| `mar-allocation-scheduler__header-group-cell` | Each `<th>` in the group row, spanning the child columns that belong to the same parent period. |

The group row uses `role="row"` and group cells use `role="columnheader"` with a `colspan` matching the number of child buckets. The leaf row uses the standard `AllocationSchedulerTimeHeaderClass` CSS provider method.

## ToolbarTemplate

Append custom content to the built-in toolbar.

```razor
<SunfishAllocationScheduler TResource="StaffResource" ...>
    <ToolbarTemplate>
        <button @onclick="ExportToExcel">Export</button>
    </ToolbarTemplate>
</SunfishAllocationScheduler>
```
