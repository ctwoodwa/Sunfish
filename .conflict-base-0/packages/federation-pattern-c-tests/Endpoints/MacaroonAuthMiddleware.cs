using Microsoft.AspNetCore.Http;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Federation.PatternC.Tests.Endpoints;

/// <summary>
/// ASP.NET middleware for the Pattern C worked example — reads an
/// <c>Authorization: Macaroon &lt;base64url&gt;</c> header, decodes the macaroon,
/// verifies it against the registered root key and a <see cref="MacaroonContext"/> built
/// from request headers, and either calls the next middleware (on success) or short-circuits
/// with <c>401 Unauthorized</c> plus a diagnostic reason (on failure).
/// </summary>
/// <remarks>
/// <para>This is <b>test-only</b> infrastructure. A production portal would almost certainly
/// derive the <see cref="MacaroonContext"/> fields from its own authentication pipeline
/// (JWT, mTLS, session) rather than trusting client-supplied headers — here the test fixture
/// sets them explicitly to exercise the caveat matrix.</para>
/// <para>The middleware populates <c>HttpContext.Items["macaroon-valid"] = true</c> on
/// success so downstream endpoints can rely on a single signal rather than re-verifying.</para>
/// </remarks>
internal sealed class MacaroonAuthMiddleware
{
    private const string HeaderPrefix = "Macaroon ";
    private readonly RequestDelegate _next;
    private readonly IMacaroonVerifier _verifier;

    /// <summary>Constructs the middleware.</summary>
    public MacaroonAuthMiddleware(RequestDelegate next, IMacaroonVerifier verifier)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(verifier);
        _next = next;
        _verifier = verifier;
    }

    /// <summary>
    /// Invokes the middleware. On any failure (missing header, malformed wire form, signature
    /// mismatch, unknown root key, caveat rejection) responds <c>401</c> and a plaintext
    /// diagnostic — callers should not leak diagnostics to untrusted peers in production.
    /// </summary>
    public async Task InvokeAsync(HttpContext ctx)
    {
        var authHeader = ctx.Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith(HeaderPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await DenyAsync(ctx, "Missing Authorization: Macaroon header").ConfigureAwait(false);
            return;
        }

        var b64 = authHeader[HeaderPrefix.Length..].Trim();
        Macaroon macaroon;
        try
        {
            macaroon = MacaroonCodec.DecodeBase64Url(b64);
        }
        catch (FormatException ex)
        {
            await DenyAsync(ctx, $"Malformed macaroon: {ex.Message}").ConfigureAwait(false);
            return;
        }

        var macCtx = new MacaroonContext(
            Now: DateTimeOffset.UtcNow,
            SubjectUri: NullIfEmpty(ctx.Request.Headers["X-Subject-Uri"].ToString()),
            ResourceSchema: NullIfEmpty(ctx.Request.Headers["X-Resource-Schema"].ToString()),
            RequestedAction: NullIfEmpty(ctx.Request.Headers["X-Requested-Action"].ToString()),
            DeviceIp: ctx.Connection.RemoteIpAddress?.ToString());

        var result = await _verifier.VerifyAsync(macaroon, macCtx, ctx.RequestAborted)
            .ConfigureAwait(false);
        if (!result.IsValid)
        {
            await DenyAsync(ctx, $"Macaroon invalid: {result.Reason}").ConfigureAwait(false);
            return;
        }

        ctx.Items["macaroon-valid"] = true;
        ctx.Items["macaroon-location"] = macaroon.Location;
        ctx.Items["macaroon-identifier"] = macaroon.Identifier;
        await _next(ctx).ConfigureAwait(false);
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

    private static async Task DenyAsync(HttpContext ctx, string reason)
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        ctx.Response.ContentType = "text/plain; charset=utf-8";
        await ctx.Response.WriteAsync(reason, ctx.RequestAborted).ConfigureAwait(false);
    }
}
