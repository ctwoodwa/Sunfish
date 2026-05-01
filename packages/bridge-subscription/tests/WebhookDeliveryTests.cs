using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Sunfish.Bridge.Subscription.Tests;

public sealed class WebhookDeliveryTests
{
    private static readonly Uri Callback = new("https://anchor.example/sunfish/bridge-events");
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static BridgeSubscriptionEvent NewEvent() => new()
    {
        TenantId = "tenant-a",
        EventType = BridgeSubscriptionEventType.SubscriptionTierUpgraded,
        EditionBefore = "anchor-self-host",
        EditionAfter = "bridge-pro",
        EffectiveAt = Now,
        EventId = Guid.Parse("7f9d2a00-0000-0000-0000-000000000000"),
        DeliveryAttempt = 1,
        Signature = "hmac-sha256:placeholder",
    };

    [Fact]
    public async Task DeliverAsync_FirstAttemptSucceeds_ReturnsDelivered()
    {
        var responses = new[] { Status(HttpStatusCode.OK) };
        var (svc, dlq, recordedDelays) = NewService(responses);

        var outcome = await svc.DeliverAsync(NewEvent(), Callback);

        Assert.Equal(WebhookDeliveryOutcome.Delivered, outcome);
        Assert.Empty(await dlq.GetByTenantAsync("tenant-a"));
        Assert.Empty(recordedDelays); // first attempt is immediate (zero delay skipped)
    }

    [Fact]
    public async Task DeliverAsync_RetryThenSucceeds_RecordsBackoffAndReturnsDelivered()
    {
        var responses = new[] { Status(HttpStatusCode.ServiceUnavailable), Status(HttpStatusCode.OK) };
        var (svc, dlq, recordedDelays) = NewService(responses);

        var outcome = await svc.DeliverAsync(NewEvent(), Callback);

        Assert.Equal(WebhookDeliveryOutcome.Delivered, outcome);
        Assert.Empty(await dlq.GetByTenantAsync("tenant-a"));
        Assert.Single(recordedDelays);
        Assert.Equal(TimeSpan.FromSeconds(1), recordedDelays[0]); // 1s before attempt 2
    }

    [Fact]
    public async Task DeliverAsync_AllAttemptsFail_ReturnsDeadLetteredAndPersistsToDlq()
    {
        var responses = new HttpResponseMessage[8];
        for (var i = 0; i < responses.Length; i++) responses[i] = Status(HttpStatusCode.ServiceUnavailable);
        var (svc, dlq, recordedDelays) = NewService(responses);

        var outcome = await svc.DeliverAsync(NewEvent(), Callback);

        Assert.Equal(WebhookDeliveryOutcome.DeadLettered, outcome);
        var dlqEntries = await dlq.GetByTenantAsync("tenant-a");
        var entry = Assert.Single(dlqEntries);
        Assert.Equal(8, entry.Event.DeliveryAttempt);
        Assert.Equal("http-503", entry.Reason);

        // 7 backoffs recorded; attempt 1 had no delay.
        Assert.Equal(7, recordedDelays.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), recordedDelays[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), recordedDelays[1]);
        Assert.Equal(TimeSpan.FromSeconds(30), recordedDelays[2]);
        Assert.Equal(TimeSpan.FromMinutes(5), recordedDelays[3]);
        Assert.Equal(TimeSpan.FromMinutes(30), recordedDelays[4]);
        Assert.Equal(TimeSpan.FromHours(2), recordedDelays[5]);
        Assert.Equal(TimeSpan.FromHours(12), recordedDelays[6]);
    }

    [Fact]
    public async Task DeliverAsync_NetworkException_TreatedAsRetryable()
    {
        var responses = new HttpResponseMessage[]
        {
            null!, // sentinel: throw HttpRequestException
            Status(HttpStatusCode.OK),
        };
        var (svc, dlq, _) = NewService(responses);

        var outcome = await svc.DeliverAsync(NewEvent(), Callback);

        Assert.Equal(WebhookDeliveryOutcome.Delivered, outcome);
        Assert.Empty(await dlq.GetByTenantAsync("tenant-a"));
    }

    [Fact]
    public async Task DeliverAsync_DeliveryAttemptFieldBumpsPerAttempt()
    {
        var attempts = new List<int>();
        var handler = new RecordingMessageHandler(attempts);
        var http = new HttpClient(handler);
        var dlq = new InMemoryDeadLetterQueue();
        var svc = new DefaultWebhookDeliveryService(http, dlq, (_, _) => Task.CompletedTask);

        await svc.DeliverAsync(NewEvent(), Callback);

        // 8 attempts seen by the handler — each with the 1-indexed attempt number.
        Assert.Equal(8, attempts.Count);
        for (var i = 0; i < attempts.Count; i++)
        {
            Assert.Equal(i + 1, attempts[i]);
        }
    }

    [Fact]
    public async Task DeliverAsync_HonorsCancellation()
    {
        var (svc, _, _) = NewService(new[] { Status(HttpStatusCode.OK) });
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.DeliverAsync(NewEvent(), Callback, cts.Token).AsTask());
    }

    [Fact]
    public async Task DeliverAsync_NullArgs_Throw()
    {
        var (svc, _, _) = NewService(new[] { Status(HttpStatusCode.OK) });
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.DeliverAsync(null!, Callback).AsTask());
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            svc.DeliverAsync(NewEvent(), null!).AsTask());
    }

    private static (DefaultWebhookDeliveryService svc, InMemoryDeadLetterQueue dlq, List<TimeSpan> recordedDelays) NewService(IReadOnlyList<HttpResponseMessage?> responses)
    {
        var handler = new ScriptedMessageHandler(responses);
        var http = new HttpClient(handler);
        var dlq = new InMemoryDeadLetterQueue();
        var recordedDelays = new List<TimeSpan>();
        Func<TimeSpan, CancellationToken, Task> delay = (d, _) => { recordedDelays.Add(d); return Task.CompletedTask; };
        var svc = new DefaultWebhookDeliveryService(http, dlq, delay);
        return (svc, dlq, recordedDelays);
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code);

    private sealed class ScriptedMessageHandler : HttpMessageHandler
    {
        private readonly IReadOnlyList<HttpResponseMessage?> _responses;
        private int _index;
        public ScriptedMessageHandler(IReadOnlyList<HttpResponseMessage?> responses) => _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = _index < _responses.Count ? _responses[_index] : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            _index++;
            if (response is null)
            {
                throw new HttpRequestException("simulated network error");
            }
            return Task.FromResult(response);
        }
    }

    private sealed class RecordingMessageHandler : HttpMessageHandler
    {
        private readonly List<int> _attemptsSeen;
        public RecordingMessageHandler(List<int> attemptsSeen) => _attemptsSeen = attemptsSeen;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            // Parse out deliveryAttempt naively — System.Text.Json would be heavier; substring is enough.
            // The wire form is camelCase per the [JsonPropertyName] attrs.
            var marker = "\"deliveryAttempt\":";
            var idx = body.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var sub = body[(idx + marker.Length)..].Trim();
                var commaIdx = sub.IndexOfAny(new[] { ',', '}' });
                var num = int.Parse(sub[..commaIdx]);
                _attemptsSeen.Add(num);
            }
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }
    }
}
