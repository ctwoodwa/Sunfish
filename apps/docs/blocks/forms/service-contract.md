---
uid: block-forms-service-contract
title: Forms — Service Contract
description: The FormBlock<TModel> component parameters, slots, events, and submission-state shape.
---

# Forms — Service Contract

## Overview

`Sunfish.Blocks.Forms` does not ship a back-end service interface. It is a Blazor-component
block, so its effective contract is the set of component parameters, slots, events, and
the observable `FormBlockState`.

## Component: `FormBlock<TModel>`

Source: `packages/blocks-forms/FormBlock.razor`

Generic constraint: `where TModel : class`.

### Parameters

| Parameter        | Type                         | Default   | Notes |
|------------------|------------------------------|-----------|-------|
| `Model`          | `TModel`                     | (required)| **Required.** The POCO bound to the form. |
| `Id`             | `string?`                    | `null`    | Passed through to `SunfishForm.Id`. |
| `Columns`        | `int`                        | `1`       | Column count for the form layout. |
| `SubmitText`     | `string?`                    | `"Submit"`| Default button label. Ignored when `FormButtons` is supplied. |
| `FormItems`      | `RenderFragment?`            | `null`    | Slot for form-item markup. |
| `FormButtons`    | `RenderFragment?`            | `null`    | Optional slot that replaces the default submit button. |
| `OnSubmitted`    | `EventCallback<TModel>`      | —         | Fires once on successful, valid submission. |
| `OnStateChanged` | `EventCallback<FormBlockState>` | —      | Fires whenever `State` changes. |

### Fixed behaviour

The block configures the underlying `SunfishForm` with the following defaults — callers
cannot override these without using `SunfishForm` directly:

- `Orientation = FormOrientation.Vertical`
- `ValidationMessageType = FormValidationMessageType.Inline`
- `FormValidation = <SunfishValidation />`

### Default submit button

If `FormButtons` is `null`, the block renders a single primary `SunfishButton` with:

- `Variant = Primary`
- `ButtonType = Submit`
- `Enabled = !State.IsSubmitting`
- Label = `SubmitText ?? "Submit"`

Supply `FormButtons` to add Cancel buttons, secondary actions, or split buttons.

### Events

- `OnSubmitted` is raised once on a valid submission. Its `TModel` argument is the same
  reference the caller passed to `Model` — validation does not mutate the binding.
- `OnStateChanged` is raised on every state transition: entering submit, leaving submit
  (successful or failed), and invalid-submit attempts.

## State: `FormBlockState`

Source: `packages/blocks-forms/State/FormBlockState.cs`

A plain class holding submission state. Updated internally by `FormBlock`; exposed to
consumers via `OnStateChanged` and as a `State` property on the component instance.

| Property               | Type        | Notes |
|------------------------|-------------|-------|
| `IsSubmitting`         | `bool`      | `true` from the start of a valid-submit handler until `OnSubmitted` returns. |
| `HasSubmitted`         | `bool`      | `true` after the first submit attempt (valid or invalid). |
| `LastSubmitWasValid`   | `bool`      | `true` after a valid submission; `false` after an invalid one. |
| `LastSubmitAttemptUtc` | `DateTime?` | Wall-clock UTC of the most recent submit attempt. |

All setters are `internal` — consumers read the state but do not mutate it.

## Submit lifecycle

1. User submits the form.
2. Underlying `SunfishForm` validates.
3. **Valid submission**:
   - `State.IsSubmitting = true`, `State.LastSubmitAttemptUtc = now`; `OnStateChanged` fires.
   - `OnSubmitted.InvokeAsync(Model)` is awaited.
   - `State.LastSubmitWasValid = true`; `State.HasSubmitted = true`.
   - `State.IsSubmitting = false`; `OnStateChanged` fires again.
4. **Invalid submission**:
   - `State.LastSubmitAttemptUtc = now`; `State.LastSubmitWasValid = false`;
     `State.HasSubmitted = true`.
   - `OnStateChanged` fires; `OnSubmitted` is **not** invoked.

## Related pages

- [Overview](overview.md)
