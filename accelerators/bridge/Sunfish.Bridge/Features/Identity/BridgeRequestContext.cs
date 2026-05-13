using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sunfish.Bridge.Client.Services;
using Sunfish.Bridge.Middleware;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Bridge.Features.Identity;

/// <summary>
/// Captures TenantId and ActorId from the initial HTTP connection (when
/// <see cref="IBrowserTenantContext"/> has been populated by middleware and
/// <see cref="IHttpContextAccessor"/> has a live <see cref="HttpContext"/>).
/// Values remain stable for the Blazor circuit lifetime even after
/// <see cref="IHttpContextAccessor.HttpContext"/> becomes null during SignalR rendering.
/// </summary>
public sealed class BridgeRequestContext : IBridgeRequestContext
{
    public bool IsResolved { get; }
    public TenantId TenantId { get; }
    public ActorId ActorId { get; }

    public BridgeRequestContext(IBrowserTenantContext tenantCtx, IHttpContextAccessor accessor)
    {
        if (!tenantCtx.IsResolved) return;

        var http = accessor.HttpContext;
        if (http is null) return;

        var sub = http.User.FindFirst("sub")?.Value
            ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (sub is null) return;

        TenantId = new TenantId(tenantCtx.TenantId.ToString());
        ActorId = new ActorId(sub);
        IsResolved = true;
    }
}
