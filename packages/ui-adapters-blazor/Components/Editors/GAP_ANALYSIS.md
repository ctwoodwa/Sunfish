# SunfishEditor Gap Analysis

## Summary

The `SunfishEditor.razor` component is a bare-bones HTML editor implemented as a plain `<textarea>` with a simple toolbar that appends raw HTML snippets. The documentation specs describe a full WYSIWYG rich-text editor powered by ProseMirror with an `<iframe>`/`<div>` content-editable area, debounced value updates, programmatic command execution, paste cleanup, custom tools, AI integration, ProseMirror schema/plugin customization, and accessibility compliance. The implementation gap is **severe** -- the component is essentially a scaffold/placeholder that fulfills almost none of the documented contract.

---

## Spec to Code Gaps (Documented but not correctly implemented)

### Parameters

| Documented Parameter | Spec Details | Implementation Status | Severity |
|---|---|---|---|
| `DebounceDelay` | `int`, default `100`. Debounces `Value` updates and events. | **Not implemented.** Every keystroke fires `ValueChanged`/`OnChange` synchronously with no debounce. | [High] |
| `EditMode` | `EditorEditMode` enum (`Iframe` default, `Div`). Controls whether the content area is an `<iframe>` or `<div contenteditable>`. | **Not implemented.** Content area is always a `<textarea>` -- neither an iframe nor a contenteditable div. | [High] |
| `Width` | `string`, default `null` (themes apply `100%`). | **Not implemented.** No `Width` parameter exists. The textarea has `width:100%` hardcoded in inline style. | [Medium] |
| `Adaptive` | `bool`. Toolbar auto-hides overflowing items into a popup on narrow widths. | **Not implemented.** No `Adaptive` parameter or responsive toolbar behavior. | [Medium] |
| `AriaLabelledBy` | `string`. Maps to `aria-labelledby`. | **Not implemented.** No parameter. | [Medium] |
| `AriaDescribedBy` | `string`. Maps to `aria-describedby`. | **Not implemented.** No parameter. | [Medium] |
| `Plugins` | `string`. Name of a JS function returning custom ProseMirror plugins. | **Not implemented.** No `Plugins` parameter or ProseMirror integration. | [High] |
| `Tools` | `List<IEditorTool>` with `IEditorTool` interface supporting buttons, dropdowns, color pickers, button groups, custom tools. | **Partially implemented.** Parameter exists as `IEnumerable<EditorTool>?` using a flat enum, not the documented `List<IEditorTool>` with rich tool objects (`Bold()`, `EditorButtonGroup(...)`, `FontFamily()`, `ForeColor()`, etc.). No support for tool grouping, dropdown tools, color pickers, or tool customization (Title, Data, Colors, etc.). | [High] |
| `Height` | `string`, default `250px` per spec. | **Partially implemented.** Parameter exists but defaults to `300px` instead of spec's `250px`. | [Low] |

### Events

| Documented Event | Spec Details | Implementation Status | Severity |
|---|---|---|---|
| `ValueChanged` | Debounced by `DebounceDelay` (100ms default). | **Partially implemented.** Event exists but fires on every `oninput` with no debounce. | [High] |

### Methods

| Documented Method | Spec Details | Implementation Status | Severity |
|---|---|---|---|
| `ExecuteAsync` | Programmatically executes built-in editor commands (`bold`, `insertHtml`, `insertTable`, `createLink`, `insertImage`, `foreColor`, `backColor`, etc.) using typed argument classes (`ToolCommandArgs`, `HtmlCommandArgs`, `FormatCommandArgs`, `TableCommandArgs`, `LinkCommandArgs`, `ImageCommandArgs`). | **Not implemented.** No `ExecuteAsync` method exists. No command argument classes. The component cannot be used via `@ref` for programmatic operations. | [High] |

### Behaviors / Features

| Documented Feature | Spec Details | Implementation Status | Severity |
|---|---|---|---|
| WYSIWYG editing | Content area is a ProseMirror-powered contenteditable region rendering rich text in real-time. | **Not implemented.** Uses a plain `<textarea>` showing raw HTML source. No rich-text rendering during editing. | [High] |
| ProseMirror integration | Editor is built on ProseMirror with schema-based content management, plugins, and document model. | **Not implemented.** No ProseMirror dependency or integration whatsoever. | [High] |
| Tool actions (Bold, Italic, etc.) | Tools apply formatting to selected text via ProseMirror commands. | **Not implemented.** Tools append raw HTML snippets to the end of the value. They do not wrap selected text, do not interact with cursor position, and have no undo integration. | [High] |
| Paste cleanup | `<EditorSettings>` / `<EditorPasteSettings>` child components with parameters: `ConvertMsLists`, `RemoveMsClasses`, `RemoveMsStyles`, `RemoveHtmlComments`, `StripTags`, `RemoveAttributes`, `RemoveAllAttributes`. | **Not implemented.** No paste cleanup, no `EditorSettings` child component. | [High] |
| Custom tools | `<EditorCustomTools>` / `<EditorCustomTool>` child components for declarative custom tool definitions with `Name` parameter and arbitrary Razor content. | **Not implemented.** No child content or render fragment support for custom tools. | [High] |
| AI integration | AIPrompt component and inline prompt for AI-assisted editing. | **Not implemented.** | [Medium] |
| ProseMirror schema customization | `Schema` parameter referencing a JS function to modify or replace the default ProseMirror schema. | **Not implemented.** | [Medium] |
| Content security | Strips `<script>` tags and DOM event handler attributes from content. | **Not implemented.** The preview mode renders `Value` via `(MarkupString)` with no sanitization, which is an XSS vulnerability. | [High] |
| Table/image resizing | Tables and images in the content area are resizable with drag handles. | **Not implemented.** | [Medium] |
| Validation | Works with Data Annotation validation, debounced. | **Not implemented.** No `EditContext` integration or validation support. | [Medium] |
| Accessibility / WAI-ARIA | `role=textbox`, `aria-readonly`, `aria-labelledby`, keyboard navigation, screen reader support. | **Not implemented.** The textarea has basic HTML semantics but no ARIA roles or attributes beyond `readonly`. | [Medium] |
| Iframe edit mode styling | Editor adds CSS rules to iframe document for table borders, etc. | **Not implemented.** No iframe mode at all. | [Medium] |
| Large content support | Performance optimizations for large HTML content. | **Not implemented.** | [Low] |

### Built-in Tools Missing from `EditorTool` Enum

The spec documents the following tools that have no corresponding enum value:

| Missing Tool | Spec Class Name | Severity |
|---|---|---|
| `FontFamily` | `FontFamily` (dropdown) | [Medium] |
| `CreateLink` | `CreateLink` (spec uses `CreateLink`, enum uses `Link`) | [Low] -- naming mismatch only |
| `InsertImage` | `InsertImage` (spec uses `InsertImage`, enum uses `Image`) | [Low] -- naming mismatch only |
| `InsertTable` | `InsertTable` (spec uses `InsertTable`, enum uses `Table`) | [Low] -- naming mismatch only |
| `ViewHtml` | `ViewHtml` (spec uses `ViewHtml`, enum uses `ViewSource`) | [Low] -- naming mismatch only |
| Table manipulation tools | `AddColumnBefore`, `AddColumnAfter`, `AddRowBefore`, `AddRowAfter`, `DeleteColumn`, `DeleteRow`, `DeleteTable`, `MergeCells`, `SplitCell` | [Medium] |

---

## Code to Spec Gaps (Implemented but not documented)

### Parameters

| Implemented Parameter | Details | Severity |
|---|---|---|
| `OnChange` | `EventCallback<string>` fired alongside `ValueChanged` on every input. Not documented anywhere in specs. | [Medium] |
| `Placeholder` | `string?` parameter rendered as the textarea `placeholder` attribute. Not documented in the overview parameter table. | [Medium] |

### Behaviors

| Implemented Behavior | Details | Severity |
|---|---|---|
| Preview toggle | A "Preview" button toggles between textarea and an HTML-rendered preview using `(MarkupString)`. Not documented. Also poses an XSS risk since content is rendered unsanitized. | [High] |
| Tool snippet appending | Clicking a toolbar tool appends a raw HTML snippet to the end of `Value` instead of applying formatting to selected text. This is undocumented behavior that contradicts the spec's WYSIWYG model. | [High] |
| `ClearFormatting` enum value | Present in `EditorTool` enum but `GetToolSnippet` returns `""` (no-op). Spec documents a `cleanFormatting` command but not as a built-in tool with a button. | [Low] |
| `HorizontalRule` enum value | Present as a tool button but not documented as a toolbar tool class in specs (only as a programmatic `insertHtml` example). | [Low] |

### Constraints

| Constraint | Details | Severity |
|---|---|---|
| Flat enum-based tool model | `Tools` accepts `IEnumerable<EditorTool>` (a flat enum), making it impossible to create button groups, customize tool appearance, provide dropdown data, or mix built-in/custom tools as the spec requires. | [High] |
| No JS interop | The component is pure server-side Razor with no JavaScript interop. This makes it fundamentally impossible to implement ProseMirror, iframe edit mode, paste cleanup, cursor-position-aware operations, or keyboard shortcuts. | [High] |

---

## Recommended Changes

### Implementation Updates

1. **[Critical] Integrate ProseMirror via JS interop.** The entire editing surface must be replaced. The current `<textarea>` approach cannot deliver WYSIWYG editing. This is the single largest gap and blocks most other fixes.

2. **[Critical] Replace the `EditorTool` enum with `IEditorTool` interface hierarchy.** Implement `ButtonTool`, `DropDownListTool`, `ColorTool`, `EditorButtonGroup`, and `CustomTool` classes to match the documented `List<IEditorTool>` contract.

3. **[Critical] Implement `ExecuteAsync` method.** Add the documented programmatic command API with all typed argument classes (`ToolCommandArgs`, `HtmlCommandArgs`, `FormatCommandArgs`, `TableCommandArgs`, `LinkCommandArgs`, `ImageCommandArgs`).

4. **[Critical] Fix XSS vulnerability.** The preview mode renders raw HTML via `(MarkupString)` with no sanitization. At minimum, strip `<script>` tags and event handler attributes as the spec requires.

5. **[High] Add missing parameters:** `DebounceDelay`, `EditMode`, `Width`, `Adaptive`, `AriaLabelledBy`, `AriaDescribedBy`, `Plugins`.

6. **[High] Implement debounced ValueChanged.** Add timer-based debounce controlled by `DebounceDelay`.

7. **[High] Implement paste cleanup.** Add `EditorSettings`/`EditorPasteSettings` child components.

8. **[High] Implement custom tools.** Add `EditorCustomTools`/`EditorCustomTool` child component support.

9. **[Medium] Add table manipulation tools** to the `EditorTool` enum or tool class hierarchy (`AddColumnBefore`, `AddColumnAfter`, etc.).

10. **[Medium] Add `FontFamily` tool** -- it is completely absent from the enum.

11. **[Medium] Implement accessibility.** Add ARIA attributes, keyboard navigation, and screen reader support.

12. **[Low] Fix `Height` default.** Change from `300px` to `250px` to match spec.

### Documentation Updates

1. **[Medium] Document `OnChange` event** or remove it from the implementation if it is redundant with `ValueChanged`.

2. **[Medium] Document `Placeholder` parameter** in the overview parameter table.

3. **[Medium] Document preview mode** or remove it if it is not part of the intended API.

4. **[Low] Reconcile tool naming.** The enum uses `Link`/`Image`/`Table`/`ViewSource` while the spec uses `CreateLink`/`InsertImage`/`InsertTable`/`ViewHtml`. Decide on canonical names and align both sides.

---

## Open Questions / Ambiguities

1. **Is the current implementation intentionally a placeholder/scaffold?** The gap between spec and code is so large that the component appears to be a stub awaiting real implementation rather than an incomplete feature.

2. **Should `OnChange` coexist with `ValueChanged`?** The spec only documents `ValueChanged`. If `OnChange` provides different semantics (e.g., fires on blur rather than on input), this should be clarified. Currently both fire identically on every keystroke.

3. **What is the intended behavior of the Preview toggle?** The spec does not mention a preview mode. Is this a temporary development aid or an intended feature?

4. **Which tool naming convention should win?** The enum uses shorter names (`Link`, `Image`, `Table`) while the spec uses action-prefixed names (`CreateLink`, `InsertImage`, `InsertTable`). The enum names are arguably cleaner but break API compatibility with documented examples.

5. **Should the component support both `IEnumerable<EditorTool>` (simple enum) and `List<IEditorTool>` (rich objects)?** A convenience overload using the enum could coexist with the full `IEditorTool` API for simple use cases.

6. **What is the AI integration priority?** The spec documents AIPrompt and inline prompt integration, but these depend on a separate `AIPrompt` component that may or may not exist yet.

7. **ProseMirror dependency management.** The spec states "we have taken care of everything internally" for ProseMirror assets. How will the JS bundle be delivered -- embedded resource, CDN, or npm package?
