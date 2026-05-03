namespace Sunfish.KitchenSink.Data;

public class SiteLinks
{
    public string DocsBaseUrl { get; set; } = "http://localhost:8081";
    public string DemoBaseUrl { get; set; } = "http://localhost:5301";

    // Docs site routes
    public string GettingStarted => $"{DocsBaseUrl}/articles/getting-started/overview.html";
    public string Theming => $"{DocsBaseUrl}/articles/theming/overview.html";
    public string ApiReference => $"{DocsBaseUrl}/api/Sunfish.Foundation.Base.html";
    public string ApiPage(string relativePath) => $"{DocsBaseUrl}{relativePath}";

    // Demo site routes
    public string Components => $"{DemoBaseUrl}/components";
}
