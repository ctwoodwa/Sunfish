---
title: Templates
page_title: AIPrompt - Views Templates
description: Explore the View Templates in the AIPrompt component for Blazor. Learn how to use them to customize the appearance of the individual views.
slug: aiprompt-views-templates
tags: sunfish,blazor,aiprompt,ai,prompt,templates
published: True
position: 40
components: ["aiprompt"]
---
# AIPrompt Views Templates

This article explains the available templates for the views of the AIPrompt for Blazor.

- [View Template](#view-template)
- [Footer Template](#footer-template)


## View Template

The `ViewTemplate` allows you to control the rendering of view's content. You can define it for each of the predefined views:

>caption Using the `ViewTemplate` to alter the appearance of the Prompt view

````RAZOR
<SunfishAIPrompt @ref="@AIPromptRef" @bind-Prompt="@Prompt">
    <AIPromptViews>
        <AIPromptPromptView ButtonIcon="@SvgIcon.Sparkles">
            <ViewTemplate>
                <SunfishTextBox @bind-Value="@Prompt" Placeholder="Type your prompt here..." />
            </ViewTemplate>
        </AIPromptPromptView>
        <AIPromptOutputView ButtonIcon="@SvgIcon.Comment">
        </AIPromptOutputView>
    </AIPromptViews>
</SunfishAIPrompt>

@code {
    private SunfishAIPrompt AIPromptRef { get; set; }

    private string Prompt { get; set; }
}
````

## Footer Template

The `FooterTemplate` allows you to control the rendering of the footer within individual views.

>caption Using the `FooterTemplate` to define a custom button.

````RAZOR
<SunfishAIPrompt @ref="@AIPromptRef" @bind-Prompt="@Prompt">
    <AIPromptViews>
        <AIPromptPromptView ButtonIcon="@SvgIcon.Sparkles">
            <FooterTemplate>
                <SunfishButton OnClick="@HandlePromptRequest">Generate</SunfishButton>
            </FooterTemplate>
        </AIPromptPromptView>
        <AIPromptOutputView ButtonIcon="@SvgIcon.Comment">
        </AIPromptOutputView>
    </AIPromptViews>
</SunfishAIPrompt>

@code {
    private SunfishAIPrompt AIPromptRef { get; set; }

    private string Prompt { get; set; }

    private void HandlePromptRequest()
    {
        // The example uses dummy data intentionally. Replace the hard-coded string with a call to your AI API.
        AIPromptRef.AddOutput(
            output: "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
            title: "Generated response",
            subtitle: string.Empty,
            prompt: Prompt,
            commandId: null,
            openOutputView: true);
    }
}
````

## See Also

  * [Live Demo: AIPrompt](https://demos.sunfish.dev/blazor-ui/aiprompt/overview)
  * [Views Overview](slug:aiprompt-views-overview)
  * [Prompt View](slug:aiprompt-views-prompt)
  * [Output View](slug:aiprompt-views-output)
  * [Commands View](slug:aiprompt-views-commands)