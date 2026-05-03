---
title: Cell Selection
page_title: Grid - Cells Selection
description: Learn how to select cell in Blazor Grid component. Explore the selected cells. Discover cell selection bevahior when combined with other Grid features. Try the practical sample code for cell selection.
slug: grid-selection-cell
tags: sunfish,blazor,grid,selection,cells
position: 5
components: ["grid"]
---
# Cell Selection

The Grid component supports [single or multiple cell selection](slug:grid-selection-overview#use-single-or-multiple-selection). You can select a cell with mouse click anywhere in the cell. You can access the collection of selected cells, use this collection and manipulate it. You can subscribe to selection events.

## Basics

To select a cell, click anywhere in it.

To select a range of cells in one or more columns, hold the **Shift** key, while clicking on the first and last cell of the range. To select or deselect multiple cells that don't belong to a range, hold the **Ctrl** key.

You can also select a cell range by holding and dragging the mouse cursor. The dragging motion defines the diagonal of a rectangle and the Grid will select the cells under this rectangle. This kind of cell selection depends on the `DragToSelect` parameter in [`GridSelectionSettings`](slug:grid-selection-overview#enable-row-or-cell-selection). The default value of `DragToSelect` is `true` and in this case standard browser text selection is not supported.

To enable cell selection:

1. Set the Grid `SelectedCells` parameter to a collection of type `IEnumerable<GridCellReference<TItem>>`. The collection must be initialized in advance. See [`GridCellReference`](#gridcellreference) for infomation about the object properties.
1. Set the `SelectionUnit` parameter on the `<SunfishDataGrid>` to `GridSelectionUnit.Cell`.

>caption Grid multiple cell selection

````RAZOR
<SunfishDataGrid Data="@GridData"
             SelectionMode="@GridSelectionMode.Multiple"
             SelectionUnit="@GridSelectionUnit.Cell"
             @bind-SelectedCells="@SelectedCells"
             Pageable="true">
    <GridSettings>
        <GridSelectionSettings DragToSelect="true" />
    </GridSettings>
        <SunfishGridColumn Field="@nameof(Employee.Name)" />
        <SunfishGridColumn Field="@nameof(Employee.Team)" />
</SunfishDataGrid>

<h3>Selected Cells:</h3>

<ul>
    @foreach (GridCellReference<Employee> cellDescriptor in SelectedCells)
    {
        <li>
            Column <code>Field</code>: @cellDescriptor.Field,
            <code>EmployeeId</code>: @cellDescriptor.Item.EmployeeId
        </li>
    }
</ul>

@code {
    private List<Employee> GridData { get; set; } = new();

    private IEnumerable<GridCellReference<Employee>> SelectedCells { get; set; } = Enumerable.Empty<GridCellReference<Employee>>();

    protected override void OnInitialized()
    {
        for (int i = 1; i <= 15; i++)
        {
            GridData.Add(new Employee()
            {
                EmployeeId = i,
                Name = $"Employee {i}",
                Team = $"Team {i % 3 + 1}"
            });
        }

        SelectedCells = new List<GridCellReference<Employee>>() {
            new GridCellReference<Employee>()
            {
                Item = GridData.ElementAt(2),
                Field = nameof(Employee.Name)
            }
        };
    }

    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
    }
}
````

## SelectedCellsChanged Event

You can respond to user selection actions through the `SelectedCellsChanged` event. The event handler receives a collection of type `IEnumerable<GridCellReference<TItem>>`. The collection may have multiple, single, or no objects in it, depending on the `SelectionMode` and the last user selection.

> The `SelectedCellsChanged` event handler cannot be awaited. To execute asynchronous operations when the user selects rows, use the [`OnRowClick`](slug:grid-events#onrowclick) or [`OnRowDoubleClick`](slug:grid-events#onrowdoubleclick) event instead.

>caption Using the Grid SelectedCellsChanged event

````RAZOR
@* Select cells and handle the SelectedCellsChanged event *@

<SunfishDataGrid Data="@GridData"
             SelectionMode="@GridSelectionMode.Multiple"
             SelectionUnit="@GridSelectionUnit.Cell"
             SelectedCells="@SelectedCells"
             SelectedCellsChanged="@( (IEnumerable<GridCellReference<Employee>> newSelected) => OnCellSelect(newSelected) )"
             Pageable="true">
    <GridSettings>
        <GridSelectionSettings DragToSelect="true" />
    </GridSettings>
        <SunfishGridColumn Field="@nameof(Employee.Name)" />
        <SunfishGridColumn Field="@nameof(Employee.Team)" />
</SunfishDataGrid>

<p><code>SelectedItemsChanged</code> fired at: @SelectedCellsChangedLog</p>

<h3>Selected Cells:</h3>

<ul>
    @foreach (GridCellReference<Employee> cellDescriptor in SelectedCells)
    {
        <li>
            Column <code>Field</code>: @cellDescriptor.Field,
            <code>EmployeeId</code>: @cellDescriptor.Item.EmployeeId
        </li>
    }
</ul>

@code {
    private List<Employee> GridData { get; set; } = new();

    private IEnumerable<GridCellReference<Employee>> SelectedCells { get; set; } = Enumerable.Empty<GridCellReference<Employee>>();

    private string SelectedCellsChangedLog { get; set; } = string.Empty;

    protected void OnCellSelect(IEnumerable<GridCellReference<Employee>> cellDescriptors)
    {
        // Update the SelectedCells collection manually.
        // When using two-way binding, this happens automatically.
        SelectedCells = cellDescriptors;

        SelectedCellsChangedLog = DateTime.Now.ToLongTimeString();
    }

    protected override void OnInitialized()
    {
        for (int i = 1; i <= 15; i++)
        {
            GridData.Add(new Employee()
            {
                EmployeeId = i,
                Name = $"Employee {i}",
                Team = $"Team {i % 3 + 1}"
            });
        }

        SelectedCells = new List<GridCellReference<Employee>>() {
            new GridCellReference<Employee>()
            {
                Item = GridData.ElementAt(2),
                Field = nameof(Employee.Name)
            }
        };
    }

    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
    }
}
````

## GridCellReference

The `GridCellReference<TItem>` type exposes the following properties:

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Property Name | Type | Description |
| --- | --- | --- |
| `Item` | `TItem` | The data item (row) containing this cell. |
| `Field` | `string` | The field name of the column. |
| `Value` | `object?` | The value of the cell. |
| `RowIndex` | `int` | The row index in the current display. |

## Selection When Data Changes

When the Grid `Data` collection changes, the `SelectedCells` collection has the following behavior:

* When the user updates a selected cell and the item instance is replaced, you have to also replace the `Item` value in the `SelectedCells` collection. Do that in the [Grid `OnUpdate` event](slug:grid-editing-overview#events).
* When the user deletes a row with selected cells, update the `SelectedCells` collection in the the Grid `OnDelete` event handler.
* To select cells from a new item in the Grid you can use the [`OnCreate` event](slug:grid-editing-overview#events) to update the `SelectedCells` collection.

### Equals Comparison

The items in `SelectedCells` are compared against the items in the Grid data in order to determine which cells will be highlighted. The default framework behavior is to compare objects by reference. The data item references may not match when:

* The Grid is databound through its `OnRead` event and each data request returns different data item instances.
* The `SelectedCells` are obtained from a different data source than the all Grid items, for example, from a separate service.

In such cases, the selected cells may not appear as expected. You have to [override the `Equals` method of the Grid model class](slug:grid-state#equals-comparison) so that the items are compared by a unique identifier rather than by reference. When you override `Equals`, it is also recommended to override the [`GetHashCode`](https://docs.microsoft.com/en-us/dotnet/api/system.object.gethashcode) method.

## Cell Selection and Other Grid Features

The selection feature behavior may vary when the Grid configuration combines cell selection and other Grid features, such as editing, virtualization, paging, templates. In such cases you need to consider certain limitation or include some modications.

### Selection and Editing Modes

When you want to edit a Grid item, the cell selection has the following behavior:

* Cell selection is not supported with [`Incell` edit mode](slug:grid-editing-incell) due to the overlapping pointer events that trigger selection and editing.
* [`Inline` edit mode](slug:grid-editing-inline) and [`Popup` edit mode](slug:grid-editing-popup) integrate with cell selection without limitations.

### Selection and Virtual Scrolling

When the Grid has [virtual scrolling](slug:components/grid/virtual-scrolling), the component is able to select a range of cells with **Shift** only if all rows in that range are currently rendered. Consider the following scenario:

1. Select a cell.
1. Scroll down, so that virtualization kicks in and the rendered rows are no longer the same.
1. Select another cell with **Shift**.

In this case, the range selection will start from the first row that is currently rendered. Compare with [Selection and paging](#selection-and-paging) below.

### Selection and Paging

The `SelectedCells` collection persists across paging.

### Selection and Templates

When using [Grid templates](slug:components/grid/features/templates) with cell selection:

* If you are using a [Grid column template](slug:grid-templates-column) and you have a clickable element in the template, wrap this element in a container with a `@onclick:stopPropagation` directive. You can check the knowledge base article on [how to prevent row selection when the user clicks another component in the Grid column template](slug:grid-kb-row-selection-in-column-template). It applies for both row and cell selection.
* If you are using a [row template](slug:grid-templates-row) the Grid does not support cell selection. The row template removes the built-in cell instances and the HTML markup may not even include the expected number of cells.

## See Also

* [Live Demo: Grid Cell Selection](https://demos.sunfish.dev/blazor-ui/grid/cell-selection)
* [Blazor Grid](slug:grid-overview)
* [Grid Highlighting](slug:grid-highlighting)
