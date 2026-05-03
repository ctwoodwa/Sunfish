#add-js-interop-file-to-getting-started-client
 Add the `sunfish-blazor.js` file to the `<head>` of your main index file - `wwwroot/index.html`:

    **HTML**
    
@[template](/_contentTemplates/common/js-interop-file.md#js-interop-file-snippet)

#end

#js-interop-file-snippet
````HTML
<head>
    . . .
    <script src="_content/Sunfish.UI.for.Blazor/js/sunfish-blazor.js"></script>
</head>
````
#end

#theme-static-asset-snippet
````HTML
<head>
    . . .
    <link rel="stylesheet" href="_content/Sunfish.UI.for.Blazor/css/kendo-theme-default/all.css" />
</head>
````
#end

#register-sunfish-service-server
<div class="skip-repl"></div>
````C#
// ...

var builder = WebApplication.CreateBuilder(args);

// ...

builder.Services.AddSunfishBlazor();

// ...

var app = builder.Build();
````
#end

#register-sunfish-service-client
<div class="skip-repl"></div>
````C#
using ClientBlazorProject;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// sample host builder for a WASM app, yours may differ
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register the Sunfish services.
builder.Services.AddSunfishBlazor();

await builder.Build().RunAsync();
````
#end