---
title: Column Group Footer
page_title: Grid - Column Group Footer Template
description: Use custom column group footer templates in Grid for Blazor.
slug: grid-templates-column-group-footer
tags: sunfish,blazor,grid,templates,column,group,footer
published: True
position: 25
components: ["grid"]
---
# Column Group Footer

When the grid is grouped, the columns can display a footer with information about the column data [aggregates](slug:grid-aggregates) and some custom text/logic. The template is strongly typed and exposes the available aggregates values.

>caption Sample Column Group Footer Template

````RAZOR
@* Group by the Team column to see the results and aggregate data in the footer *@

<SunfishDataGrid Data=@GridData Groupable="true" Pageable="true" Height="650px">
    <GridAggregates>
        <GridAggregate Field=@nameof(Employee.Team) Aggregate="@GridAggregateType.Count" />
        <GridAggregate Field=@nameof(Employee.Salary) Aggregate="@GridAggregateType.Max" />
        <GridAggregate Field=@nameof(Employee.Salary) Aggregate="@GridAggregateType.Sum" />
    </GridAggregates>
        <SunfishGridColumn Field=@nameof(Employee.Name) Groupable="false" />
        <SunfishGridColumn Field=@nameof(Employee.Team) Title="Team">
            <GroupFooterTemplate>
                Team Members: <strong>@context.Count</strong>
            </GroupFooterTemplate>
        </SunfishGridColumn>
        <SunfishGridColumn Field=@nameof(Employee.Salary) Title="Salary" Groupable="false">
            <GroupFooterTemplate>
                @* you can use a group footer for non-groupable columns as well *@
                Total montly salary: @context.Sum
                <br />
                <span style="color: red;">Top paid employee: @context.Max</span>
            </GroupFooterTemplate>
        </SunfishGridColumn>
        <SunfishGridColumn Field=@nameof(Employee.ActiveProjects) Title="Active Projects">
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

>caption The result from the code snippet above after grouping by the `Team` column

![Blazor Grid Column Group Footer Template](images/column-group-footer-template.png)

## Using Components in Grid Group Footer Templates

@[template](/_contentTemplates/grid/common-link.md#using-components-in-templates)

## See Also

 * [Live Demo: Grid Templates](https://demos.sunfish.dev/blazor-ui/grid/templates)
 * [Live Demo: Grid Custom Editor Template](https://demos.sunfish.dev/blazor-ui/grid/custom-editor)
 * [Blazor Grid](slug:grid-overview)

