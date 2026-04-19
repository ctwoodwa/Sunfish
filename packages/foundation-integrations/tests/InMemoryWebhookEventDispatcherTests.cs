using Sunfish.Foundation.Integrations;

namespace Sunfish.Foundation.Integrations.Tests;

public class InMemoryWebhookEventDispatcherTests
{
    [Fact]
    public async Task Dispatches_only_to_matching_handlers()
    {
        var dispatcher = new InMemoryWebhookEventDispatcher();
        var stripePaymentSucceeded = new RecordingHandler("sunfish.providers.stripe", "payment.succeeded");
        var stripePaymentFailed = new RecordingHandler("sunfish.providers.stripe", "payment.failed");
        var plaidTxn = new RecordingHandler("sunfish.providers.plaid", "transaction.added");
        dispatcher.Register(stripePaymentSucceeded);
        dispatcher.Register(stripePaymentFailed);
        dispatcher.Register(plaidTxn);

        await dispatcher.DispatchAsync(new WebhookEventEnvelope
        {
            ProviderKey = "sunfish.providers.stripe",
            EventId = "evt_1",
            EventType = "payment.succeeded",
            RawBody = Encoding.UTF8.GetBytes("{}"),
        });

        Assert.Equal(1, stripePaymentSucceeded.Calls);
        Assert.Equal(0, stripePaymentFailed.Calls);
        Assert.Equal(0, plaidTxn.Calls);
    }

    [Fact]
    public async Task Fires_multiple_handlers_in_registration_order()
    {
        var dispatcher = new InMemoryWebhookEventDispatcher();
        var observed = new List<string>();
        dispatcher.Register(new CallbackHandler("p", "e", () => observed.Add("first")));
        dispatcher.Register(new CallbackHandler("p", "e", () => observed.Add("second")));

        await dispatcher.DispatchAsync(new WebhookEventEnvelope
        {
            ProviderKey = "p",
            EventId = "evt",
            EventType = "e",
            RawBody = Array.Empty<byte>(),
        });

        Assert.Equal(new[] { "first", "second" }, observed);
    }

    private sealed class RecordingHandler(string providerKey, string eventType) : IWebhookEventHandler
    {
        public string ProviderKey { get; } = providerKey;
        public string EventType { get; } = eventType;
        public int Calls { get; private set; }

        public ValueTask HandleAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CallbackHandler(string providerKey, string eventType, Action onCall) : IWebhookEventHandler
    {
        public string ProviderKey { get; } = providerKey;
        public string EventType { get; } = eventType;

        public ValueTask HandleAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default)
        {
            onCall();
            return ValueTask.CompletedTask;
        }
    }
}
