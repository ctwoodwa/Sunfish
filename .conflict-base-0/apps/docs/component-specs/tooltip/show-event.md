---
title: Show Event
page_title: Tooltip - Show Event
description: Choose when the Tooltip for Blazor shows up.
slug: tooltip-show-event
tags: sunfish,blazor,tooltip,show,event
published: true
position: 3
components: ["tooltip"]
---
# Tooltip Show Event

You can control what user interaction with the Tooltip target shows the tooltip through the `ShowOn` parameter.

It takes a member of the `Sunfish.Blazor.TooltipShowEvent` enum:

* `Hover` - the default value
* `Click`

By default, the tooltip shows on hover (mouseover) of its target, just like the browser tooltips that the Tooltip component replaces.

> Changing the `ShowEvent` dynamically at runtime is not supported at this stage.

>caption Explore the show events of the Tooltip

````RAZOR
@* Setting a show event is not mandatory, it defaults to Hover *@

<SunfishTooltip TargetSelector="#hoverTarget" ShowOn="@TooltipShowEvent.Hover">
</SunfishTooltip>

<div id="hoverTarget" title="lorem ipsum">
    <strong>Hover</strong> me to see the tooltip.
</div>

<SunfishTooltip TargetSelector="#clickTarget" ShowOn="@TooltipShowEvent.Click">
</SunfishTooltip>

<div id="clickTarget" title="dolor sit amet">
    <strong>Click</strong> me to see the tooltip.
    Then click somewhere to hide the tooltip.
</div>

@code {
    TooltipShowEvent showEvent { get; set; } = TooltipShowEvent.Hover;
}

<style>
    #hoverTarget {
        position: absolute;
        top: 200px;
        left: 200px;
        width: 200px;
        background: yellow;
    }

    #clickTarget {
        position: absolute;
        top: 200px;
        left: 500px;
        width: 200px;
        background: green;
    }
</style>
````

## Next Steps

* [Explore ToolTip Templates](slug:tooltip-template)

## See Also

* [Blazor Tooltip Overview](slug:tooltip-overview)
* [Live Demo: Tooltip Show Event](https://demos.sunfish.dev/blazor-ui/tooltip/show-event)
