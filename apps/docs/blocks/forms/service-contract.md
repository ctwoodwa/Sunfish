---
uid: block-forms-service-contract
title: Forms — Service Contract
description: The FormBlock<TModel> component parameters, slots, events, and submission-state shape.
keywords:
  - sunfish
  - forms
  - formblock
  - formblockstate
  - blazor
  - validation
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

`OnSubmitted` is awaited in full before `IsSubmitting` is reset — this guarantees the submit
button stays disabled for the entire round-trip, including network I/O. If `OnSubmitted`
throws, the exception bubbles; `IsSubmitting` is still reset inside a `finally` block so
the UI does not get stuck in a submitting state.

## Error propagation from `OnSubmitted`

The block does not catch exceptions thrown by the `OnSubmitted` handler — they propagate to
the Blazor error boundary (or unhandled-exception pipeline) the host has configured.
`State.LastSubmitWasValid` is only set to `true` on a successful return, so an exception
leaves the last-valid flag untouched.

For expected failures (validation from a server-side check, for example), prefer returning
normally after attaching a validation message via `EditContext`, rather than throwing.

## Composition with `SunfishForm`

`FormBlock<TModel>` renders `SunfishForm` internally; the block doesn't attempt to hide it.
The underlying form still reads standard `[EditContext]` cascades and validation attributes
on `TModel`, so:

- `DataAnnotations` attributes on the model (`[Required]`, `[Range]`, …) are honoured.
- Additional `<SunfishValidation />` providers cannot be added on top — the block injects
  its own and there is no override. If you need a custom validator, compose `SunfishForm`
  directly instead of using the block.

## Test-surface guarantees

The first-pass smoke tests (`FormBlockTests.cs`) pin the public namespaces:

- `FormBlock<>` must stay in `Sunfish.Blocks.Forms`.
- `FormBlockState` must stay in `Sunfish.Blocks.Forms.State`.

Anything else is fair game in subsequent passes (subject to normal SemVer rules).

## Related pages

- [Overview](overview.md)
