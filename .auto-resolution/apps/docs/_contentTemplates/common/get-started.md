#prerequisites-tip
>tip This step-by-step tutorial starts with the basics and is suitable for first-time Blazor or Sunfish component users. If you are already familiar with the Sunfish NuGet source, components, and Blazor in general, you may prefer the [Sunfish UI for Blazor Workflow Details](slug:getting-started/what-you-need) article. It provides more setup options and suggests possible enhancements.

#end

#prerequisites-download

* To successfully complete the steps in this tutorial, make sure you have an <a href="https://visualstudio.microsoft.com/vs/" target="_blank">up-to-date Visual Studio</a>, which is compatible with the [supported .NET version](slug:system-requirements#supported-net-versions) of your choice. If you are not using Visual Studio, some of the steps require using the .NET CLI or editing files manually. In this case, also refer to the [Workflow Details tutorial](slug:getting-started/what-you-need).

* To learn more about the compatibility of the Sunfish UI for Blazor components with different browser and .NET versions, see the [system requirements](slug:system-requirements).

* This online documentation covers the latest version of Sunfish UI for Blazor, which is `{{site.uiForBlazorLatestVersion}}`. If needed, [download the offline PDF documentation](slug:blazor-overview#learning-resources) for the required older product version.

## Step 0: Download Sunfish UI for Blazor

* If you have already purchased a commercial Sunfish UI for Blazor license, continue with the [next step and install a license key](#step-1-install-a-license-key).

* If you are new to UI for Blazor and haven’t purchased a license yet, you can <a href="https://sunfish.dev/try/ui-for-blazor" target="_blank">Start a Free Trial</a> by downloading and installing the UI for Blazor components. This process activates your free trial and enables you to use the components. During the installation, select the **Set up Sunfish NuGet package source** checkbox so the installer can automatically configure the Sunfish [online NuGet feed](slug:installation/nuget), which you will use later in the tutorial. 

* Just starting a free trial on sunfish.dev is not enough. Trial users must also complete the local installation of the components. Otherwise, the trial license does not activate and the tutorial cannot be completed successfully.

#end

#generate-nuget-api-key

As the Sunfish NuGet server requires authentication, the first step is to obtain an API key that you will use instead of a password. Using an API key instead of a password is a more secure approach, especially when working with the [.NET CLI](#use-the-net-cli) or a [`NuGet.Config` file](#edit-the-nugetconfig-file).

1. Go to the [API Keys](https://sunfish.dev/account/downloads/api-keys) page in your Sunfish account.
1. Click **Generate New Key +**.
1. In the **Key Note** field, add a note that describes the API key.
1. Click **Generate Key**.
1. Select **Copy and Close**. Once you close the window, you can no longer copy the generated key. For security reasons, the **API Keys** page displays only a portion of the key.
1. Store the generated NuGet API key as you will need it in the next steps.

Whenever you need to authenticate your system with the Sunfish NuGet server, use `api-key` as the username and your generated API key as the password.

> Sunfish API keys expire in two years. Make sure to generate and use a new one in time.

#end

#nuget-cli-add-command

````SHELL PowerShell
dotnet nuget add source "https://nuget.sunfish.dev/v3/index.json" `
  --name "SunfishOnlineFeed" `
  --username "api-key" `
  --password "<YOUR-NUGET-API-KEY>" `
  --store-password-in-clear-text
````
````SHELL Bash
dotnet nuget add source "https://nuget.sunfish.dev/v3/index.json" \
  --name "SunfishOnlineFeed" \
  --username "api-key" \
  --password "<YOUR-NUGET-API-KEY>" \
  --store-password-in-clear-text
````
````SHELL Zsh
dotnet nuget add source "https://nuget.sunfish.dev/v3/index.json" \
  --name "SunfishOnlineFeed" \
  --username "api-key" \
  --password "<YOUR-NUGET-API-KEY>" \
  --store-password-in-clear-text
````

#end

#add-nuget-feed
## Step 3: Add the Sunfish NuGet Feed

In this tutorial, you will use the [Sunfish NuGet server](slug:installation/nuget) to download the UI for Blazor components. The NuGet feed is private and requires you to authenticate with a NuGet API key.

### Generate NuGet API Key

1. Go to the [API Keys](https://sunfish.dev/account/downloads/api-keys) page in your Sunfish account.
1. Click **Generate New Key +**.
1. In the **Key Note** field, add a note that describes the API key.
1. Click **Generate Key**.
1. Select **Copy and Close**. Once you close the window, you can no longer copy the generated key. For security reasons, the **API Keys** page displays only a portion of the key.
1. Store the generated NuGet API key as you will need it in the next steps.

Next, add the Sunfish NuGet feed to your local development environment:

* [Visual Studio on Windows](#visual-studio)
* [All IDEs and operating systems](#all-ides-and-operating-systems)

> Sunfish API keys expire in two years. Make sure to generate and use a new one in time. For more information on the Sunfish NuGet packages and download options, check [the NuGet Packages section in the Workflow article](slug:getting-started/what-you-need#nuget-packages).

### Visual Studio

The following approach will store the Sunfish NuGet server URL in your [global `NuGet.Config` file](https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior), and save your NuGet API key in the Windows Credential Manager.

1. In Visual Studio, go to **Tools** > **NuGet Package Manager** > **Package Manager Settings**.
1. Select **Package Sources** and then click the **+** or **Add** button.
1. Enter a **Name** for the new package source. The examples in this documentation usually use `SunfishOnlineFeed`.
1. Add `https://nuget.sunfish.dev/v3/index.json` as a **Source** URL. Click **OK** or **Save**.
1. Whenever Visual Studio displays a dialog to enter credentials for `nuget.sunfish.dev`, use `api-key` as the username and your NuGet API key as the password.

### All IDEs and Operating Systems

Run [`dotnet nuget add source`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-add-source) in your preferred command line interface (cmd, Terminal, PowerShell, Bash). The command will store the Sunfish NuGet server URL and your NuGet API key in your [global `NuGet.Config` file](https://learn.microsoft.com/en-us/nuget/consume-packages/configuring-nuget-behavior).

Replace `<YOUR-NUGET-API-KEY>` with the API key that you [generated previously](#generate-nuget-api-key).

The linebreak characters used below enable multi-line commands for better readability. If they don't work in your terminal, combine the parameters into a single line instead.

>caption Use the .NET CLI to add the Sunfish NuGet source

@[template](/_contentTemplates/common/get-started.md#nuget-cli-add-command)

#end


#add-component-sample

   ````RAZOR.skip-repl
   <SunfishButton>Say Hello</SunfishButton>
   ````

1. Optionally, hook up a click handler that will show a message. The resulting view will look like this:

   ````RAZOR.skip-repl
   @page "/"
           
   <SunfishButton ThemeColor="@ThemeConstants.Button.ThemeColor.Primary"
                  OnClick="@SayHelloHandler">Say Hello</SunfishButton>
   
   <p>@HelloString</p>
   
   @code {
       private MarkupString HelloString { get; set; }
   
       private void SayHelloHandler()
       {
           string msg = $"Hello from <strong>Sunfish UI for Blazor</strong> at {DateTime.Now.ToString("HH:mm:ss")}!" +
               "<br /> Now you can use C# to write front-end!";
   
           HelloString = new MarkupString(msg);
       }
   }
   ````

1. Run the app in the browser. You should see something like this:

![Sunfish Blazor app in the browser](images/blazor-app-in-browser.png)

Well done! Now you have your first Sunfish UI for Blazor component running in your Blazor app, showcasing the power of front-end development with Blazor.

#end

#next-steps-after-getting-started
## Next Steps

* [Check the list of available components](slug:blazor-overview#list-of-components).
* [Explore the live Sunfish UI for Blazor demos](https://demos.sunfish.dev/blazor-ui).
* [Learn the data binding fundamentals for Sunfish UI for Blazor components](slug:common-features-data-binding-overview).
* [Get started with the data Grid](slug:grid-overview).
* [Review the built-in themes or create custom ones](slug:themes-overview).

#end

#demos-project-net-version
 The project targets the latest official .NET version, and its readme file provides more details on running the project and using older versions of the framework.
#end


#after-install
Once you have the Sunfish NuGet source set up, follow the instructions to [create a Sunfish Blazor app](slug:blazor-overview#getting-started).
#end

#setup-local-feed-vs
## Set Up a Local NuGet Feed in Visual Studio

To setup a local NuGet package source, so you can install the Sunfish components without an active Internet connection and without setting up our private feed, do the following:

1. Copy all the `.nupkg` files we provide from the **`packages`** and **`dpl`** folders of your Sunfish UI for Blazor installation to your preferred local feed location. By default, the installation path is `C:\Program Files (x86)\Progress\Sunfish UI for Blazor <VERSION>` or where you unzip the ZIP installer.

1. Open **Visual Studio** and go to **Tools** > **Options**.

1. Find the **NuGet Package Manager** node, expand it, and select **Package Sources**.

1. Click the Add (`+`) icon at the top to add the new local feed, select its name and point it to the path where you placed all the Sunfish `.nupkg` files.

    >tip Make sure to add the packages from both the `packages` and `dpl` folders to your custom feed. You can also point the package source to the Sunfish installation folder to include all packages recursively.

    For example:

    ![Blazor Create Local Nuget Feed](images/create-local-nuget-feed.png)
#end


#navigate-account
1. Go to [Downloads](https://sunfish.dev/account/downloads) in your [Sunfish account](https://sunfish.dev/account/).

1. On the loaded page click on **Progress® Sunfish® UI for Blazor**.
#end


#root-component-main-layout
Add a `<SunfishRootComponent>` to the app layout file (by default, `MainLayout.razor`). Make sure that the `SunfishRootComponent` wraps all the content in the `MainLayout`.

>caption MainLayout.razor

<div class="skip-repl"></div>

````RAZOR
@inherits LayoutComponentBase

<SunfishRootComponent>
    @* existing MainLayout.razor content here *@
</SunfishRootComponent>
````

You can learn more about the [`SunfishRootComponent` purpose and usage](slug:rootcomponent-overview) in its dedicated documentation.
#end


#start-trial-button
<div class="justify-content-center text-center try-button">
    <a class="button" href="https://sunfish.dev/download-trial-file/v2/ui-for-blazor" target="_blank">Start a free trial</a>
</div>

<style>
.try-button {
    margin-top: 3rem;
    margin-bottom: 3rem;
}
.try-button .button {
    display: inline-block;
    font-size: 18px;
    color: #ffffff;
    background-color: #ff6358;
    border-radius: 2px;
    transition: color .2s ease,background-color .2s ease;
    text-decoration: none;
    padding: 10px 30px 10px 30px;
    line-height: 1.5em;
    height: auto;
}

.try-button .button:hover {
    color: #ffffff;
    background-color: #e74b3c;
}
</style>
#end

#license-key-version

>tip This documentation section applies to Sunfish UI for Blazor version **8.0.0** and above. Older versions do not require a license key.

#end

#license-key-update-whenever

>tip Update your license key [whenever you renew or purchase a new Sunfish license](slug:installation-license-key#license-key-updates).

#end

#license-key-manual-steps

To download and install your Sunfish license key:

1. Go to the <a href="https://sunfish.dev/account/your-licenses/license-keys" target="_blank">License Keys page</a> in your Sunfish account.
1. Click the **Download License Key** button.
1. Save the `sunfish-license.txt` file to:
    * (on Windows) `%AppData%\Sunfish\sunfish-license.txt`, for example, `C:\Users\...\AppData\Roaming\Sunfish\sunfish-license.txt`
    * (on Mac or Linux) `~/.sunfish/sunfish-license.txt`, for example, `/Users/.../.sunfish/sunfish-license.txt`

This will make the license key available to all Sunfish .NET apps that you develop on your local machine.

#end

#license-key-know-more-link

The [Sunfish License Key](slug:installation-license-key) article provides additional details on installing and updating your Sunfish license key in different scenarios. [Automatic license key maintenance](slug:installation-license-key#automatic-installation) is more effective and recommended in the long run.

#end

#ai-coding-assistant-ad

Sunfish UI for Blazor provides AI-powered development assistance through a unified [MCP (Model Context Protocol) server](slug:ai-overview) that delivers intelligent, context-aware help directly in your IDE. The MCP server automatically recognizes your Sunfish license and activates the available tools:

* [Agentic UI Generator](slug:agentic-ui-generator-getting-started)&mdash;Build complete, production-ready UIs using natural language prompts. Describe your desired page, layout, or component configuration, and the AI-powered generator will create responsive, styled Blazor code with proper Sunfish UI for Blazor component integration.

This unified MCP server integrates seamlessly with your IDE to provide contextual help and automate repetitive tasks, making it easier to explore the library and build feature-rich applications faster. Give the AI tools a try as you follow this guide or build your next project!

#end