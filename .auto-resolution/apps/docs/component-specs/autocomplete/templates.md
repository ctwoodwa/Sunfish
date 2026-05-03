---
title: Templates
page_title: AutoComplete - Templates
description: Templates in the AutoComplete for Blazor.
slug: autocomplete-templates
tags: sunfish,blazor,combo,autocomplete,templates
published: True
position: 20
components: ["autocomplete"]
---
# AutoComplete Templates

The AutoComplete component allows you to change what is rendered in its items, header and footer through templates.

>caption In this article:

* [Item Template](#item-template)
* [Header Template](#header-template)
* [Footer Template](#footer-template)
* [No Data Template](#no-data-template)
* [Example](#example)

## Item Template

@[template](/_contentTemplates/dropdowns/templates.md#item-template)

## Header Template

@[template](/_contentTemplates/dropdowns/templates.md#header-template)

## Footer Template

@[template](/_contentTemplates/dropdowns/templates.md#footer-template)

## No Data Template

@[template](/_contentTemplates/dropdowns/templates.md#no-data-template)

## Example

>caption Using AutoComplete Templates

````RAZOR
@* AutoComplete component with HeaderTemplate, ItemTemplate, FooterTemplate and NoDataTemplate *@

<p>
    <SunfishCheckBox @bind-Value="@IsDataAvailable" OnChange="@OnCheckBoxChangeHandler" />
    AutoComplete has data
</p>

<SunfishAutoComplete Data="@AutoCompleteData" @bind-Value="@Role" Placeholder="Write your position">
    <HeaderTemplate>
        <strong>Write your own if you don't see it in the list</strong>
    </HeaderTemplate>
    <ItemTemplate>
        Are you a <strong>@context</strong>
    </ItemTemplate>
    <FooterTemplate>
        <h6>Total Positions: @AutoCompleteData.Count()</h6>
    </FooterTemplate>
    <NoDataTemplate>
        <div class="no-data-template">
            <SunfishSvgIcon Icon="@SvgIcon.FilesError" Size="@ThemeConstants.SvgIcon.Size.Large"></SunfishSvgIcon>
            <p>No items available</p>
        </div>
    </NoDataTemplate>
</SunfishAutoComplete>

@code {
    private string Role { get; set; }

    private bool IsDataAvailable { get; set; } = true;

    private List<string> AutoCompleteData { get; set; }

    private List<string> SourceData { get; set; } = new List<string> { "Manager", "Developer", "QA", "Technical Writer", "Support Engineer", "Sales Agent", "Architect", "Designer" };

    protected override void OnInitialized()
    {
        AutoCompleteData = SourceData;
    }

    private void OnCheckBoxChangeHandler()
    {
        if (IsDataAvailable)
        {
            AutoCompleteData = new List<string>(SourceData);
        }else{
            AutoCompleteData = new List<string>();
        }
    }
}
````

## See Also

  * [Live Demo: AutoComplete Templates](https://demos.sunfish.dev/blazor-ui/autocomplete/templates)
   
  