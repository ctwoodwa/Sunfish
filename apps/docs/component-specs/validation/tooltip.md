---
title: Tooltip
page_title: Validation Tools - Tooltip
description: Validation Tools - Tooltip.
slug: validation-tools-tooltip
tags: sunfish,blazor,validation,tools,tooltip
published: True
position: 20
components: ["validationmessage", "validationsummary", "validationtooltip"]
---
# Sunfish Validation Tooltip for Blazor

The [Sunfish Validation Tooltip for Blazor](https://sunfish.dev/blazor-ui/validationtooltip) displays validation errors as tooltips that point to the problematic input component. The tooltips show on mouse over. Validaton tooltips serve the same purpose as inline [validation messages](slug:validation-tools-message), but as popups, they don't take up space on the page.

## Basics

To use a Sunfish Validation Tooltip:

1. Add the `<SunfishValidationTooltip>` tag.
1. Set the `TargetSelector` parameter to a CSS selector that points to the desired element in the Form.
1. (optional) Set the `Position` parameter to define on which side of the target the tooltip shows. Refer to the [Position article for the Sunfish Blazor Tooltip component](slug:tooltip-position) for details.
1. Set the `For` parameter in the same way as with a standard Blazor `<ValidationMessage>`. There are two options:
    * If the `SunfishValidationTooltip` is in the same component as the Form or if the Form model object is available, use a lambda expression that points to a property of the Form model.
        ````RAZOR.skip-repl
        <SunfishTextBox Id="first-name" />
        <SunfishValidationTooltip For="@(() => Customer.FirstName)"
                                  TargetSelector="#first-name" />

        @code {
            private CustomerModel Customer { get; set; } = new();

            public class CustomerModel
            {
                public string FirstName { get; set; } = string.Empty;
            }
        }
        ````
    * If the [validation message is in a child component](slug:inputs-kb-validate-child-component) that receives a `ValueExpression`, set the `For` parameter directly to the expression, without a lambda.
        ````RAZOR.skip-repl
        <SunfishTextBox Id="first-name" />
        <SunfishValidationTooltip For="@FirstNameExpression"
                                  TargetSelector="#first-name" />

        @code {
            [Parameter]
            public Expression<System.Func<string>>? FirstNameExpression { get; set; }
        }
        ````

Refer to the following sections for additional information and examples with the [Sunfish Form](#using-with-sunfishform) and standard [Blazor `<EditForm>`](#using-with-editform).

## Using with SunfishForm

The Sunfish Form can [display built-in validation tooltips](slug:form-validation#validation-message-type). You may need to define `<SunfishValidationTooltip>` components manually when you want to:

* Use [form item templates](slug:form-formitems-template). In this case, [add the validation tooltip in the form item template](slug:form-formitems-template#example).
* Customize the validation tooltips, for example, change their rendering with a [validation tooltip template](#template). In this case, add the validation tooltip inside a [Form item template](slug:form-formitems-template#example).

The following example sets a `Tooltip` `ValidationMessageType` on the Form. This is not required, but makes the validation user experience the same for `FirstName` and `LastName` properties, as the latter is not using an explicit `<SunfishValidationTooltip>`.

>caption Use Sunfish ValidationTooltip in a Sunfish Form

````RAZOR
@using System.ComponentModel.DataAnnotations

<SunfishForm Model="@Employee"
             Width="300px"
             ValidationMessageType="@FormValidationMessageType.Tooltip">
    <FormValidation>
        <DataAnnotationsValidator />
    </FormValidation>
    <FormItems>
        <FormItem Field="@nameof(Person.FirstName)" LabelText="First Name">
            <Template>
                <label for="first-name" class="k-label k-form-label">First Name</label>
                <div class="k-form-field-wrap">
                    <SunfishTextBox @bind-Value="@Employee.FirstName"
                                    Id="first-name" />
                    <SunfishValidationTooltip For="@(() => Employee.FirstName)"
                                              Position="@TooltipPosition.Right"
                                              TargetSelector="#first-name" />
                </div>
            </Template>
        </FormItem>
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

## Using with EditForm

In an existing Blazor `EditForm`, replace the `<ValidationMessage>` tags with `<SunfishValidationTooltip>` tags. The `For` parameter is set in the same way for both validation components.

>caption Use Sunfish Validation Tooltip in an EditForm

````RAZOR
@using System.ComponentModel.DataAnnotations

<EditForm Model="@Employee" style="width:300px">
    <DataAnnotationsValidator />

    <label for="first-name">First Name</label>
    <SunfishTextBox @bind-Value="@Employee.FirstName" Id="first-name" />
    <SunfishValidationTooltip For="@(() => Employee.FirstName)"
                              Position="@TooltipPosition.Right"
                              TargetSelector="#first-name" />


    <label for="last-name">Last Name</label>
    <SunfishTextBox @bind-Value="@Employee.LastName" Id="last-name" />
    <SunfishValidationTooltip For="@(() => Employee.LastName)"
                              Position="@TooltipPosition.Right"
                              TargetSelector="#last-name" />

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

## Position

The position of the ValidationTooltip is configured in the [same way as for the Sunfish Tooltip component for Blazor](slug:tooltip-position).

## Template

The Sunfish ValidationTooltip allows you to customize its rendering with a nested `<Template>` tag. The template `context` is an `IEnumerable<string>` collection of all messages for the validated model property.

>caption Using ValidationTooltip Template

````RAZOR
@using System.ComponentModel.DataAnnotations

<SunfishForm Model="@Employee"
             Width="300px"
             ValidationMessageType="@FormValidationMessageType.Tooltip">
    <FormValidation>
        <DataAnnotationsValidator />
    </FormValidation>
    <FormItems>
        <FormItem Field="@nameof(Person.FirstName)" LabelText="First Name">
            <Template>
                <label for="first-name" class="k-label k-form-label">First Name</label>
                <div class="k-form-field-wrap">
                    <SunfishTextBox @bind-Value="@Employee.FirstName"
                                    Id="first-name" />
                    <SunfishValidationTooltip For="@(() => Employee.FirstName)"
                                              Position="@TooltipPosition.Right"
                                              TargetSelector="#first-name">
                        <Template Context="validationMessages">
                            @foreach (string message in validationMessages)
                            {
                                <div>
                                    <span style="display:flex; gap: .4em;">
                                        <SunfishSvgIcon Icon="@SvgIcon.ExclamationCircle" />
                                        @message
                                    </span>
                                </div>
                            }
                        </Template>
                    </SunfishValidationTooltip>
                </div>
            </Template>
        </FormItem>
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

Use the `Class` parameter of the Validation Tooltip to add a custom CSS class to `div.k-animation-container`. This element wraps the `div.k-tooltip` and element and its child `div.k-tooltip`.

````RAZOR
<SunfishValidationTooltip Class="bold-red" />

<style>
    .bold-red .k-tooltip-content {
        font-weight: bold;
        color: var(--kendo-color-error);
    }
</style>
````

## See Also

* [Live Demo: Validation](https://demos.sunfish.dev/blazor-ui/validation/overview)
* [Validate Inputs in Child Components](slug:inputs-kb-validate-child-component)
* [Sunfish ValidationSummary](slug:validation-tools-summary)
* [Sunfish ValidationMessage](slug:validation-tools-message)
