#vs-intro
provides automated configuration commands for the Sunfish AI-powered development tools. These commands help you quickly set up the [Sunfish MCP server](slug:ai-overview) for enhanced developer productivity with Sunfish UI for Blazor components.
#end

#prerequisites
* Check the tool-specific prerequisites for the [Sunfish Blazor MCP Server](slug:agentic-ui-generator-getting-started).
#end

#verify-license-key
file to verify that the `SUNFISH_LICENSE_PATH` value matches your actual [Sunfish license file location](slug:installation-license-key). Alternatively, replace `SUNFISH_LICENSE_PATH` with `SUNFISH_LICENSE` and set your license key directly. Using `SUNFISH_LICENSE_PATH` is recommended.
#end

#command-github-app
command opens the [SunfishBlazor GitHub App installation page](https://github.com/apps/sunfishblazor/installations/select_target) in your default browser.
#end

#copilot-instructions
command generates a `copilot-instructions.md` file in the `.github` folder under the solution. This file contains custom instructions that help GitHub Copilot provide better assistance when working with Sunfish UI for Blazor components. The generated file includes the following default instructions:

* Guidance to use the Sunfish MCP Server whenever applicable
* Guidance to prioritize the usage of Sunfish UI components
* Guidance to use best coding practices related to Sunfish UI for Blazor
#end
