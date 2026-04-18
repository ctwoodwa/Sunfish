---
title: Wai-Aria Support
page_title: Sunfish UI for Blazor FileManager Documentation | FileManager Accessibility
description: "Get started with the Sunfish UI for Blazor FileManager and learn about its accessibility support for WAI-ARIA, Section 508, and WCAG 2.2."
tags: sunfish,blazor,accessibility,wai-aria,wcag
slug: filemanager-wai-aria-support 
position: 50 
---

# Blazor FileManager Accessibility

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)



Out of the box, the Sunfish UI for Blazor FileManager provides extensive accessibility support and enables users with disabilities to acquire complete control over its features.


The FileManager is compliant with the [Web Content Accessibility Guidelines (WCAG) 2.2 AA](https://www.w3.org/TR/WCAG22/) standards and [Section 508](https://www.section508.gov/) requirements, follows the [Web Accessibility Initiative - Accessible Rich Internet Applications (WAI-ARIA)](https://www.w3.org/WAI/ARIA/apg/) best practices for implementing the [keyboard navigation](#keyboard-navigation) for its `component` role, provides options for managing its focus and is tested against the most popular screen readers.

## WAI-ARIA


This section lists the selectors, attributes, and behavior patterns supported by the component and its composite elements, if any.


The FileManager component is a composite component that is used to represent file system-like structure. It contains:


 - `ToolBar` - on top of the component
 - `Splitter` - it separates the panes in the component
 - `TreeView` - in its left pane.
 - `Breadcrumb` - in the top of its center pane
 - `ListView` or a `Grid` - in its center pane.
 - `k-filemanager-preview` element - in its right pane


Each component implements its own dedicated ARIA spec.


The FileManager component integrates the ToolBar component and follows its WAI-ARIA spec:

[ToolBar accessibility specification](../../toolbar/accessibility/wai-aria-support.md)


The component that organizes the inner structure of the FileManager is a Splitter:

[Splitter accessibility specification](../../splitter/accessibility/wai-aria-support.md)


The main navigation component in the FileManager is the TreeView:

[TreeView accessibility specification](../../treeview/accessibility/wai-aria-support.md)


The helper navigation component in the FileManager is the Breadcrumb:

[Breadcrumb accessibility specification](../../breadcrumb/accessibility/wai-aria-support.md)


The component placed in the main pain of the FileManager can be a selectable ListView:

[ListView accessibility specification](../../listview/accessibility/wai-aria-support.md)


or it can be a Grid:

[Grid accessibility specification](../../grid/accessibility/wai-aria-support.md)


Apart from that the `.k-filemanager-preview` element must be focusable, so that its content would be comunicated to the users:

| Selector | Attribute | Usage |
| -------- | --------- | ----- |
| `.k-filemanager-preview` | `tabindex=0` | The element must be focusable, so that its content would be comunicated to the users. |

## Section 508


The FileManager is fully compliant with the [Section 508 requirements](http://www.section508.gov/).

## Testing


The FileManager has been extensively tested automatically with [axe-core](https://github.com/dequelabs/axe-core) and manually with the most popular screen readers.

> To report any accessibility issues, contact the team through the [Sunfish Support System](https://sunfish.dev/account/support-center).

### Screen Readers


The FileManager has been tested with the following screen readers and browsers combinations:

| Environment | Tool |
| ----------- | ---- |
| Firefox | NVDA |
| Chrome | JAWS |
| Microsoft Edge | JAWS |



## Keyboard Navigation

For details on how the keyboard navigation works in Sunfish UI for Blazor, refer to the [Accessibility Overview](slug:accessibility-overview#keyboard-navigation) article.

## See Also

* [Blazor FileManager Demos](https://demos.sunfish.dev/blazor-ui/filemanager/overview)
* [Accessibility in Sunfish UI for Blazor](slug:accessibility-overview)