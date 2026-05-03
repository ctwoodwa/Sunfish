---
title: Wai-Aria Support
page_title: Sunfish UI for Blazor Gantt Documentation | Gantt Accessibility
description: "Get started with the Sunfish UI for Blazor Gantt and learn about its accessibility support for WAI-ARIA, Section 508, and WCAG 2.2."
tags: sunfish,blazor,accessibility,wai-aria,wcag
slug: gantt-wai-aria-support 
position: 50 
---

# Blazor Gantt Accessibility

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)



Out of the box, the Sunfish UI for Blazor Gantt provides extensive accessibility support and enables users with disabilities to acquire complete control over its features.


The Gantt is compliant with the [Web Content Accessibility Guidelines (WCAG) 2.2 AA](https://www.w3.org/TR/WCAG22/) standards and [Section 508](https://www.section508.gov/) requirements, follows the [Web Accessibility Initiative - Accessible Rich Internet Applications (WAI-ARIA)](https://www.w3.org/WAI/ARIA/apg/) best practices for implementing the [keyboard navigation](#keyboard-navigation) for its `component` role, provides options for managing its focus and is tested against the most popular screen readers.

## WAI-ARIA


This section lists the selectors, attributes, and behavior patterns supported by the component and its composite elements, if any.


The Gantt component is a composite component that is used to represent project planning.

| Selector | Attribute | Usage |
| -------- | --------- | ----- |
| `.k-gantt` | `role=application` | Indicates the Gantt's role as an application. |


The Gantt component integrates the toolbar component and follows its wai-aria support.

[ToolBar accessibility specification](../../toolbar/accessibility/wai-aria-support.md)


The main inner component in the Gantt is the TreeList.

[TreeList accessibility specification](../../treelist/accessibility/wai-aria-support.md)


Another part of the component is the Splitter component and Wai-Aria support.

[Splitter accessibility specification](../../splitter/accessibility/wai-aria-support.md)


The following Wai-Aria support is implemented in the TimeLine of the Gantt.

| Selector | Attribute | Usage |
| -------- | --------- | ----- |
| `.k-gantt-timeline .k-grid-content` | `role=presentation` | Container for the timeline content. |
| `.k-gantt-timeline .k-gantt-rows` | `role=presentation` | Used to build the accessibility tree. |
| `.k-gantt-timeline .k-gantt-columns` | `role=presentation` | Used to build the accessibility tree. |
| `.k-gantt-timeline .k-gantt-tasks` | `role=presentation` | Used to build the accessibility tasks. |
| `.k-gantt-timeline .k-task` | `role=img` | Represents a timeline bar as a visual element. |
|  | `aria-label="{title}: {start} – {end}"` | Provides an accessible description of the task's title and date range. |
|  | `aria-describedby=.k-tooltip id` | Gives more details for the task through its tooltip. |
| `.k-gantt-timeline .k-task .k-task-complete` | `aria-hidden=true` | Hides the status element from the task. |
| `.k-gantt-timeline .k-task .k-task-actions` | `aria-hidden=true` | Hides the actions element from the task. |
| `[role=columnheader]` (sortable) | `aria-sort` | Applied to sortable column headers. Values: `ascending`, `descending`, or `none` depending on current sort state. |
| `.k-gantt [role=treegrid]` | `tabindex=0` | Enables keyboard entry into the treegrid container. |
| `.k-gantt [role=row]` (focused) | `tabindex=0` | The currently focused row receives `tabindex="0"` (roving tabindex pattern). |
| `.k-gantt [role=row]` (unfocused) | `tabindex=-1` | All non-focused rows receive `tabindex="-1"` to remove them from the tab order. |

## Section 508


The Gantt is fully compliant with the [Section 508 requirements](http://www.section508.gov/).

## Testing


The Gantt has been extensively tested automatically with [axe-core](https://github.com/dequelabs/axe-core) and manually with the most popular screen readers.

> To report any accessibility issues, contact the team through the [Sunfish Support System](https://sunfish.dev/account/support-center).

### Screen Readers


The Gantt has been tested with the following screen readers and browsers combinations:

| Environment | Tool |
| ----------- | ---- |
| Firefox | NVDA |
| Chrome | JAWS |
| Microsoft Edge | JAWS |



## Keyboard Navigation

For details on how the keyboard navigation works in Sunfish UI for Blazor, refer to the [Accessibility Overview](slug:accessibility-overview#keyboard-navigation) article.

## See Also

* [Blazor Gantt Demos](https://demos.sunfish.dev/blazor-ui/gantt/overview)
* [Accessibility in Sunfish UI for Blazor](slug:accessibility-overview)