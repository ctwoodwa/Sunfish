#define-in-sunfishlayout

1. Create a new layout file in the app, for example, `SunfishLayout.razor`.
1. Place the new layout in the same folder as the default application layout `MainLayout.razor`.
1. Add a `<SunfishRootComponent>` tag to the new layout and set `@Body` as the root component's child content.
1. Make the new layout a parent of the default application layout.

>caption Adding SunfishRootComponent to a new layout

<div class="skip-repl"></div>

````RAZOR SunfishLayout.razor
@inherits LayoutComponentBase

<SunfishRootComponent>
    @Body
</SunfishRootComponent>
````
````RAZOR MainLayout.razor
@inherits LayoutComponentBase
@layout SunfishLayout

@* The other MainLayout.razor content remains the same. *@
````

#end
