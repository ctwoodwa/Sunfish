namespace Sunfish.Bridge.Proxy;

public sealed record ERPNextOptions
{
    public const string SectionName = "ERPNext";

    public string BaseUrl { get; init; } = "http://erp.localhost:8080";

    /// <summary>
    /// Frappe site name used as the <c>Host</c> header on every request.
    /// Required for multi-site Frappe routing; without it the nginx layer
    /// returns 404 even though the TCP connection succeeds.
    /// </summary>
    public string SiteName { get; init; } = "erp.localhost";

    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";

    /// <summary>
    /// Phase 1 fallback company. Replaced by the <c>company</c> auth claim
    /// once UserService + OIDC claim wiring lands (W#60 UserService task).
    /// </summary>
    public string DefaultCompany { get; init; } = "";

    public string AuthorizationHeader => $"token {ApiKey}:{ApiSecret}";
}
