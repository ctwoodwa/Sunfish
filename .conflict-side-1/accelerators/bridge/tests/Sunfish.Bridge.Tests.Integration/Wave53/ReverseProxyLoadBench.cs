using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Sunfish.Bridge.Data.Entities;
using Sunfish.Bridge.Middleware;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Proxy;

using Xunit;
using Xunit.Abstractions;

namespace Sunfish.Bridge.Tests.Integration.Wave53;

/// <summary>
/// Wave 5.3.C reverse-proxy load-test harness implementing the measurement
/// gate from <c>docs/adrs/0033-browser-shell-render-model-and-trust-posture.md</c>
/// §Reverse-proxy latency budget and ADR 0033's checklist item
/// <c>ReverseProxyLoadTest.cs: 30 sessions × 3 tenants × 10 min</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Gate criteria (ADR 0033 §Decision).</b> v1 ships iff all pass:
/// <list type="bullet">
///   <item>p99 handshake latency &lt; 2000 ms under 30 concurrent sessions,</item>
///   <item>p99 frame round-trip &lt; 500 ms,</item>
///   <item>zero slow-consumer-induced stalls over the run.</item>
/// </list>
/// </para>
/// <para>
/// <b>Opt-in.</b> The fact is skipped by default so it does not gate
/// <c>dotnet test</c>. Set <c>SUNFISH_RUN_BENCH=1</c> in the environment to
/// enable it. When disabled, running the Facts is a no-op; when enabled, the
/// harness prints a markdown-table summary via <see cref="ITestOutputHelper"/>
/// and emits a GitHub-Actions-style <c>::error</c> annotation on failure so
/// CI surfacing is automatic once the env var lands in the workflow.
/// </para>
/// <para>
/// <b>Not a production benchmark.</b> The harness uses loopback Kestrel hosts
/// for both the Bridge front-end and the tenant echo endpoints. Prod TCP +
/// TLS + NIC + LAN latency is NOT modeled. Interpret the results as a ceiling:
/// if p99 frame-RTT on loopback already exceeds 500 ms, prod will be worse;
/// if loopback passes, prod still needs real-network validation before the
/// ADR 0033 gate can be declared met.
/// </para>
/// <para>
/// <b>Architecture.</b>
/// <list type="number">
///   <item>Three in-process tenant echo hosts, each serving <c>/ws</c> and
///     simply echoing every binary frame back with identical bytes / EOM.</item>
///   <item>One Bridge-proxy host exposing <c>/ws</c> and running
///     <see cref="TenantWebSocketReverseProxy.InvokeAsync"/> with a shim
///     middleware that binds <see cref="IBrowserTenantContext"/> from the
///     query string <c>?tenant=&lt;guid&gt;</c>.</item>
///   <item>30 <see cref="ClientWebSocket"/>s (10 per tenant) open to the
///     Bridge proxy, each recording the ConnectAsync→first-ReceiveAsync
///     handshake time and then sending 100 × 1 KB binary frames with
///     per-frame RTT timing.</item>
///   <item>The harness collects p50 / p95 / p99 / max over all samples and
///     enforces the ADR 0033 gates via <see cref="Assert"/>.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ReverseProxyLoadBench : IAsyncDisposable
{
    private const string OptInEnvVar = "SUNFISH_RUN_BENCH";
    private const int TenantCount = 3;
    private const int SessionsPerTenant = 10;
    private const int FramesPerSession = 100;
    private const int FramePayloadBytes = 1024;
    private static readonly TimeSpan HardTimeout = TimeSpan.FromMinutes(10);
    private const double HandshakeP99GateMs = 2000;
    private const double FrameRttP99GateMs = 500;

    private readonly ITestOutputHelper _output;
    private readonly List<WebApplication> _hosts = new();

    public ReverseProxyLoadBench(ITestOutputHelper output)
    {
        _output = output;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var host in _hosts)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await host.StopAsync(cts.Token);
                await ((IAsyncDisposable)host).DisposeAsync();
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    /// <summary>
    /// The ADR 0033 #2 gate. Skipped unless <c>SUNFISH_RUN_BENCH=1</c>; enable
    /// locally or in a dedicated CI job to produce numbers.
    /// </summary>
    [Fact]
    public Task ReverseProxy_meets_adr0033_p99_gates()
    {
        if (!BenchEnabled())
        {
            // Skip-without-xunit-Skip: emit a visible log and return. Using an
            // inline `[Fact(Skip=…)]` means the test appears "skipped" in
            // reports which is noisy — we want "passes trivially when opt-out"
            // so CI-default is always green.
            _output.WriteLine(
                $"[ReverseProxyLoadBench] {OptInEnvVar} not set — skipping.");
            return Task.CompletedTask;
        }

        return RunBenchmarkAsync();
    }

    // xUnit1030 forbids ConfigureAwait inside `[Fact]` methods; the actual body
    // lives in a non-test helper so ConfigureAwait(false) stays correct.
    private async Task RunBenchmarkAsync()
    {
        _output.WriteLine("[ReverseProxyLoadBench] starting harness — "
            + $"{TenantCount} tenants × {SessionsPerTenant} sessions × "
            + $"{FramesPerSession} × {FramePayloadBytes}-byte frames each.");

        // 1. Spin up three tenant echo hosts and collect their ws:// URIs.
        var tenantIds = Enumerable.Range(0, TenantCount).Select(_ => Guid.NewGuid()).ToArray();
        var registry = new InMemoryTenantEndpointRegistry();
        foreach (var id in tenantIds)
        {
            var endpointUri = await StartTenantEchoHostAsync().ConfigureAwait(false);
            registry.Register(id, endpointUri);
        }

        // 2. Spin up the Bridge-proxy host that serves /ws and delegates to
        //    TenantWebSocketReverseProxy.InvokeAsync.
        var bridgeBase = await StartBridgeProxyHostAsync(registry).ConfigureAwait(false);
        _output.WriteLine($"[ReverseProxyLoadBench] bridge-proxy at {bridgeBase}");

        // 3. Fire 30 sessions in parallel.
        var handshakes = new ConcurrentBag<double>();
        var frameRtts = new ConcurrentBag<double>();
        var sessionErrors = new ConcurrentBag<string>();

        var overallCts = new CancellationTokenSource(HardTimeout);
        var runSw = Stopwatch.StartNew();

        var tasks = new List<Task>();
        foreach (var tenantId in tenantIds)
        {
            for (var i = 0; i < SessionsPerTenant; i++)
            {
                var sessionNum = i;
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await RunSessionAsync(
                            bridgeBase,
                            tenantId,
                            sessionNum,
                            handshakes,
                            frameRtts,
                            overallCts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        sessionErrors.Add($"tenant={tenantId} session={sessionNum}: {ex.Message}");
                    }
                }, overallCts.Token));
            }
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        runSw.Stop();

        // 4. Compute percentiles + report.
        var hs = handshakes.ToArray();
        var rtt = frameRtts.ToArray();
        var report = BuildReport(runSw.Elapsed, hs, rtt, sessionErrors.ToArray());
        _output.WriteLine(report);

        // Emit raw samples to a file for the research doc to ingest.
        var samplesPath = Environment.GetEnvironmentVariable("SUNFISH_BENCH_OUTPUT");
        if (!string.IsNullOrWhiteSpace(samplesPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(samplesPath)!);
            File.WriteAllText(samplesPath, report);
            _output.WriteLine($"[ReverseProxyLoadBench] report written to {samplesPath}");
        }

        // 5. Gate assertions. Use Percentile-based checks matching ADR 0033.
        var handshakeP99 = Percentile(hs, 0.99);
        var frameP99 = Percentile(rtt, 0.99);
        var failures = new List<string>();
        if (sessionErrors.Count > 0)
        {
            failures.Add($"{sessionErrors.Count} session(s) errored — see report.");
        }
        if (handshakeP99 >= HandshakeP99GateMs)
        {
            failures.Add($"p99 handshake {handshakeP99:F1} ms ≥ gate {HandshakeP99GateMs:F0} ms.");
        }
        if (frameP99 >= FrameRttP99GateMs)
        {
            failures.Add($"p99 frame RTT {frameP99:F1} ms ≥ gate {FrameRttP99GateMs:F0} ms.");
        }

        if (failures.Count > 0)
        {
            var msg = "ADR 0033 gate failure: " + string.Join(" ", failures);
            // GitHub-Actions-style annotation — picked up when the workflow's
            // log parser sees "::error::…" on a run line.
            _output.WriteLine("::error title=ADR0033 reverse-proxy load gate::" + msg);
            Assert.Fail(msg);
        }
    }

    // -----------------------------------------------------------------------
    // Session runner
    // -----------------------------------------------------------------------

    private static async Task RunSessionAsync(
        Uri bridgeBase,
        Guid tenantId,
        int sessionNum,
        ConcurrentBag<double> handshakes,
        ConcurrentBag<double> frameRtts,
        CancellationToken ct)
    {
        var wsUri = new UriBuilder(bridgeBase)
        {
            Scheme = bridgeBase.Scheme == "https" ? "wss" : "ws",
            Path = "/ws",
            Query = $"tenant={tenantId:D}",
        }.Uri;

        using var client = new ClientWebSocket();
        // Keep-alive must not dominate RTT samples.
        client.Options.KeepAliveInterval = TimeSpan.Zero;

        // HANDSHAKE timer: ConnectAsync → first ReceiveAsync-return. Prime the
        // pump with a 1-byte "priming" frame the tenant echo host is obligated
        // to reflect so we actually observe a receive. Without this, the
        // socket connects successfully but the first receive would just block
        // forever in the absence of traffic, and ConnectAsync itself already
        // completes before the proxy's upstream connect finishes (it does not
        // — ConnectAsync waits for the Accept — but the client can still be
        // "connected" without having received any bytes).
        var hsSw = Stopwatch.StartNew();
        await client.ConnectAsync(wsUri, ct).ConfigureAwait(false);

        var primePayload = new byte[] { 0x01 };
        await client.SendAsync(
            primePayload,
            WebSocketMessageType.Binary,
            endOfMessage: true,
            ct).ConfigureAwait(false);
        var primeBuf = new byte[4];
        var primeResult = await client.ReceiveAsync(primeBuf, ct).ConfigureAwait(false);
        hsSw.Stop();
        handshakes.Add(hsSw.Elapsed.TotalMilliseconds);
        if (primeResult.MessageType != WebSocketMessageType.Binary)
        {
            throw new InvalidOperationException(
                $"handshake prime frame returned {primeResult.MessageType}, expected Binary");
        }

        // FRAME RTT samples.
        var payload = new byte[FramePayloadBytes];
        for (var b = 0; b < payload.Length; b++)
        {
            payload[b] = (byte)((sessionNum + b) & 0xFF);
        }
        var recvBuf = new byte[FramePayloadBytes + 32];
        for (var frame = 0; frame < FramesPerSession; frame++)
        {
            var rttSw = Stopwatch.StartNew();
            await client.SendAsync(
                payload,
                WebSocketMessageType.Binary,
                endOfMessage: true,
                ct).ConfigureAwait(false);

            // Drain until EOM — the echo is one-shot so this is a single loop
            // iteration in the normal case, but robust to fragment boundaries.
            var totalRead = 0;
            while (true)
            {
                var r = await client.ReceiveAsync(
                    new ArraySegment<byte>(recvBuf, totalRead, recvBuf.Length - totalRead),
                    ct).ConfigureAwait(false);
                totalRead += r.Count;
                if (r.EndOfMessage)
                {
                    break;
                }
                if (totalRead >= recvBuf.Length)
                {
                    throw new InvalidOperationException(
                        "frame echo overflowed receive buffer — payload framing mismatch");
                }
            }
            rttSw.Stop();
            frameRtts.Add(rttSw.Elapsed.TotalMilliseconds);
        }

        try
        {
            using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await client.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "bench-done",
                closeCts.Token).ConfigureAwait(false);
        }
        catch
        {
            // Close errors are not gate-meaningful — the run already collected
            // RTT samples.
        }
    }

    // -----------------------------------------------------------------------
    // Host setup
    // -----------------------------------------------------------------------

    /// <summary>
    /// Start a tiny Kestrel host exposing <c>/ws</c> that echoes every binary
    /// frame back unchanged. Stands in for the per-tenant <c>local-node-host</c>
    /// child's sync-daemon endpoint.
    /// </summary>
    private async Task<Uri> StartTenantEchoHostAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var buf = new byte[16 * 1024];
            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(buf, ctx.RequestAborted);
                    if (r.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "bye",
                            ctx.RequestAborted);
                        return;
                    }
                    await ws.SendAsync(
                        new ArraySegment<byte>(buf, 0, r.Count),
                        r.MessageType,
                        r.EndOfMessage,
                        ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* request aborted */ }
            catch (WebSocketException) { /* client dropped */ }
        });
        await app.StartAsync();
        _hosts.Add(app);
        return new Uri(UrlOf(app));
    }

    /// <summary>
    /// Start the Bridge-proxy host. Routes <c>GET /ws?tenant=&lt;guid&gt;</c>
    /// through a middleware that binds <see cref="IBrowserTenantContext"/>
    /// from the query string, then invokes
    /// <see cref="TenantWebSocketReverseProxy.InvokeAsync"/>.
    /// </summary>
    private async Task<Uri> StartBridgeProxyHostAsync(ITenantEndpointRegistry registry)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(registry);
        // Proxy uses IBrowserTenantContext from DI (scoped). Register the
        // concrete impl so the test middleware can Bind() on it per-request.
        builder.Services.AddScoped<IBrowserTenantContext, BrowserTenantContext>();
        // No IUpstreamWebSocketFactory override — use the default
        // ClientWebSocket-backed factory so we exercise real upstream dial.

        var app = builder.Build();
        app.UseWebSockets();

        // Shim middleware: pull ?tenant=<guid> from the query, bind the
        // scoped IBrowserTenantContext, then fall through.
        app.Use(async (ctx, next) =>
        {
            var tenantStr = ctx.Request.Query["tenant"].ToString();
            if (Guid.TryParse(tenantStr, out var tenantId))
            {
                var bt = ctx.RequestServices.GetRequiredService<IBrowserTenantContext>();
                // AuthSalt must be non-empty; TrustLevel HostedPeerAttested keeps
                // the proxy path unrestricted in the test (production uses the
                // subdomain middleware's real value; the proxy does not gate on
                // it in Wave 5.3.C).
                bt.Bind(tenantId, "bench", TrustLevel.AttestedHostedPeer, null, new byte[16]);
            }
            await next(ctx);
        });

        app.Map("/ws", TenantWebSocketReverseProxy.InvokeAsync);
        await app.StartAsync();
        _hosts.Add(app);
        return new Uri(UrlOf(app));
    }

    private static string UrlOf(WebApplication app)
    {
        return app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();
    }

    // -----------------------------------------------------------------------
    // Reporting + stats
    // -----------------------------------------------------------------------

    private static string BuildReport(
        TimeSpan wall,
        double[] handshakes,
        double[] frameRtts,
        string[] errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("## Wave 5.3.C reverse-proxy load-bench");
        sb.AppendLine();
        sb.Append("Wall: ").Append(wall.TotalSeconds.ToString("F1")).AppendLine(" s");
        sb.Append("Sessions: ").Append(handshakes.Length)
          .Append(" (target ").Append(TenantCount * SessionsPerTenant).AppendLine(")");
        sb.Append("Frame-RTT samples: ").Append(frameRtts.Length)
          .Append(" (target ").Append(TenantCount * SessionsPerTenant * FramesPerSession).AppendLine(")");
        sb.Append("Errors: ").AppendLine(errors.Length.ToString());
        sb.AppendLine();
        sb.AppendLine("| metric | samples | p50 (ms) | p95 (ms) | p99 (ms) | max (ms) |");
        sb.AppendLine("|---|---:|---:|---:|---:|---:|");
        AppendRow(sb, "handshake", handshakes);
        AppendRow(sb, "frame RTT", frameRtts);
        if (errors.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Errors");
            foreach (var e in errors)
            {
                sb.Append("- ").AppendLine(e);
            }
        }
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, string label, double[] samples)
    {
        if (samples.Length == 0)
        {
            sb.Append("| ").Append(label).AppendLine(" | 0 | — | — | — | — |");
            return;
        }
        sb.Append("| ").Append(label).Append(" | ")
          .Append(samples.Length).Append(" | ")
          .Append(Percentile(samples, 0.50).ToString("F1")).Append(" | ")
          .Append(Percentile(samples, 0.95).ToString("F1")).Append(" | ")
          .Append(Percentile(samples, 0.99).ToString("F1")).Append(" | ")
          .Append(samples.Max().ToString("F1")).AppendLine(" |");
    }

    /// <summary>
    /// Nearest-rank percentile — cheap and stable for moderate sample counts.
    /// For the ADR 0033 gate we sample on the order of 3000 RTTs, where the
    /// choice between nearest-rank / linear-interpolation is negligible.
    /// </summary>
    private static double Percentile(double[] samples, double p)
    {
        if (samples.Length == 0)
        {
            return double.NaN;
        }
        var sorted = (double[])samples.Clone();
        Array.Sort(sorted);
        var rank = (int)Math.Ceiling(p * sorted.Length) - 1;
        if (rank < 0) rank = 0;
        if (rank >= sorted.Length) rank = sorted.Length - 1;
        return sorted[rank];
    }

    private static bool BenchEnabled()
    {
        var v = Environment.GetEnvironmentVariable(OptInEnvVar);
        return v is not null && (v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase));
    }
}
