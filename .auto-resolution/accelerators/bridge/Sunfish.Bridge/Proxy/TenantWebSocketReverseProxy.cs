using System.Buffers;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Sunfish.Bridge.Middleware;
using Sunfish.Bridge.Orchestration;

namespace Sunfish.Bridge.Proxy;

/// <summary>
/// Wave 5.3.C reverse proxy that bridges a browser-shell WebSocket to the
/// tenant child's <c>/ws</c> endpoint. Per
/// <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.C the browser
/// speaks CBOR-framed sync-daemon traffic over the apex-hosted shell's
/// <c>/ws</c>, and Bridge transparently tunnels those frames to the
/// <c>local-node-host</c> process it spawned for that tenant (Wave 5.2.C)
/// whose endpoint is registered in <see cref="ITenantEndpointRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Auth.</b> Wave 5.3.B lands the browser-session cookie + passphrase
/// handshake. Until then, <see cref="InvokeAsync"/> does NOT call
/// <see cref="AuthorizationAppBuilderExtensions.UseAuthorization"/>-gated
/// endpoints — the reverse-proxy simply requires a resolved
/// <see cref="IBrowserTenantContext"/> (populated by the Wave 5.3.A subdomain
/// middleware). TODO(5.3.B): wrap the mapping in
/// <c>.RequireAuthorization("browser-shell")</c> once the policy lands.
/// </para>
/// <para>
/// <b>Byte-pump.</b> Two <see cref="Task.Run(Func{Task})"/> loops copy frames
/// in each direction. Using the bytes API (not the message-record API) keeps
/// the proxy protocol-agnostic — the CBOR envelope is opaque to Bridge.
/// </para>
/// <para>
/// <b>Close propagation.</b> When either side closes (clean close,
/// <see cref="WebSocketCloseStatus"/> reply, or abrupt abort), the proxy
/// mirrors the status + description onto the other side and disposes both.
/// </para>
/// </remarks>
public static class TenantWebSocketReverseProxy
{
    /// <summary>
    /// Map the Wave 5.3.C <c>/ws</c> route. Call from the SaaS-posture
    /// <see cref="Microsoft.AspNetCore.Builder.WebApplication"/>'s
    /// <c>MapGet</c>-style pipeline AFTER the subdomain-resolution
    /// middleware has run.
    /// </summary>
    public static void MapTenantWebSocketProxy(
        this Microsoft.AspNetCore.Routing.IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        // Map GET /ws — the WebSocket upgrade is negotiated via headers on the
        // initial HTTP GET request, so Map + MapGet are equivalent here.
        endpoints.Map("/ws", InvokeAsync);
        // TODO(5.3.B): chain .RequireAuthorization("browser-shell") once the
        // browser-session policy ships.
    }

    /// <summary>
    /// Entry point for the reverse proxy. Public so unit tests can exercise
    /// it with a synthetic <see cref="HttpContext"/> and synthetic tenant
    /// endpoint.
    /// </summary>
    public static async Task InvokeAsync(HttpContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var logger = ctx.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Sunfish.Bridge.Proxy.TenantWebSocketReverseProxy");

        // Pipeline-ordering sanity: the Wave 5.3.A subdomain middleware must
        // have bound IBrowserTenantContext before this handler runs. Missing
        // registration is a composition error, not a client error, so 500
        // rather than 400.
        var tenantContext = ctx.RequestServices.GetService<IBrowserTenantContext>();
        if (tenantContext is null)
        {
            logger.LogError(
                "TenantWebSocketReverseProxy: IBrowserTenantContext is not registered. " +
                "Middleware order: ensure UseMiddleware<TenantSubdomainResolutionMiddleware> " +
                "runs before MapTenantWebSocketProxy.");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }
        if (!tenantContext.IsResolved)
        {
            logger.LogError(
                "TenantWebSocketReverseProxy: IBrowserTenantContext is not resolved. " +
                "The Wave 5.3.A subdomain middleware must run before this endpoint.");
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        var registry = ctx.RequestServices.GetRequiredService<ITenantEndpointRegistry>();
        if (!registry.TryGet(tenantContext.TenantId, out var endpoint) || endpoint is null)
        {
            // Tenant is unknown to the registry — either never spawned, paused,
            // or cancelled. Wave 5.3.A's tenant resolution would have already
            // 404'd if the DB row was missing; this branch fires for a real
            // tenant that doesn't currently have a running child.
            logger.LogInformation(
                "TenantWebSocketReverseProxy: no endpoint registered for tenant {TenantId} " +
                "(tenant child not running?).",
                tenantContext.TenantId);
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Translate http(s)://…/ → ws(s)://…/ws. The registry stores the
        // health-endpoint URI the supervisor recorded on spawn; the tenant
        // child's Kestrel hosts /health and /ws on the same listener (Wave
        // 5.3.C SharedHostedWebApp).
        var tenantWsUri = BuildTenantWsUri(endpoint);

        // Factory so tests can inject a fake upstream ClientWebSocket. Default
        // factory returns a fresh System.Net.WebSockets.ClientWebSocket.
        var factory = ctx.RequestServices.GetService<IUpstreamWebSocketFactory>()
            ?? DefaultUpstreamWebSocketFactory.Instance;

        WebSocket browser;
        WebSocket tenant;
        try
        {
            tenant = await factory.ConnectAsync(tenantWsUri, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "TenantWebSocketReverseProxy: failed to connect upstream to {TenantWsUri}.",
                tenantWsUri);
            ctx.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        try
        {
            browser = await ctx.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            tenant.Dispose();
            throw;
        }

        await PumpAsync(browser, tenant, logger, ctx.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>
    /// Run the bidirectional byte-pump until either side closes. Visible
    /// internally so unit tests can exercise the pump against a pair of
    /// loopback WebSockets without the HTTP plumbing.
    /// </summary>
    /// <remarks>
    /// When either direction finishes (a <see cref="WebSocketMessageType.Close"/>
    /// frame arrives, or the socket is otherwise torn down), the opposite
    /// direction is cancelled via a linked token so the whole pump shuts down
    /// deterministically rather than waiting on the far side's pending
    /// <see cref="WebSocket.ReceiveAsync(ArraySegment{byte}, CancellationToken)"/>.
    /// </remarks>
    internal static async Task PumpAsync(
        WebSocket browser,
        WebSocket tenant,
        ILogger logger,
        CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            var b2t = Task.Run(() => CopyAsync(browser, tenant, "browser→tenant", logger, linked.Token), linked.Token);
            var t2b = Task.Run(() => CopyAsync(tenant, browser, "tenant→browser", logger, linked.Token), linked.Token);
            // Wait for one direction to finish, then cancel the other so
            // Task.WhenAll completes promptly.
            var first = await Task.WhenAny(b2t, t2b).ConfigureAwait(false);
            linked.Cancel();
            try { await Task.WhenAll(b2t, t2b).ConfigureAwait(false); }
            catch (OperationCanceledException) { /* expected on the second direction */ }
        }
        catch (OperationCanceledException)
        {
            // Request aborted — fine.
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "TenantWebSocketReverseProxy pump failed.");
        }
        finally
        {
            try { browser.Dispose(); } catch { /* best-effort */ }
            try { tenant.Dispose(); } catch { /* best-effort */ }
        }
    }

    /// <summary>
    /// One-directional byte-pump: read frames from <paramref name="src"/>,
    /// write them to <paramref name="dst"/>, preserving binary/text/close
    /// framing + the end-of-message bit. Returns when <paramref name="src"/>
    /// closes or the cancellation token fires.
    /// </summary>
    private static async Task CopyAsync(
        WebSocket src,
        WebSocket dst,
        string direction,
        ILogger logger,
        CancellationToken ct)
    {
        var pool = ArrayPool<byte>.Shared;
        var buffer = pool.Rent(16 * 1024);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await src.ReceiveAsync(new ArraySegment<byte>(buffer), ct)
                        .ConfigureAwait(false);
                }
                catch (WebSocketException wex)
                {
                    logger.LogDebug(wex, "{Direction} receive threw.", direction);
                    await TryCloseOutputAsync(dst, WebSocketCloseStatus.EndpointUnavailable,
                        "peer receive failed", ct).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Mirror the close onto the other side (propagate the
                    // termination intent to the far peer) AND complete the
                    // close handshake on this side so the initiator's
                    // bidirectional CloseAsync() unblocks. Both calls use
                    // CloseOutputAsync — one-way — to avoid the proxy itself
                    // blocking on further reads from either side.
                    var status = result.CloseStatus ?? WebSocketCloseStatus.NormalClosure;
                    var desc = result.CloseStatusDescription ?? string.Empty;
                    await TryCloseOutputAsync(dst, status, desc, ct).ConfigureAwait(false);
                    await TryCloseOutputAsync(src, status, desc, ct).ConfigureAwait(false);
                    return;
                }

                try
                {
                    await dst.SendAsync(
                        new ArraySegment<byte>(buffer, 0, result.Count),
                        result.MessageType,
                        result.EndOfMessage,
                        ct).ConfigureAwait(false);
                }
                catch (WebSocketException wex)
                {
                    logger.LogDebug(wex, "{Direction} send threw.", direction);
                    return;
                }
            }
        }
        finally
        {
            pool.Return(buffer);
        }
    }

    private static async Task TryCloseOutputAsync(
        WebSocket ws,
        WebSocketCloseStatus status,
        string description,
        CancellationToken ct)
    {
        if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseOutputAsync(status, description, ct).ConfigureAwait(false);
            }
            catch
            {
                // Socket already closing / closed — ignore.
            }
        }
    }

    private static Uri BuildTenantWsUri(Uri endpoint)
    {
        var builder = new UriBuilder(endpoint)
        {
            Scheme = endpoint.Scheme switch
            {
                "https" => "wss",
                "http" => "ws",
                _ => endpoint.Scheme,
            },
            Path = "/ws",
        };
        return builder.Uri;
    }
}

/// <summary>
/// Strategy for opening the upstream WebSocket to the tenant's
/// <c>local-node-host</c>. Tests substitute a fake; production uses
/// <see cref="DefaultUpstreamWebSocketFactory"/>.
/// </summary>
public interface IUpstreamWebSocketFactory
{
    Task<WebSocket> ConnectAsync(Uri uri, CancellationToken ct);
}

/// <summary>
/// Default <see cref="IUpstreamWebSocketFactory"/> — opens a fresh
/// <see cref="ClientWebSocket"/> against the tenant child's <c>/ws</c>.
/// </summary>
public sealed class DefaultUpstreamWebSocketFactory : IUpstreamWebSocketFactory
{
    public static DefaultUpstreamWebSocketFactory Instance { get; } = new();

    public async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken ct)
    {
        var client = new ClientWebSocket();
        try
        {
            await client.ConnectAsync(uri, ct).ConfigureAwait(false);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
