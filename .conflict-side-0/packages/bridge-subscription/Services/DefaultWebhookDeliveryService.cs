using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Bridge.Subscription;

/// <summary>
/// Reference <see cref="IWebhookDeliveryService"/> per ADR 0031-A1.3 +
/// A1.5. HTTPS POST with 30-second timeout; <see cref="WebhookRetryPolicy"/>
/// schedule (1s/5s/30s/5min/30min/2h/12h × 7); dead-letters on the 8th
/// failed attempt.
/// </summary>
/// <remarks>
/// Construction takes an <see cref="HttpClient"/> the host supplies —
/// production hosts wire this through <c>IHttpClientFactory</c> with
/// per-tenant cert-pinning (per <see cref="ITrustChainResolver"/>).
/// Tests inject a loopback <see cref="HttpClient"/> + fake delay
/// provider so the 1s/5s/30s/... schedule doesn't burn test runtime.
/// </remarks>
public sealed class DefaultWebhookDeliveryService : IWebhookDeliveryService
{
    /// <summary>Per A1.3.</summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly IDeadLetterQueue _dlq;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _requestTimeout;

    /// <summary>Production constructor — uses real <see cref="Task.Delay(TimeSpan, CancellationToken)"/>.</summary>
    public DefaultWebhookDeliveryService(HttpClient http, IDeadLetterQueue dlq, TimeSpan? requestTimeout = null)
        : this(http, dlq, Task.Delay, requestTimeout)
    {
    }

    /// <summary>Test constructor — accepts a delay sink so tests don't burn the 1s+5s+30s... schedule.</summary>
    public DefaultWebhookDeliveryService(
        HttpClient http,
        IDeadLetterQueue dlq,
        Func<TimeSpan, CancellationToken, Task> delay,
        TimeSpan? requestTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(dlq);
        ArgumentNullException.ThrowIfNull(delay);
        _http = http;
        _dlq = dlq;
        _delay = delay;
        _requestTimeout = requestTimeout ?? DefaultRequestTimeout;
    }

    /// <inheritdoc />
    public async ValueTask<WebhookDeliveryOutcome> DeliverAsync(
        BridgeSubscriptionEvent evt,
        Uri callbackUrl,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ArgumentNullException.ThrowIfNull(callbackUrl);

        string lastReason = "no-attempt";
        for (var attempt = 1; attempt <= WebhookRetryPolicy.MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            var preDelay = WebhookRetryPolicy.DelayBeforeAttempt(attempt);
            if (preDelay > TimeSpan.Zero)
            {
                await _delay(preDelay, ct).ConfigureAwait(false);
            }

            var attemptedEvent = evt with { DeliveryAttempt = attempt };
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(_requestTimeout);
                var resp = await _http.PostAsJsonAsync(callbackUrl, attemptedEvent, JsonSerializerOptions.Default, cts.Token)
                    .ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    return WebhookDeliveryOutcome.Delivered;
                }
                lastReason = $"http-{(int)resp.StatusCode}";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                lastReason = "timeout";
            }
            catch (HttpRequestException ex)
            {
                lastReason = $"network:{ex.HttpRequestError}";
            }
            catch (Exception ex)
            {
                lastReason = $"transport-error:{ex.GetType().Name}";
            }
        }

        await _dlq.EnqueueAsync(
            evt with { DeliveryAttempt = WebhookRetryPolicy.MaxAttempts },
            lastReason,
            ct).ConfigureAwait(false);
        return WebhookDeliveryOutcome.DeadLettered;
    }
}
