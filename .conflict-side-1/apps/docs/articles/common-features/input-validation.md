---
uid: common-features-input-validation
title: Input Validation
description: How Sunfish input components integrate with Blazor's EditForm and validation infrastructure.
---

# Input Validation

Sunfish input components integrate with Blazor's standard validation pipeline. When you place Sunfish inputs inside an `EditForm`, they automatically respond to validation state — no extra wiring is required.

## Blazor Validation Basics

Blazor's built-in validation model revolves around three pieces:

- **`EditForm`** — wraps your model and manages an `EditContext`
- **`EditContext`** — tracks field modification and validation state for the model
- **`DataAnnotationsValidator`** — hooks into the `EditContext` to run `System.ComponentModel.DataAnnotations` rules

A minimal setup looks like this:

```razor
<EditForm Model="@person" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />
    <!-- inputs go here -->
    <button type="submit">Submit</button>
</EditForm>

@code {
    private PersonModel person = new();

    private void HandleSubmit()
    {
        // Called only when all validation passes
    }
}
```

## How Sunfish Inputs Respond to Validation State

Sunfish input components observe the nearest `EditContext` and apply CSS modifier classes when a field is invalid. When a field fails validation, the component automatically receives the `mar-invalid` CSS class on its root element and surfaces the invalid state visually — matching the active provider's error styling (red border in FluentUI, Bootstrap danger color, Material error state).

No additional code is needed. Place the input inside an `EditForm`, bind it with `@bind-Value`, and it handles state transitions automatically.

## SunfishForm

`SunfishForm` wraps `EditForm` and adds automatic layout generation from a model type. It renders a field row for each public property, using `SunfishField` and `SunfishLabel` internally.

```razor
<SunfishForm Model="@person" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />
    <SunfishValidationSummary />
    <button type="submit">Save</button>
</SunfishForm>

@code {
    private PersonModel person = new();
}
```

| Parameter | Type | Description |
|---|---|---|
| `Model` | `object` | The model instance to bind. |
| `OnValidSubmit` | `EventCallback` | Fires when the form passes validation. |
| `OnInvalidSubmit` | `EventCallback` | Fires when the form fails validation. |
| `OnSubmit` | `EventCallback<EditContext>` | Fires on every submit, bypassing built-in validation. |
| `ChildContent` | `RenderFragment` | Additional content rendered inside the form (validators, buttons, etc.). |

## SunfishField and SunfishLabel

`SunfishField` is a layout container for a single input and its associated label and validation message. It manages spacing, floating label support, and accessibility attributes.

`SunfishLabel` renders a `<label>` element with the correct `for` association and optional floating-label behavior.

```razor
<EditForm Model="@person" OnValidSubmit="HandleSubmit">
    <DataAnnotationsValidator />

    <SunfishField>
        <SunfishLabel For="@(() => person.Name)">Full Name</SunfishLabel>
        <SunfishTextBox @bind-Value="person.Name" />
        <SunfishValidationMessage For="@(() => person.Name)" />
    </SunfishField>

    <button type="submit">Save</button>
</EditForm>
```

**Floating label** — pass `Floating="true"` to `SunfishLabel` to activate a label that starts inside the input and animates upward on focus:

```razor
<SunfishField>
    <SunfishLabel For="@(() => person.Email)" Floating="true">Email</SunfishLabel>
    <SunfishTextBox @bind-Value="person.Email" />
    <SunfishValidationMessage For="@(() => person.Email)" />
</SunfishField>
```

## Validation Display Components

### SunfishValidationMessage

Renders the validation error message for a single field. Equivalent to Blazor's `ValidationMessage<T>` but styled with the active provider's error typography.

```razor
<SunfishValidationMessage For="@(() => model.Email)" />
```

### SunfishValidation

A lower-level component used internally by `SunfishField`. Prefer `SunfishValidationMessage` for explicit field-level messages.

### SunfishValidationSummary

Renders all validation errors for the current `EditContext` as a list. Place it at the top of the form to give users a combined overview of what needs to be corrected.

```razor
<SunfishValidationSummary />
```

## Complete Example

```razor
@page "/registration"

<EditForm Model="@model" OnValidSubmit="HandleSubmit" OnInvalidSubmit="HandleInvalid">
    <DataAnnotationsValidator />
    <SunfishValidationSummary />

    <SunfishField>
        <SunfishLabel For="@(() => model.Name)" Floating="true">Full Name</SunfishLabel>
        <SunfishTextBox @bind-Value="model.Name" />
        <SunfishValidationMessage For="@(() => model.Name)" />
    </SunfishField>

    <SunfishField>
        <SunfishLabel For="@(() => model.Email)" Floating="true">Email</SunfishLabel>
        <SunfishTextBox @bind-Value="model.Email" Type="email" />
        <SunfishValidationMessage For="@(() => model.Email)" />
    </SunfishField>

    <SunfishField>
        <SunfishLabel For="@(() => model.Age)" Floating="true">Age</SunfishLabel>
        <SunfishNumericTextBox @bind-Value="model.Age" />
        <SunfishValidationMessage For="@(() => model.Age)" />
    </SunfishField>

    <button type="submit">Register</button>
</EditForm>

@code {
    private RegistrationModel model = new();

    private void HandleSubmit()
    {
        // Validation passed — persist the model
    }

    private void HandleInvalid()
    {
        // Validation failed — form stays open
    }

    public class RegistrationModel
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Range(18, 120)]
        public int Age { get; set; }
    }
}
```

## Custom Validation — FluentValidation

Sunfish's inputs work with any custom validator that integrates with Blazor's `EditContext`. The community `Blazored.FluentValidation` package provides a drop-in `FluentValidationValidator` component that slots directly into an `EditForm`.

1. Install the package:

   ```
   dotnet add package Blazored.FluentValidation
   ```

2. Define a validator:

   ```csharp
   using FluentValidation;

   public class PersonValidator : AbstractValidator<PersonModel>
   {
       public PersonValidator()
       {
           RuleFor(p => p.Name).NotEmpty().MaximumLength(100);
           RuleFor(p => p.Email).NotEmpty().EmailAddress();
       }
   }
   ```

3. Use it in the form:

   ```razor
   @using Blazored.FluentValidation

   <EditForm Model="@person" OnValidSubmit="HandleSubmit">
       <FluentValidationValidator />
       <SunfishValidationSummary />

       <SunfishField>
           <SunfishLabel For="@(() => person.Name)">Name</SunfishLabel>
           <SunfishTextBox @bind-Value="person.Name" />
           <SunfishValidationMessage For="@(() => person.Name)" />
       </SunfishField>

       <button type="submit">Save</button>
   </EditForm>
   ```

Because Sunfish observes the standard `EditContext` validation API, it works with any validator that populates `ValidationMessageStore` correctly — including manual validators that call `editContext.NotifyValidationStateChanged()`.
