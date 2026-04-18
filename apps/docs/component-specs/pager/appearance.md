---
title: Appearance
page_title: Pager Appearance
description: Appearance settings of the Pager for Blazor.
slug: pager-appearance
tags: sunfish,blazor,pager,appearance
published: True
position: 35
components: ["pager"]
---
# Appearance Settings

This article outlines the available Pager parameters, which control its appearance.

## Size

You can increase or decrease the size of the Pager by setting the `Size` parameter to a member of the `Sunfish.Blazor.ThemeConstants.Pager.Size` class:

| Class members | Manual declarations |
|---------------|--------|
| `Small`   |`sm`|
| `Medium`<br /> default value   |`md`|
| `Large`   |`lg`|

>caption The built-in sizes

````RAZOR
@{
    var fields = typeof(Sunfish.Blazor.ThemeConstants.Pager.Size)
        .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static
        | System.Reflection.BindingFlags.FlattenHierarchy)
        .Where(field => field.IsLiteral && !field.IsInitOnly).ToList();

    foreach (var field in fields)
    {
        string size = field.GetValue(null).ToString();

        <div style="float:left; margin: 20px;">
            <SunfishPager Size="@size"
                          PageSize="10"
                          Total="20" 
                          PageSizes="@(new List<int?> {5, 10, null})">
            </SunfishPager>
        </div>
    }
}
````

## See Also

  * [Live Demo: Pager Appearance](https://demos.sunfish.dev/blazor-ui/pager/appearance)