---
title: Validation
page_title: FloatingLabel Validation
description: The FloatingLabel can change its styling when it is inside a validated form and the associated form field is invalid. How to integrate the Blazor floating label with form validation.
slug: floatinglabel-validation
tags: sunfish,blazor,floatinglabel,floating,label,form,validation
published: True
position: 10
components: ["floatinglabel"]
---
# Floating Label Validation

The Blazor FloatingLabel integrates with form validation of [compatible Sunfish components](slug:floatinglabel-overview#compatibility). When a form field is invalid, the floating label will change color to suggest user action.

>caption Floating Label Integration with Forms and Validation

````RAZOR
@using System.ComponentModel.DataAnnotations

@if (ValidSubmit)
{
    <p>The form was submitted successfully.</p>
}
else
{
    <EditForm Model="@TestUser"
              OnValidSubmit="@HandleValidSubmit">
        <DataAnnotationsValidator />
        <SunfishValidationSummary />

        <div>
            <SunfishFloatingLabel Text="Username *">
                <SunfishTextBox @bind-Value="@TestUser.Username" />
            </SunfishFloatingLabel>
        </div>

        <div>
            <SunfishFloatingLabel Text="Password *">
                <SunfishTextBox @bind-Value="@TestUser.Password" Password="true" />
            </SunfishFloatingLabel>
        </div>

        <p>
            <SunfishButton ButtonType="@ButtonType.Submit">Submit</SunfishButton>
        </p>
    </EditForm>
}

@code {
    User TestUser { get; set; } = new User();

    bool ValidSubmit { get; set; }

    async void HandleValidSubmit()
    {
        ValidSubmit = true;

        await Task.Delay(2000);

        ValidSubmit = false;
        TestUser = new User();

        StateHasChanged();
    }

    public class User
    {
        [Display(Name = "Username")]
        [Required(ErrorMessage = "Username is required")]
        public string Username { get; set; }

        [Display(Name = "Password")]
        [MinLength(8, ErrorMessage = "Password should be at least 8 characters")]
        [Required(ErrorMessage = "Password is required")]
        public string Password { get; set; }
    }
}
````


## See Also

* [FloatingLabel Overview](slug:floatinglabel-overview)
