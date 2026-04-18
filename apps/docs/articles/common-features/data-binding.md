---
uid: common-features-data-binding
title: Data Binding
description: How to bind data to Sunfish components using Value/ValueChanged, OnRead, and observable patterns.
---

# Data Binding

Sunfish components follow Blazor's standard two-way binding conventions and extend them with server-side loading and observable collection support for data-heavy components such as DataGrid, ListView, and MultiSelect.

## Value Binding

All Sunfish input components expose a `Value` parameter and a matching `ValueChanged` callback. Blazor's `@bind-Value` directive combines these into a two-way binding with a single attribute.

```razor
<SunfishTextBox @bind-Value="model.Name" />

<SunfishNumericTextBox @bind-Value="model.Quantity" />

<SunfishCheckBox @bind-Value="model.IsActive" />
```

The bound property is updated immediately whenever the user changes the input value. No additional event handlers are needed for standard form scenarios.

## One-Way Binding with Manual ValueChanged

When you need to intercept or transform the value before applying it to your model, split the binding into its two parts — `Value` for the current value and `ValueChanged` for the update callback.

```razor
<SunfishTextBox
    Value="@model.Name"
    ValueChanged="@OnNameChanged" />

@code {
    private async Task OnNameChanged(string newValue)
    {
        // Validate, transform, or audit before applying
        model.Name = newValue.Trim();
        await SaveDraftAsync();
    }
}
```

> Do not use both `@bind-Value` and a separate `ValueChanged` at the same time. `@bind-Value` expands to `Value` + `ValueChanged` internally, so supplying a second `ValueChanged` will cause a duplicate parameter error.

## OnRead Pattern

Components that display collections — DataGrid, ListView, MultiSelect, TreeList — support server-side data loading through an `OnRead` callback. This pattern gives you full control over filtering, sorting, and paging without loading the entire dataset into memory.

The callback receives a read event args object that describes what data the component needs, and you set `Data` and `Total` on it before returning.

### DataGrid Example

```razor
<SunfishDataGrid TItem="Order"
                OnRead="@LoadOrders"
                PageSize="20">
    <SunfishGridColumn Field="@nameof(Order.Id)" Title="Order #" />
    <SunfishGridColumn Field="@nameof(Order.Customer)" Title="Customer" />
    <SunfishGridColumn Field="@nameof(Order.Total)" Title="Total" />
</SunfishDataGrid>

@code {
    private async Task LoadOrders(GridReadEventArgs<Order> args)
    {
        var query = OrderRepository.Query();

        // Apply filters supplied by the grid
        foreach (var filter in args.Filters)
        {
            query = query.Where(filter.Field, filter.Operator, filter.Value);
        }

        // Apply sorting
        foreach (var sort in args.Sorts)
        {
            query = sort.Descending
                ? query.OrderByDescending(sort.Field)
                : query.OrderBy(sort.Field);
        }

        args.Total = await query.CountAsync();
        args.Data  = await query
            .Skip(args.Skip)
            .Take(args.PageSize)
            .ToListAsync(args.CancellationToken);
    }
}
```

### MultiSelect Remote Data Example

```razor
<SunfishMultiSelect TItem="Product"
                   TValue="int"
                   ValueField="@(p => p.Id)"
                   TextField="@(p => p.Name)"
                   OnRead="@LoadProducts"
                   @bind-Value="selectedIds" />

@code {
    private List<int> selectedIds = new();

    private async Task LoadProducts(MultiSelectReadEventArgs<Product> args)
    {
        var results = await ProductService.SearchAsync(
            filter: args.Filter,
            cancellationToken: args.CancellationToken);

        args.Data  = results.Items;
        args.Total = results.TotalCount;
    }
}
```

### Read Event Args Reference

| Property | Description |
|---|---|
| `Filter` | Current text filter entered by the user (MultiSelect, ComboBox). |
| `Filters` | Collection of field filters (DataGrid). |
| `Sorts` | Collection of sort descriptors. |
| `Skip` | Number of records to skip (for paging). |
| `PageSize` | Number of records the component is requesting. |
| `CancellationToken` | Cancel the async operation if the component initiates a newer read. |
| `Data` | **You set this.** The page of items to display. |
| `Total` | **You set this.** The total record count (used to calculate page count). |

### Rebind

Call `Rebind()` on the component reference to force a fresh `OnRead` invocation — for example, after an external action modifies the data source.

```razor
<SunfishDataGrid @ref="grid" TItem="Order" OnRead="@LoadOrders" .../>

@code {
    private SunfishDataGrid<Order> grid = default!;

    private async Task RefreshGrid()
    {
        await grid.Rebind();
    }
}
```

## Observable Data

For client-side collections that change at runtime, bind `Data` to an `ObservableCollection<T>`. The component subscribes to `CollectionChanged` and re-renders automatically when items are added, removed, or replaced — without requiring a manual `StateHasChanged` call.

```razor
<SunfishListView TItem="Task" Data="@tasks">
    <ItemTemplate Context="task">
        <span>@task.Title</span>
    </ItemTemplate>
</SunfishListView>

@code {
    private ObservableCollection<Task> tasks = new();

    private void AddTask(string title)
    {
        // ListView updates automatically
        tasks.Add(new Task { Title = title });
    }

    private void RemoveTask(Task task)
    {
        tasks.Remove(task);
    }
}
```

Use `ObservableCollection<T>` when the list is small and lives entirely in the client. Use `OnRead` when the dataset is large or lives on the server.

## Item Templates

Data-display components accept render fragment parameters to customize how items are presented.

### ItemTemplate

Controls how each item in the list or grid body is rendered.

```razor
<SunfishListView TItem="Employee" Data="@employees">
    <ItemTemplate Context="emp">
        <div class="employee-row">
            <SunfishAvatar Name="@emp.FullName" />
            <span>@emp.FullName</span>
            <span>@emp.Department</span>
        </div>
    </ItemTemplate>
</SunfishListView>
```

### HeaderTemplate

Renders a custom header above the list or grid body.

```razor
<SunfishListView TItem="Product" Data="@products">
    <HeaderTemplate>
        <div class="list-header">Products (@products.Count)</div>
    </HeaderTemplate>
    <ItemTemplate Context="p">
        <span>@p.Name</span>
    </ItemTemplate>
</SunfishListView>
```

### FooterTemplate

Renders a custom footer below the list or grid body.

```razor
<SunfishListView TItem="Order" Data="@orders">
    <ItemTemplate Context="o">
        <span>@o.Reference</span>
    </ItemTemplate>
    <FooterTemplate>
        <div class="list-footer">Total: @orders.Sum(o => o.Total):C</div>
    </FooterTemplate>
</SunfishListView>
```
