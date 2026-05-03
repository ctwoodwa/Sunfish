# Implementation Notes: Forms/Containers

## Design Decisions

### SunfishForm

1. **Standard Blazor EditContext pattern**: Follows the same pattern as Blazor's built-in `EditForm` — `Model` creates an internal `EditContext`, or an existing `EditContext` can be passed directly. Mutual exclusion enforced with `InvalidOperationException`.

2. **Submit event semantics**: When `OnSubmit` has a delegate, it fires on every submission (caller handles validation). When `OnSubmit` has no delegate, `EditContext.Validate()` is called and `OnValidSubmit`/`OnInvalidSubmit` fire based on the result. This matches ASP.NET Core Blazor conventions.

3. **CascadingValue for EditContext**: The `EditContext` is cascaded to all child components. Validation components (`SunfishValidationMessage`, etc.) consume this via `[CascadingParameter]` and throw if not found.

4. **FormValidation/FormItems/FormButtons as RenderFragments**: These are `RenderFragment` parameters, not child component registration patterns. This keeps the API simple while supporting the structured form layout from the spec.

5. **Layout parameters stored but not yet CSS-driven**: `Columns`, `Orientation`, `Size`, `ColumnSpacing`, `RowSpacing`, `ButtonsLayout` are parameters on the component. CSS provider integration for these layout modes is deferred — they will be wired to `CssProvider` methods when the form layout system is built out in Phase 3.

### Validation Components

1. **Three separate components**: Created `SunfishValidationMessage<TValue>`, `SunfishValidationSummary`, and `SunfishValidationTooltip<TValue>` as distinct components. The existing `SunfishValidation` is retained as a simple static message display.

2. **Generic type parameter on For expression**: `SunfishValidationMessage<TValue>` and `SunfishValidationTooltip<TValue>` use `Expression<Func<TValue>>` for the `For` parameter, matching Blazor's `ValidationMessage<TValue>` pattern. This provides compile-time type safety for model property references.

3. **OnValidationStateChanged subscription**: All validation components subscribe to `EditContext.OnValidationStateChanged` (not `OnFieldChanged`) to detect when validation messages change. This fires after `EditContext.Validate()` and after `NotifyValidationStateChanged()`.

4. **Proper lifecycle management**: Components track the previous `EditContext` to handle cascading parameter changes. Event subscriptions are properly cleaned up in `Dispose`.

### SunfishField / SunfishLabel

1. **Validation-aware CSS classes**: Both components check the cascading `EditContext` for validation errors on the associated field (identified by `Id` for Field, `For` for Label) and add `mar-field--invalid` / `mar-label--invalid` CSS classes.

2. **SunfishField renders a label**: When `Text` is set, `SunfishField` renders a `<label>` element inside the field container using `CssProvider.LabelClass()`. This provides a simpler API than requiring a separate `SunfishLabel` inside every field.

3. **Floating label deferred**: The spec's floating label animation (move on focus, coordinate placeholder) requires JS interop. This is deferred to Phase 2 and may result in a separate `SunfishFloatingLabel` component.

## Code Notes

### Key Files Changed/Created

| File | Action |
|------|--------|
| `Forms/Containers/SunfishForm.razor` | Rewritten with EditContext, events, child RenderFragments |
| `Forms/Containers/SunfishField.razor` | Enhanced with Text, Id, validation CSS |
| `Forms/Containers/SunfishLabel.razor` | Enhanced with Text, Id, validation CSS |
| `Forms/Containers/SunfishValidation.razor` | Retained as-is (backward compatible) |
| `Forms/Containers/SunfishValidationMessage.razor` | **NEW** — EditContext per-field messages |
| `Forms/Containers/SunfishValidationSummary.razor` | **NEW** — EditContext all messages |
| `Forms/Containers/SunfishValidationTooltip.razor` | **NEW** — EditContext per-field tooltip |
| `Core/Enums/FormEnums.cs` | **NEW** — FormOrientation, FormValidationMessageType, FormButtonsLayout |
| `Core/Models/FormUpdateEventArgs.cs` | **NEW** — OnUpdate event args |
