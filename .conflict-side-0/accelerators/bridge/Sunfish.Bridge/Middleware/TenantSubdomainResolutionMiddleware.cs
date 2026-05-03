using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Services;

namespace Sunfish.Bridge.Middleware;

/// <summary>
/// Configuration for <see cref="TenantSubdomainResolutionMiddleware"/>
/// (Wave 5.3.A, see <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.A).
/// Bound from the <c>Bridge:BrowserShell:TenantResolution</c> configuration
/// section.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RootHost"/> is the apex host the browser shell is reachable on
/// (e.g. <c>sunfish.example.com</c> in production, <c>localhost</c> in dev).
/// The middleware strips this suffix off the incoming <c>Host</c> header and
/// treats the remaining left-most DNS label as the tenant slug. An empty
/// <see cref="RootHost"/> opts out of suffix-stripping — every host is then
/// looked up verbatim — which is convenient for single-label dev hostnames
/// like <c>acme.localhost</c>.
/// </para>
/// <para>
/// <see cref="TrustForwardedHost"/> controls whether the
/// <c>X-Forwarded-Host</c> header takes precedence over <c>Host</c>. Disabled
/// by default; only enable when a reverse proxy strips and re-adds the
/// header, otherwise it is a trivial spoof vector.
/// </para>
/// </remarks>
public sealed record TenantResolutionOptions
{
    /// <summary>Apex host the browser shell answers on. Empty = no suffix strip.</summary>
    public string RootHost { get; init; } = string.Empty;

    /// <summary>When true, the middleware prefers <c>X-Forwarded-Host</c> over
    /// <c>Host</c>. Default false — the header is only set correctly by a
    /// trusted reverse proxy.</summary>
    public bool TrustForwardedHost { get; init; }
}

/// <summary>
/// Resolves the tenant for each browser-shell request by parsing the
/// subdomain off the incoming <c>Host</c> header, looking the slug up via
/// <see cref="ITenantRegistry"/>, and binding the result on the scoped
/// <see cref="IBrowserTenantContext"/> (Wave 5.3.A).
/// </summary>
/// <remarks>
/// <para>
/// Status policy (per decomposition doc):
/// <list type="bullet">
///   <item>missing / unknown slug → 404</item>
///   <item><see cref="TenantStatus.Pending"/> → 404 (the tenant exists but the
///     founder flow has not completed, so exposing its existence is pointless
///     and slightly information-leaky)</item>
///   <item><see cref="TenantStatus.Cancelled"/> → 410 Gone</item>
///   <item><see cref="TenantStatus.Suspended"/> → 503 with <c>Retry-After: 300</c></item>
///   <item><see cref="TenantStatus.Active"/> → bind context, continue pipeline</item>
/// </list>
/// </para>
/// <para>
/// Reserved slugs (<c>admin</c>, <c>www</c>, <c>auth</c>, <c>api</c>, empty)
/// short-circuit to 404 before touching the registry — they are operator-
/// owned hostnames that must never match a tenant row even if a tenant
/// squats them in the DB.
/// </para>
/// </remarks>
public sealed class TenantSubdomainResolutionMiddleware
{
    private static readonly HashSet<string> ReservedSlugs =
        new(StringComparer.OrdinalIgnoreCase) { "admin", "www", "auth", "api", string.Empty };

    private readonly RequestDelegate _next;

    public TenantSubdomainResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext ctx,
        ITenantRegistry registry,
        IBrowserTenantContext tenantContext,
        IOptions<TenantResolutionOptions> options)
    {
        var opts = options.Value;
        var host = ResolveHost(ctx, opts.TrustForwardedHost);
        var slug = ExtractSlug(host, opts.RootHost);

        if (slug is null || ReservedSlugs.Contains(slug))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var registration = await registry.GetBySlugAsync(slug, ctx.RequestAborted);
        if (registration is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        switch (registration.Status)
        {
            case TenantStatus.Pending:
                // Founder flow incomplete — treat as not-yet-existing for the
                // browser shell. /auth/* and /ws are equally off-limits.
                ctx.Response.StatusCode = StatusCodes.Status404NotFound;
                return;

            case TenantStatus.Cancelled:
                ctx.Response.StatusCode = StatusCodes.Status410Gone;
                return;

            case TenantStatus.Suspended:
                ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                ctx.Response.Headers.RetryAfter = "300";
                return;

            case TenantStatus.Active:
                break;
        }

        // AuthSalt is populated at CreateAsync time for new tenants and by
        // the Wave 5.3.A backfill for old rows; a still-null salt at this
        // point indicates a backfill gap, not normal operation. Fail 503
        // rather than crashing the request — operator can remediate without
        // cycling the app.
        if (registration.AuthSalt is null || registration.AuthSalt.Length == 0)
        {
            ctx.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ctx.Response.Headers.RetryAfter = "60";
            return;
        }

        tenantContext.Bind(
            registration.TenantId,
            registration.Slug,
            registration.TrustLevel,
            registration.TeamPublicKey,
            registration.AuthSalt);

        await _next(ctx);
    }

    private static string ResolveHost(HttpContext ctx, bool trustForwardedHost)
    {
        if (trustForwardedHost
            && ctx.Request.Headers.TryGetValue("X-Forwarded-Host", out var forwarded)
            && !string.IsNullOrWhiteSpace(forwarded))
        {
            // Take the first value if comma-separated; callers should never
            // send multiples but some CDNs do.
            var first = forwarded.ToString().Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first))
            {
                return first;
            }
        }

        return ctx.Request.Host.Host;
    }

    private static string? ExtractSlug(string host, string rootHost)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        // Normalise: lower-case, drop port (HostString never includes it here,
        // but X-Forwarded-Host might).
        var colon = host.IndexOf(':');
        if (colon >= 0)
        {
            host = host[..colon];
        }
        host = host.ToLowerInvariant();

        if (!string.IsNullOrEmpty(rootHost))
        {
            var root = rootHost.ToLowerInvariant();
            if (host.Equals(root, StringComparison.Ordinal))
            {
                // Apex request, no subdomain.
                return string.Empty;
            }
            var suffix = "." + root;
            if (host.EndsWith(suffix, StringComparison.Ordinal))
            {
                host = host[..^suffix.Length];
            }
            else
            {
                // Host doesn't belong to our root zone — reject.
                return null;
            }
        }

        // Left-most label is the slug.
        var dot = host.IndexOf('.');
        return dot >= 0 ? host[..dot] : host;
    }
}
