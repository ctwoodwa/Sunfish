---
title: Summary
page_title: Validation Tools - Summary
description: Validation Tools - Summary.
slug: validation-tools-summary
tags: sunfish,blazor,validation,tools,summary
published: True
position: 5
components: ["validationmessage", "validationsummary", "validationtooltip"]
---
# Sunfish Validation Summary for Blazor

The [Sunfish Validation Summary for Blazor](https://sunfish.dev/blazor-ui/validationsummary) adds built-in styling and customization options on top of the standard [.NET ValidationSummary](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.components.forms.validationsummary), such as [`Template`](#template) and [`Class`](#class) parameters.

The Sunfish ValidationSummary component must be placed inside a Form. Refer to the following sections for additional information and examples with the [Sunfish Form](#using-with-sunfishform) and standard [Blazor `<EditForm>`](#using-with-editform).

## Using with SunfishForm

There are three ways to add a Sunfish ValidationSummary to a Sunfish Form:

* To display validation messages at the top of the Sunfish Form, add the `<SunfishValidationSummary>` tag inside the [`<FormValidation>` child tag of the `<SunfishForm>`](slug:form-validation).
* To display validation messages at the bottom of the Sunfish Form, add the `<SunfishValidationSummary>` tag inside the [`<FormButtons>` template](slug:form-formitems-buttons). Wrap the `<SunfishValidationSummary>` and all buttons in a single HTML element, otherwise the validation messages will shrink horizontally and display on the same line as the buttons.
* To display validation messages anywhere else in the Sunfish Form, add the `<SunfishValidationSummary>` tag inside a [`<FormItemsTemplate>` child tag of the `<SunfishForm>`](slug:form-formitems-formitemstemplate).

Optionally, [disable the built-in inline validation messages of the Sunfish Form](slug:form-validation#validation-message-type) to avoid repetition.

>caption Use Sunfish ValidationSummary at the top of a SunfishForm

````RAZOR
@using System.ComponentModel.DataAnnotations

<SunfishForm Model="@Employee"
             ValidationMessageType="@FormValidationMessageType.None"
             Width="300px">
    <FormValidation>
        <DataAnnotationsValidator />
        <SunfishValidationSummary />
    </FormValidation>
    <FormItems>
        <FormItem Field="@nameof(Person.FirstName)" LabelText="First Name" />
        <FormItem Field="@nameof(Person.LastName)" LabelText="Last Name" />
    </FormItems>
</SunfishForm>

@code {
    private Person Employee { get; set; } = new();

    public class Person
    {
        [Required(ErrorMessage = "Please enter a first name")]
        [MinLength(2, ErrorMessage = "The first name must be at least 2 characters long")]
        [MaxLength(40, ErrorMessage = "The first name must be up to 40 characters long")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
    }
}
````

>caption Use Sunfish ValidationSummary at the bottom of a SunfishForm

````RAZOR
@using System.ComponentModel.DataAnnotations

<SunfishForm Model="@Employee"
             ValidationMessageType="@FormValidationMessageType.None"
             Width="300px">
    <FormValidation>
        <DataAnnotationsValidator />
    </FormValidation>
    <FormItems>
        <FormItem Field="@nameof(Person.FirstName)" LabelText="First Name" />
        <FormItem Field="@nameof(Person.LastName)" LabelText="Last Name" />
    </FormItems>
    <FormButtons>
        <div>
            <SunfishValidationSummary />
            <SunfishButton ThemeColor="@ThemeConstants.Button.ThemeColor.Primary">Submit</SunfishButton>
        </div>
    </FormButtons>
</SunfishForm>

@code {
    private Person Employee { get; set; } = new();

    public class Person
    {
        [Required(ErrorMessage = "Please enter a first name")]
        [MinLength(2, ErrorMessage = "The first name must be at least 2 characters long")]
        [MaxLength(40, ErrorMessage = "The first name must be up to 40 characters long")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
    }
}
````

## Using with EditForm

In a standard Blazor `EditForm`, place a `<SunfishValidationSummary />` instead of a `<ValidationSummary />` anywhere inside the Form.

>caption Use Sunfish ValidationSummary in an EditForm

````RAZOR
@using System.ComponentModel.DataAnnotations

<EditForm Model="@Employee" style="width:300px">
    <DataAnnotationsValidator />

    <SunfishValidationSummary />

    <label for="first-name">First Name</label>
    <SunfishTextBox @bind-Value="@Employee.FirstName" Id="first-name" />

    <label for="last-name">Last Name</label>
    <SunfishTextBox @bind-Value="@Employee.LastName" Id="last-name" />

    <div>
        <SunfishButton>Submit</SunfishButton>
    </div>
</EditForm>

@code {
    private Person Employee { get; set; } = new();

    public class Person
    {
        [Required(ErrorMessage = "Please enter a first name")]
        [MinLength(2, ErrorMessage = "The first name must be at least 2 characters long")]
        [MaxLength(40, ErrorMessage = "The first name must be up to 40 characters long")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
    }
}
````

## Template

The Sunfish ValidationSummary allows you to customize its rendering with a nested `<Template>` tag. The template `context` is an `IEnumerable<string>` collection of all messages for the validated model.

>caption Using ValidationSummary Template

````RAZOR
@using System.ComponentModel.DataAnnotations

<SunfishForm Model="@Employee"
             ValidationMessageType="@FormValidationMessageType.None"
             Width="300px">
    <FormValidation>
        <DataAnnotationsValidator />
        <SunfishValidationSummary>
            <Template Context="validationMessages">
                @if (validationMessages.Any())
                {
                    <div class="k-validation-summary k-messagebox k-messagebox-error" role="alert">
                        <ul style="list-style-type: none; margin-bottom: 0; padding-left: .4em;">
                            @foreach (string message in validationMessages)
                            {
                                <li @key="@message" style="display: flex; gap: .4em; padding: .2em 0;">
                                    <SunfishSvgIcon Icon="@SvgIcon.ExclamationCircle" />
                                    @message
                                </li>
                            }
                        </ul>
                    </div>
                }
            </Template>
        </SunfishValidationSummary>

        <SunfishValidationSummary />
    </FormValidation>
    <FormItems>
        <FormItem Field="@nameof(Person.FirstName)" LabelText="First Name" />
        <FormItem Field="@nameof(Person.LastName)" LabelText="Last Name" />
    </FormItems>
</SunfishForm>

@code {
    private Person Employee { get; set; } = new();

    public class Person
    {
        [Required(ErrorMessage = "Please enter a first name")]
        [MinLength(2, ErrorMessage = "The first name must be at least 2 characters long")]
        [MaxLength(40, ErrorMessage = "The first name must be up to 40 characters long")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;
    }
}
````

## Class

Use the `Class` parameter of the Validation Summary to add a custom CSS class to the `div.k-validation-summary` container element. If you need to override the color styles, use [CSS specificity](slug:themes-override) that is higher than 2 CSS classes.

>caption Using SunfishValidationSummary Class

````RAZOR.skip-repl
<SunfishValidationSummary Class="bold-blue" />

<style>
    div.bold-blue.k-validation-summary {
        font-weight: bold;
        color: blue;
    }
</style>
````

## Next Steps

* Use [SunfishValidationMessage](slug:validation-tools-message)
* Try [SunfishValidationTooltip](slug:validation-tools-tooltip)

## See Also

* [Live Demo: Validation](https://demos.sunfish.dev/blazor-ui/validation/overview)
* [Form Component](slug:form-overview)
