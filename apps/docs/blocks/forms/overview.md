---
uid: block-forms-overview
title: Forms — Overview
description: An opinionated single-page validated form block that wraps SunfishForm and SunfishValidation with sensible defaults and a lightweight submission-state model.
keywords:
  - sunfish
  - blocks
  - forms
  - formblock
  - sunfishform
  - validation
  - blazor
---

# Forms — Overview

## What this block is

`Sunfish.Blocks.Forms` is a thin, opinionated wrapper around `SunfishForm` +
`SunfishValidation`. It provides a one-page validated form with sensible defaults so that
consumers can drop in a small block rather than re-wiring the underlying form primitives
for every screen.

Out of the box the block gives you:

- Vertical, single-column layout by default (configurable `Columns` parameter).
- Inline validation messages.
- A default submit button (overridable via a `FormButtons` slot).
- A typed `FormBlockState` that tracks submit state between renders.
- A single event callback (`OnSubmitted`) that fires on valid submissions.

Deep features — multi-page forms, autosave, wizard flows, section navigation — are
intentionally deferred. Reach for `SunfishWizard` or compose `FormBlock`s manually when
you need those behaviours.

## Package

- Package: `Sunfish.Blocks.Forms`
- Source: `packages/blocks-forms/`
- Namespace roots: `Sunfish.Blocks.Forms.State`
- Razor components: `FormBlock.razor`

## When to use it

Use `FormBlock<TModel>` when:

- You have a single-screen form bound to a POCO model.
- You want validation on by default without writing the `SunfishValidation` wiring yourself.
- You want a submission state you can observe (e.g. disabling navigation while submitting).

Do not reach for it when:

- Your form is multi-page or step-wise — use `SunfishWizard`.
- You need deep custom layout — compose `SunfishForm` directly.

## Key pieces

- `FormBlock<TModel>` (Razor component) — the opinionated wrapper.
- `FormBlockState` — the submission-state record.

## Minimal example

```razor
@typeparam TModel where TModel : class

<FormBlock TModel="EditLeaseInput"
           Model="@_model"
           Columns="2"
           SubmitText="Save lease"
           OnSubmitted="SaveAsync">
    <FormItems>
        <FormItem Field="@nameof(EditLeaseInput.Rent)" />
        <FormItem Field="@nameof(EditLeaseInput.StartDate)" />
    </FormItems>
</FormBlock>

@code {
    private EditLeaseInput _model = new();
    private Task SaveAsync(EditLeaseInput input) => _service.SaveAsync(input);
}
```

## Customising the button row

The default submit button covers the common "Save" case. Supply the `FormButtons` slot to
render a Cancel-and-Save pair or any other layout:

```razor
<FormBlock TModel="EditLeaseInput" Model="@_model" OnSubmitted="SaveAsync">
    <FormItems>
        @* ... items ... *@
    </FormItems>
    <FormButtons>
        <SunfishButton Variant="ButtonVariant.Secondary"
                       OnClick="Cancel">
            Cancel
        </SunfishButton>
        <SunfishButton Variant="ButtonVariant.Primary"
                       ButtonType="ButtonType.Submit"
                       Enabled="@(!_formBlock.State.IsSubmitting)">
            Save
        </SunfishButton>
    </FormButtons>
</FormBlock>
```

When you supply `FormButtons`, the built-in submit button is not rendered — the slot
replaces it entirely. `SubmitText` is ignored in that case.

## Observing submission state

Attach `OnStateChanged` to react to transitions without sprinkling `_isSubmitting` flags
through the parent:

```razor
<FormBlock TModel="EditLeaseInput"
           Model="@_model"
           OnSubmitted="SaveAsync"
           OnStateChanged="HandleStateChanged">
    ...
</FormBlock>

@code {
    private string? _banner;

    private void HandleStateChanged(FormBlockState state)
    {
        _banner = state switch
        {
            { IsSubmitting: true }                          => "Saving…",
            { HasSubmitted: true, LastSubmitWasValid: true } => "Saved.",
            { HasSubmitted: true, LastSubmitWasValid: false } => "Please fix the highlighted errors.",
            _                                                => null,
        };
    }
}
```

## Typeparam ergonomics

Blazor renders `FormBlock<TModel>` as `<FormBlock TModel="...">`. The `TModel` constraint is
`class`, so value-type models are not supported (the `SunfishForm` primitive underneath
reserves value types for editor primitives). For scalar edits, skip the block and use
`SunfishForm` directly.

## Related pages

- [Service Contract](service-contract.md)
