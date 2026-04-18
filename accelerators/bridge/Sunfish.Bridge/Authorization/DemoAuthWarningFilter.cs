using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Sunfish.Bridge.Authorization;

/// <summary>
/// Emits a startup warning when the web app boots in demo-auth mode.
/// Registered only when <c>IWebHostEnvironment.IsDevelopment()</c> is true
/// (same gate that registers DemoTenantContext).
/// </summary>
public sealed class DemoAuthWarningFilter : IStartupFilter
{
    private readonly ILogger<DemoAuthWarningFilter> _logger;

    public DemoAuthWarningFilter(ILogger<DemoAuthWarningFilter> logger) => _logger = logger;

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        _logger.LogWarning(
            "DEMO AUTH SEAM ACTIVE: Bridge is running with demo authentication wiring " +
            "(DemoTenantContext + MockOktaService). This is for local development only. " +
            "Replace with real ITenantContext + Okta/Entra/Auth0 configuration before production.");
        next(app);
    };
}
