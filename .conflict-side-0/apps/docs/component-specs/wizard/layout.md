---
title: Layout
page_title: Wizard Layout
description: Layout of the Wizard for Blazor.
slug: wizard-layout
tags: sunfish,blazor,wizard,layout
published: True
position: 10
components: ["wizard"]
---
# Wizard Layout

The Wizard component allows you to control its layout. By default, the internal stepper is rendered on top of the Wizard [content](slug:wizard-structure-content). You can customize its position through the `StepperPosition` the `SunfishWizard` exposes. It takes a member if the `WizardStepperPosition` enum:

* `Top` (the default)
* `Bottom`
* `Left`
* `Right`

>caption Customize the Wizard layout

````RAZOR
@* Change the position of the internal Stepper to Left *@

<div style="text-align:center">
    <SunfishWizard StepperPosition="WizardStepperPosition.Left" Width="600px" Height="300px">
        <WizardSteps>
            <WizardStep Text="1">
                <Content>
                    <div style="padding-top:80px">
                        <h2>Content for Wizard Step 1</h2>
                    </div>
                </Content>
            </WizardStep>
            <WizardStep Text="2">
                <Content>
                    <div style="padding-top:80px">
                        <h2>Content for Wizard Step 2</h2>
                    </div>
                </Content>
            </WizardStep>
            <WizardStep Text="3">
                <Content>
                    <div style="padding-top:80px">
                        <h2>Content for Wizard Step 3</h2>
                    </div>
                </Content>
            </WizardStep>           
        </WizardSteps>
    </SunfishWizard>
</div>
````

## See Also

* [Live Demos: Wizard Configuration](https://demos.sunfish.dev/blazor-ui/wizard/configuration)
