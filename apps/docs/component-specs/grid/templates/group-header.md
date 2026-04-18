---
title: Group Header
page_title: Grid - Group Header Template
description: Use custom group header templates in Grid for Blazor.
slug: grid-templates-group-header
tags: sunfish,blazor,grid,templates,group,header
published: True
position: 30
components: ["grid"]
---
# Group Header

When the grid is grouped, the top row above the group provides information about the current group value by default. You can use this template to add custom content there in addition to the current value. For more information and examples, see the [Aggregates](slug:grid-aggregates) article.

>caption Sample Group Header Template

````RAZOR
@* Group by the Team and Active Projects fields to see the results *@

<SunfishDataGrid Data=@GridData Groupable="true" Pageable="true" Height="650px">
    <GridAggregates>
        <GridAggregate Field=@nameof(Employee.Team) Aggregate="@GridAggregateType.Count" />
    </GridAggregates>
        <SunfishGridColumn Field=@nameof(Employee.Name) Groupable="false" />
        <SunfishGridColumn Field=@nameof(Employee.Team) Title="Team">
            <GroupHeaderTemplate>
                @context.Value @* the default text you would get without the template *@
                &nbsp;<span>Team size: @context.Count</span>
            </GroupHeaderTemplate>
        </SunfishGridColumn>
        <SunfishGridColumn Field=@nameof(Employee.Salary) Title="Salary" Groupable="false" />
        <SunfishGridColumn Field=@nameof(Employee.ActiveProjects) Title="Active Projects">
            <GroupHeaderTemplate>
                @{
                    <span>Currently active projects: @context.Value &nbsp;</span>

                    //sample of conditional logic in the group header
                    if ( (int)context.Value > 3) // in a real case, ensure type safety and add defensive checks
                    {
                        <strong style="color: red;">These people work on too many projects</strong>
                    }
                }
            </GroupHeaderTemplate>
        </SunfishGridColumn>
</SunfishDataGrid>

@code {
    public List<Employee> GridData { get; set; }

    protected override void OnInitialized()
    {
        GridData = new List<Employee>();
        var rand = new Random();
        for (int i = 0; i < 15; i++)
        {
            Random rnd = new Random();
            GridData.Add(new Employee()
            {
                EmployeeId = i,
                Name = "Employee " + i.ToString(),
                Team = "Team " + i % 3,
                Salary = rnd.Next(1000, 5000),
                ActiveProjects = i % 4 == 0 ? 2 : 5
            });
        }
    }

    public class Employee
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; }
        public string Team { get; set; }
        public decimal Salary { get; set; }
        public int ActiveProjects { get; set; }
    }
}
````

>caption The result from the code snippet above after grouping by the `Team` and `Active Projects` columns

![Blazor Grid Group Header Template](images/group-header-template.png)

## GridGroupHeaderContext Type

The `@context` object inside `<GroupHeaderTemplate>` and `<GroupFooterTemplate>` is an instance of `GridGroupHeaderContext<TItem>`, where `TItem` is the grid's row model type (the element type of the grid's `Data` source). It exposes information about the current group, plus aggregate helpers for computing totals over the group's items.

### Properties

| Property | Type | Description |
| --- | --- | --- |
| `Field` | `string` | The field name being grouped. |
| `Value` | `object?` | The group key value. |
| `Items` | `IReadOnlyList<TItem>` | The items in this group. |
| `Count` | `int` | Number of items in this group. |
| `Depth` | `int` | The nesting depth (`0` = top-level). |
| `IsCollapsed` | `bool` | Whether this group is collapsed. |

### Aggregate helper methods

* `Sum(Func<TItem, decimal> selector)` — computes the sum of a `decimal` property across items in this group.
* `Average(Func<TItem, decimal> selector)` — computes the average of a `decimal` property across items in this group.
* `Sum(Func<TItem, int> selector)` — computes the sum of an `int` property across items in this group.
* `Average(Func<TItem, int> selector)` — computes the average of an `int` property across items in this group.
* `Min<TResult>(Func<TItem, TResult> selector)` — gets the minimum value of a property across items in this group.
* `Max<TResult>(Func<TItem, TResult> selector)` — gets the maximum value of a property across items in this group.

## See Also

 * [Live Demo: Grid Templates](https://demos.sunfish.dev/blazor-ui/grid/templates)
 * [Live Demo: Grid Custom Editor Template](https://demos.sunfish.dev/blazor-ui/grid/custom-editor)
 * [Blazor Grid](slug:grid-overview)

