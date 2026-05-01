using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Bridge;

/// <summary>
/// Default <see cref="IDataResidencyEnforcerMiddleware"/> per ADR 0064-A1.4.
/// Resolves the residency context from request headers (
/// <c>X-Sunfish-Record-Class</c> + <c>X-Sunfish-Jurisdiction</c>) by default;
/// hosts override <see cref="ResolveContextAsync"/> for richer wiring.
/// </summary>
/// <remarks>
/// <para>
/// HTTP 451 RFC 7725 response shape: status 451, body is a short
/// human-readable string referencing the violated constraint. The
/// <c>Retry-After</c> header is intentionally omitted per A1.4 — Phase 1
/// substrate's Retry-After semantic is "never" by default; hosts that
/// know a time-bounded restriction can override the middleware to set
/// the header.
/// </para>
/// </remarks>
public class DataResidencyEnforcerMiddleware : IDataResidencyEnforcerMiddleware
{
    /// <summary>Header carrying the record class. Hosts MAY override <see cref="ResolveContextAsync"/> to use a different resolution strategy.</summary>
    public const string RecordClassHeader = "X-Sunfish-Record-Class";

    /// <summary>Header carrying the jurisdiction code.</summary>
    public const string JurisdictionHeader = "X-Sunfish-Jurisdiction";

    private readonly IDataResidencyEnforcer _enforcer;

    public DataResidencyEnforcerMiddleware(IDataResidencyEnforcer enforcer)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        _enforcer = enforcer;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var resolved = await ResolveContextAsync(context).ConfigureAwait(false);
        if (resolved is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var verdict = await _enforcer.EnforceAsync(
            resolved.RecordClass,
            resolved.JurisdictionCode,
            context.RequestAborted).ConfigureAwait(false);

        if (verdict.IsPermitted)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        // HTTP 451 — Unavailable for Legal Reasons (RFC 7725).
        context.Response.StatusCode = StatusCodes.Status451UnavailableForLegalReasons;
        context.Response.ContentType = "text/plain; charset=utf-8";
        var body = verdict.Detail ?? $"Record-class '{resolved.RecordClass}' is not permitted in jurisdiction '{resolved.JurisdictionCode}'.";
        await context.Response.WriteAsync(body, context.RequestAborted).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual ValueTask<DataResidencyContext?> ResolveContextAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Request.Headers.TryGetValue(RecordClassHeader, out var recordClass)
            || !context.Request.Headers.TryGetValue(JurisdictionHeader, out var jurisdiction)
            || string.IsNullOrEmpty(recordClass.ToString())
            || string.IsNullOrEmpty(jurisdiction.ToString()))
        {
            return ValueTask.FromResult<DataResidencyContext?>(null);
        }

        return ValueTask.FromResult<DataResidencyContext?>(new DataResidencyContext
        {
            RecordClass = recordClass.ToString(),
            JurisdictionCode = jurisdiction.ToString(),
        });
    }
}
