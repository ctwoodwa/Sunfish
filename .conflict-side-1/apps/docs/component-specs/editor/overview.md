---
title: Overview
page_title: Editor Overview
description: Overview of the Editor for Blazor.
slug: editor-overview
tags: sunfish,blazor,editor
published: True
position: 0
components: ["editor"]
---
# Blazor Editor Overview

The <a href = "https://sunfish.dev/blazor-ui/editor" target="_blank">Blazor HTML Editor component</a> enables your users to create rich textual content through a What-You-See-Is-What-You-Get (WYSIWYG) interface and delivers a set of tools for creating, editing, and formatting text, paragraphs, lists, and other HTML elements.

## Creating Editor

1. Use the `SunfishEditor` tag to add the component to your razor page.
1. Bind its `Value` to the `string` field you want to get the HTML content in.


## Get/Set Content

The Blazor HTML Editor has a `Value` parameter, similar to other input components. Use the `Value` parameter to get or set the HTML string that shows inside the Editor content area.

An empty string is a valid initial Editor `Value`, but after the user interacts with the component, the minimal component `Value` is at least an empty element (usually `"<p></p>"`). Note that [the Editor and the browser treat empty paragraphs differently](slug:editor-kb-missing-br-tags-in-value).

The Editor manages its content and `Value` depending on a [customizable schema](#architecture). The component strips all other tags and attributes for compliance and security reasons.


## Security

@[template](/_contentTemplates/editor/general.md#app-must-sanitize-content)


## Validation

You can use the standard Data Annotation attributes to validate the content of the Editor. For the performance reasons listed above, validation happens with the `DebounceDelay` delay, not immediately on every keystroke, like simpler inputs. [See the Validation article for an example on how to validate the content of the Editor...](slug:common-features/input-validation#editor)

## Large Content Support 

@[template](/_contentTemplates/editor/general.md#content-size-signalr)

## Resizing

The Editor allows you to resize:

#### Tables

Tables, their columns, and rows in the content area of Editor are resizable. To grab the resize handles, hover on the column or row borders. 

#### Images

Images in the content area of the Editor are resizable. To grab the resize handles, hover on the borders of the image.

## Architecture

The Sunfish Editor uses a `contenteditable` div with `document.execCommand` for rich text editing. The JS interop module is loaded as an inline IIFE. No external JS dependencies are required.

## Editor Parameters

The following table lists Editor parameters, which are not discussed elsewhere in the component documentation. 

@[template](/_contentTemplates/common/parameters-table-styles.md#table-layout)

| Parameter | Type and Default value | Description |
|-----------|------------------------|-------------|
| `Value`  | `string?` | The HTML content of the editor. Supports two-way binding via `@bind-Value` or use with the [`ValueChanged` event](slug:editor-events#valuechanged). |
| `ValueExpression` | `Expression<Func<string>>?` | Expression identifying the bound value for Blazor `EditForm` validation integration. |
| `EditMode` | `EditorEditMode` <br /> (`Edit`) | The edit mode: `Edit` (WYSIWYG), `Preview` (rendered HTML), or `Source` (raw HTML textarea). Supports two-way binding via `EditModeChanged`. |
| `Tools` | `IEnumerable<EditorTool>?` | The built-in toolbar tools to show. When `null`, a default set of 19 tools is displayed. |
| `CustomTools` | `IEnumerable<EditorCustomTool>?` | Custom tool buttons rendered after built-in tools in the toolbar. |
| `ToolbarTemplate` | `RenderFragment?` | Custom render template that replaces the entire toolbar. When set, built-in and custom tool buttons are not rendered. |
| `ChildContent` | `RenderFragment?` | Child content for child components such as `EditorPasteSettings`. |
| `Placeholder` | `string?` | Placeholder text shown when the editor is empty. Rendered via `data-placeholder` attribute in Edit mode and `placeholder` attribute in Source mode. |
| `DebounceDelay`  | `int` <br /> (`100`) | The time in milliseconds between content updates to the `Value`. Increase for large content to reduce repaints. |
| `Adaptive`  | `bool` | When `true`, the toolbar collapses overflowing items into a popup when the editor is too narrow. |
| `Width`  | `string?` | The width of the editor container. Default is `null` (themes apply `100%`). |
| `Height`  | `string` <br /> (`250px`) | The height of the content area. |
| `ReadOnly` | `bool` | When `true`, the editor is read-only. The toolbar is hidden and `contenteditable` is set to `false`. |
| `Disabled` | `bool` | When `true`, the editor is disabled. The toolbar is hidden and the content area is not interactive. |
| `AriaLabelledBy`  | `string?` | Maps to the `aria-labelledby` attribute on the WYSIWYG content area. |
| `AriaDescribedBy`  | `string?` | Maps to the `aria-describedby` attribute on the WYSIWYG content area. |

## Editor Reference and Methods

The Editor provides methods for programmatic operation. To use them, obtain a reference to the component through its `@ref` attribute.

| Method | Description |
| --- | --- |
| `ExecuteAsync(EditorCommandArgs)` | Executes a formatting command programmatically. Accepts `HtmlCommandArgs`, `FormatCommandArgs`, `LinkCommandArgs`, `ImageCommandArgs`, `TableCommandArgs`, `ColorCommandArgs`, `FontSizeCommandArgs`, `FontFamilyCommandArgs`, or `ToolCommandArgs`. |
| `ExecuteCommandAsync(string)` | Executes a command by name (e.g., `"bold"`, `"italic"`). |
| `SetModeAsync(EditorEditMode)` | Sets the edit mode programmatically. Captures content from WYSIWYG before switching to Source. |
| `GetHtmlAsync()` | Returns the current HTML content from the editor. |
| `ImportAsync(string, string)` | Imports content in the specified format (e.g., `"markdown"`, `"plaintext"`) via a registered `IEditorFormatConverter`, converting it to HTML and setting the editor value. |
| `ExportAsync(string)` | Exports the current HTML content to the specified format via a registered `IEditorFormatConverter`. |

>caption Insert HTML at the cursor position

````RAZOR
<button @onclick="@InsertHr">Insert HR</button>

<SunfishEditor @ref="@TheEditor" @bind-Value="@TheContent" />

@code {
    SunfishEditor? TheEditor;
    string TheContent = "<p>Lorem ipsum.</p><p>Dolor sit amet.</p>";

    async Task InsertHr()
    {
        if (TheEditor is not null)
            await TheEditor.ExecuteAsync(new HtmlCommandArgs { Command = "insertHtml", Html = "<hr />" });
    }
}
````

## Next Steps

* [Explore the Built-in Tools and Commands](slug:editor-built-in-tools)
* [Create Custom Tools](slug:editor-custom-tools)
* [Explore the Editor Edit Modes](slug:editor-edit-modes-overview)
* [Import and Export Data](slug:editor-import-export)
* [Learn more about the Editor Events](slug:editor-events)

## See Also

* [Live Demo: Editor](https://demos.sunfish.dev/blazor-ui/editor/overview)
* [Editor API Reference](slug:Sunfish.Blazor.Components.SunfishEditor)
