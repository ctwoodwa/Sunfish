---
title: Display Format
page_title: Grid - Display Format
description: Use C# Format string to display values in the Grid for Blazor.
slug: grid-columns-displayformat
tags: sunfish,blazor,grid,column,display,format
published: True
position: 15
components: ["grid"]
---
# Column Display Format

@[template](/_contentTemplates/grid/common-link.md#display-format-basics)
The `SunfishGridColumn` exposes two parameters for formatting cell values: `DisplayFormat` (the preferred modern form, using composite format strings) and `Format` (a legacy simple format string retained for backwards compatibility). If both are set, `DisplayFormat` takes precedence.

## Parameters

| Parameter | Type | Description |
| --- | --- | --- |
| `DisplayFormat` | `string?` | Composite format string for display (e.g. `"{0:C2}"`, `"{0:dd MMM yy}"`). Takes precedence over `Format`. This is the preferred modern form for new code. |
| `Format` | `string?` | Simple format string for displaying the cell value (e.g. `"C2"`, `"yyyy-MM-dd"`). Applied via `IFormattable.ToString(format, null)`. Legacy — retained for backwards compatibility with older demos and markup. |

>note Which to use: prefer `DisplayFormat` for new code — composite format syntax is more flexible and matches the `DisplayFormatAttribute.DataFormatString` convention. Use `Format` only when maintaining compatibility with existing demos or markup that already relies on the simple-format form.

## Example

>caption Use C# format strings in the grid through the component markup and a data annotation attribute in the model

````RAZOR
@using System.ComponentModel.DataAnnotations
@* This Using is for the model class attribute only *@

<SunfishDataGrid Data="@GridData" Pageable="true">
        <SunfishGridColumn Field="@nameof(SampleModel.Name)" />

        <SunfishGridColumn Field="@nameof(SampleModel.Salary)" />
        <SunfishGridColumn DisplayFormat="{0:dd MMM yy}" Field="@nameof(SampleModel.HireDate)" />

</SunfishDataGrid>

@code {
    class SampleModel
    {
        public string Name { get; set; }

        [DisplayFormat(DataFormatString = "{0:C}")]
        public decimal Salary { get; set; }
        public DateTime HireDate { get; set; }
    }

    // sample data generation

    List<SampleModel> GridData { get; set; }

    protected override void OnInitialized()
    {
        Random rand = new Random();
        GridData = Enumerable.Range(1, 50).Select(x => new SampleModel
        {
            Name = $"name {x}",
            Salary = x * 20000 / 12.34m,
            HireDate = DateTime.Now.Date.AddMonths(rand.Next(-20, 20)).AddDays(rand.Next(-10, 10)),
        }).ToList();
    }
}
````


@[template](/_contentTemplates/grid/common-link.md#display-format-notes)


## See Also

  * [Live Demo: Cell Formatting](https://demos.sunfish.dev/blazor-ui/grid/cell-formatting)
  * [Blazor Grid](slug:grid-overview)
