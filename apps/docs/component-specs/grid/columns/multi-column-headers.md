---
title: Multi-Column Headers
page_title: Multi Column Headers
description: Stack multiple columns under a single header in the data grid for Blazor.
slug: grid-columns-multiple-column-headers
tags: sunfish,blazor,grid,column,multi,multiple,headers
published: True
position: 60
components: ["grid"]
---
# Multi-Column Headers

The Grid allows you to stack several columns under one header to visually group relevant fields for your end users.

To use multiple column headers:

1. Define a `GridColumn` instance for each multi-column header you want. Set its `Title` or [`HeaderTemplate`](slug:grid-templates-column-header).
1. Under its `<Columns>` nested tag, add the columns you want it to contain.

While you can set all the parameters of such a multi-column header column, it only supports and works with the `Title`, and the nested `HeaderTemplate` and `Columns` tags (templates).

You will find the following sections in this article:

* [Basic Example](#basic-example)
* [Behavior With Other Features](#behavior-with-other-features)

## Basic Example

The following code snippet shows how you can group columns in the Grid in multi-column headers. You can also use "regular" columns at the root level, not all of them have to be column groups.

>caption Multiple Column Headers in the Grid

![multi-column headers example](images/multi-column-headers-overview.png)

````RAZOR
@* See the root-level GridColumn tags that have their own Columns collections *@

<SunfishDataGrid Data=@GridData
             Pageable="true" Sortable="true" Resizable="true" Reorderable="true"
             ShowColumnMenu="true" FilterMode="@GridFilterMode.FilterMenu"
             Width="800px" Height="400px">
        <SunfishGridColumn Title="Personal Information">
            <Columns>
                <SunfishGridColumn Field=@nameof(Customer.FirstName) Title="First Name" Width="100px" />
                <SunfishGridColumn Field=@nameof(Customer.LastName) Title="Last Name" Width="100px" />
            </Columns>
        </SunfishGridColumn>
        <SunfishGridColumn Title="Company">
            <Columns>
                <SunfishGridColumn Field=@nameof(Customer.CompanyName) Title="Name" />
                <SunfishGridColumn Field=@nameof(Customer.HasCompanyContract) Title="Has Contract" Width="120px" />
            </Columns>
        </SunfishGridColumn>
        <SunfishGridColumn Title="Contact Details">
            <Columns>
                <SunfishGridColumn Field="@nameof(Customer.Email)" Title="Email"></SunfishGridColumn>
                <SunfishGridColumn Field="@nameof(Customer.Phone)" Title="Phone"></SunfishGridColumn>
                <SunfishGridColumn Field="@nameof(Customer.City)" Title="City"></SunfishGridColumn>
            </Columns>
        </SunfishGridColumn>
        <SunfishGridColumn Title="Admin Settings">
            <Columns>
                <SunfishGridColumn Field=@nameof(Customer.Id) Title="UserID" />
                <SunfishGridColumn Field=@nameof(Customer.PasswordHash) Title="Pass Hash" Width="100px" />
            </Columns>
        </SunfishGridColumn>
</SunfishDataGrid>

@code {
    public List<Customer> GridData { get; set; }

    public class Customer
    {
        public int Id { get; set; }
        public string PasswordHash { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string CompanyName { get; set; }
        public bool HasCompanyContract { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string City { get; set; }
    }

    // generation of dummy data
    protected override void OnInitialized()
    {
        GridData = GenerateData();
    }

    List<Customer> GenerateData()
    {
        var data = new List<Customer>();

        string[] fNames = new string[] { "Nancy", "John", "Orlando", "Jane", "Bob", "Juan" };
        string[] lNames = new string[] { "Harris", "Gates", "Smith", "Caprio", "Gash", "Gee" };
        string[] cNames = new string[] { "Acme", "Northwind", "Contoso" };
        string[] cities = new string[] { "Denver", "New York", "LA", "London", "Paris", "Helsinki", "Moscow", "Sofia" };
        Random rnd = new Random();

        for (int i = 0; i < 150; i++)
        {
            string fName = fNames[rnd.Next(0, fNames.Length)];
            string lName = lNames[rnd.Next(0, lNames.Length)];
            string cName = cNames[rnd.Next(0, cNames.Length)];
            data.Add(new Customer
            {
                Id = i,
                PasswordHash = "not shown",
                FirstName = fName,
                LastName = lName,
                CompanyName = cName,
                HasCompanyContract = i % 3 == 0,
                Email = $"{fName}.{lName}@{cName}.com",
                Phone = $"{rnd.Next(100, 999)}-555-{rnd.Next(100, 999)}",
                City = cities[rnd.Next(0, cities.Length)]
            });
        }

        return data;
    }
}
````





## Behavior With Other Features

@[template](/_contentTemplates/grid/common-link.md#multi-column-headers-feature-integration)






## See Also

  * [Live Demo: Multi-Column Headers](https://demos.sunfish.dev/blazor-ui/grid/multi-column-headers)
  * [Blazor Grid](slug:grid-overview)