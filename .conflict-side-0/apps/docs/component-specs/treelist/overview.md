---
title: Overview
page_title: TreeList Overview
description: Overview of the TreeList for Blazor. Review features and configuration parameters.
slug: treelist-overview
tags: sunfish,blazor,treelist,overview
published: True
position: 0
components: ["treelist"]
---
# Blazor TreeList Component Overview

The <a href = "https://sunfish.dev/blazor-ui/treelist" target="_blank">Blazor TreeList component</a> displays hierarchical data in a tabular format and allows [sorting](slug:treelist-sorting), [filtering](slug:treelist-filtering), [data editing](slug:treelist-editing-overview); provides item [selection](slug:treelist-selection-overview), [templates](slug:treelist-templates-overview) and [load on demand](slug:treelist-data-binding-load-on-demand).


## Creating Blazor TreeList

The TreeList supports both flat data and hierarchical data. The example below uses flat data.

1. Use the `<SunfishTreeList>` tag.
1. Assign the TreeList `Data` attribute to an `IEnumerable<TItem>` property. The model class `TItem` should have two properties that describe the parent-child relations, for example: `Id` (`int`) and `ParentId` (`int?`).
1. Set the following TreeList parameters, based on the `ITem` property names: `IdField` and `ParentIdField`.
1. Add some `<TreeListColumn>` instances inside a `<TreeListColumns>` tag.
1. For each column, set a `Field` and an optional `Title`.
1. Set `Expandable="true"` for the column that should render expand/collapse arrows.
1. (optional) Enable other features, such as `Pageable`, `Sortable` or `FilterMode`.

>caption Basic TreeList

````RAZOR
<SunfishTreeList Data="@TreeListData"
                 IdField="@nameof(Employee.Id)"
                 ParentIdField="@nameof(Employee.ParentId)"
                 Pageable="true"
                 Sortable="true"
                 FilterMode="@TreeListFilterMode.FilterMenu">
    <TreeListColumns>
        <TreeListColumn Expandable="true" Field="FirstName" Title="First Name" />
        <TreeListColumn Field="LastName" Title="Last Name" />
        <TreeListColumn Field="Position" />
    </TreeListColumns>
</SunfishTreeList>

@code {

    List<Employee> TreeListData { get; set; }

    protected override void OnInitialized()
    {
        TreeListData = new List<Employee>();

        for (int i = 1; i <= 9; i++)
        {
            TreeListData.Add(new Employee()
            {
                Id = i,
                ParentId = i <= 3 ? null : i % 3 + 1,
                FirstName = "First " + i,
                LastName = "Last " + i,
                Position = i <= 3 ? "Team Lead" : "Software Engineer"
            });
        }

        base.OnInitialized();
    }

    public class Employee
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Position { get; set; }
    }
}
````


## Data Binding

The Sunfish Blazor TreeList is datasource agnostic. You can use any database and service, according to your project. The important step is to configure the model property names, which define the data structure.

The following resources provide details and examples for data binding a TreeList:

* [TreeList Data Binding Overview](slug:treelist-data-binding-overview) - general information on how data binding works
* [Bind TreeList to Flat Self-Referencing Data](slug:treelist-data-binding-flat-data)
* [Bind TreeList to Hierarchical Data](slug:treelist-data-binding-hierarchical-data) - in this case, each data item may contain a nested item collection
* [Load On Demand in TreeList](slug:treelist-data-binding-load-on-demand) - how to load child items only when necessary


## Data Operations

The Blazor TreeList supports all fundamental data operations out-of-the-box:

* [TreeList Paging](slug:treelist-paging)
* [TreeList Sorting](slug:treelist-sorting)
* [TreeList Filtering](slug:treelist-filtering)


## Editing

The TreeList can perform CRUD operations on its current data. It exposes events that let you control the operations and transfer changes to the actual data source. See [TreeList CRUD Operations Overview](slug:treelist-editing-overview) for more details.


## Column Features

The Treelist columns are one of its main building blocks. They offer a rich set of functionality to enable flexibility for different application scenarios.

* [Bound Columns](slug:treelist-columns-bound)
* [Column display Format](slug:treelist-columns-displayformat) for numeric and date values
* [Column reordering](slug:treelist-columns-reorder)
* [Column resizing](slug:treelist-columns-resize)
* [Column Menu](slug:treelist-column-menu) to control data operations and column visibility
* [How column width works](slug:treelist-columns-width)
* [CheckBox column](slug:treelist-columns-checkbox)
* [Command column](slug:treelist-columns-command)
* [Frozen (Locked) columns](slug:treelist-columns-frozen)
* [UI Virtualization](slug:treelist-columns-virtual)
* [Visibility](slug:treelist-columns-visible)
* [Autogenerated columns](slug:treelist-columns-automatically-generated)
* [Multi-column Headers](slug:treelist-columns-multiple-column-headers)
* [Column events](slug:treelist-column-events)


## Templates

The various [TreeList templates](slug:treelist-templates-overview) provide better control over the rendering of:

* [data cells](slug:treelist-templates-column) and [data rows](slug:treelist-templates-row)
* [header cells](slug:treelist-templates-column-header)
* [filter menus and rows](slug:treelist-templates-filter)
* [data editors](slug:treelist-templates-editor)
* [no data message](slug:treelist-templates-no-data)


## More TreeList Features

* [Selection - single and multiple](slug:treelist-selection-overview).
* [State - get or set the TreeList configuration programmatically](slug:treelist-state)
* [Toolbar - define custom TreeList actions](slug:treelist-toolbar)


## TreeList Parameters

The following table lists Tree List parameters, which are not related to other features on this page. Check the [TreeList API Reference](slug:Sunfish.Blazor.Components.SunfishTreeList-1) for a full list of properties, methods and events.

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type and Default&nbsp;Value | Description |
|---|---|---|
| `Data` | `IEnumerable<TItem>` | The data source for the TreeList. |
| `IdField` | `string?` | The property name that uniquely identifies each item (used with flat data). |
| `ParentIdField` | `string?` | The property name that identifies the parent item (used with flat data). |
| `ItemsField` | `string?` | The property name that contains child items (used with hierarchical data). |
| `HasChildrenField` | `string?` | The property name (bool) indicating whether an item has children. |
| `Sortable` | `bool` | Enables sorting on all columns. Individual columns can override via their own `Sortable` parameter. |
| `FilterMode` | `TreeListFilterMode` (`None`) | `None` or `FilterRow`. |
| `SelectionMode` | `TreeListSelectionMode` (`None`) | `None`, `Single`, or `Multiple`. |
| `SelectedItems` | `IReadOnlyList<TItem>?` | The currently selected items (two-way bindable via `SelectedItemsChanged`). |
| `EditMode` | `TreeListEditMode` (`None`) | `None` or `Inline`. |
| `Navigable` | `bool` | Enables keyboard navigation (Arrow keys, Enter, Escape, Home, End). |
| `Resizable` | `bool` | Enables column resizing by dragging header cell edges. |
| `Reorderable` | `bool` | Enables column reordering by dragging header cells. |
| `Pageable` | `bool` | Shows a pager below the TreeList. Paging applies to top-level items. |
| `PageSize` | `int` (`10`) | Number of top-level items per page. |
| `Page` | `int` (`1`) | Current page number (1-based, two-way bindable). |
| `EnableVirtualization` | `bool` | Uses Blazor's built-in `<Virtualize>` component for the row list instead of rendering all rows. Dramatically improves performance for large trees. |
| `ItemHeight` | `int` (`36`) | Pixel height of each row, used as `ItemSize` by the Virtualize component. |
| `RowDraggable` | `bool` | Enables row drag-and-drop. Rows get `draggable="true"` and CSS classes `mar-treelist__row--dragging` / `mar-treelist__row--drop-target`. |
| `Class` | `string` | Additional CSS class rendered on the root `div.mar-treelist` element. |
| `Height` | `string` | The height value in any supported CSS unit. |
| `Width` | `string` | The width value in any supported CSS unit. |


## TreeList Reference and Methods

The TreeList component has methods to to execute actions such as:

* [Rebind to refresh the data](slug:treelist-refresh-data#rebind-method)
* [Automatically resize columns to fit their content](slug:treelist-columns-resize)
* [Get or set the TreeList configuration state](slug:treelist-state)
* [Get the dragged data item and its drop index from the destination TreeList](slug:treelist-drag-drop-overview)

To execute these methods, obtain reference to the Grid instance via `@ref`. 

The TreeList is a generic component.Its type depends on the type of its model and the type of its `Value`. In case you cannot provide either the `Value` or `Data` initially, you need to [set the corresponding types to the `TItem` and `TValue` parameters](slug:common-features-data-binding-overview#component-type).

>caption Store the TreeList instance reference and execute methods

````RAZOR
<SunfishButton OnClick="@AutoFit">Autofit All Columns</SunfishButton>

<SunfishTreeList @ref="@TreeListRef"
                 Data="@Data"
                 IdField="EmployeeId"
                 ParentIdField="ReportsTo"
                 Pageable="true">
    <TreeListColumns>
        <TreeListColumn Field="FirstName" Expandable="true"></TreeListColumn>
        <TreeListColumn Field="EmployeeId"></TreeListColumn>
    </TreeListColumns>
</SunfishTreeList>

@code {
    SunfishTreeList<Employee> TreeListRef { get; set; }
    public List<Employee> Data { get; set; }

    void AutoFit()
    {
        TreeListRef.AutoFitAllColumns();
    }

    protected override void OnInitialized()
    {
        Data = new List<Employee>();
        var rand = new Random();
        int currentId = 1;

        for (int i = 1; i < 6; i++)
        {
            Data.Add(new Employee()
            {
                EmployeeId = currentId,
                ReportsTo = null,
                FirstName = "Employee  " + i.ToString()
            });

            currentId++;
        }
        for (int i = 1; i < 6; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                Data.Add(new Employee()
                {
                    EmployeeId = currentId,
                    ReportsTo = i,
                    FirstName = "    Employee " + i + " : " + j.ToString()
                });

                currentId++;
            }
        }
    }

    public class Employee
    {
        public int EmployeeId { get; set; }
        public string FirstName { get; set; }
        public int? ReportsTo { get; set; }
    }
}
````


# Next Steps

* [Explore Tree List data binding](slug:treelist-data-binding-overview)
* [Learn about Tree List columns](slug:treelist-columns-bound)


## See Also

* [Live Demos: TreeList](https://demos.sunfish.dev/blazor-ui/treelist/overview)
* [TreeList API Reference](slug:Sunfish.Blazor.Components.SunfishTreeList-1)
