---
uid: foundation-integrations-webhooks
title: Integrations — Webhooks
description: The webhook envelope, dispatcher, handlers, and the boundary where provider-specific signatures are verified.
keywords:
  - webhooks
  - IWebhookEventDispatcher
  - IWebhookEventHandler
  - WebhookEventEnvelope
  - signature verification
  - ADR 0013
---

# Integrations — Webhooks

## The envelope

Every inbound webhook reaches Sunfish as a `WebhookEventEnvelope`:

```csharp
public sealed record WebhookEventEnvelope
{
    public required string ProviderKey { get; init; }
    public required string EventId { get; init; }
    public required string EventType { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public required byte[] RawBody { get; init; }
    public TenantId? TenantId { get; init; }
    public IReadOnlyDictionary<string, string> SignatureMetadata { get; init; } = ...;
}
```

The envelope carries a provider identifier, a provider-assigned event identifier, a provider-assigned event type, the raw request body, optional tenant scope, and signature metadata. The dispatcher never re-interprets these fields — it routes the envelope to every registered handler that matches `ProviderKey` and `EventType`.

`RawBody` is preserved so handlers and auditors can re-verify signatures, re-parse with alternate schemas, and replay events if needed.

## The signature-verification boundary

Adapters — the package that owns an inbound webhook endpoint for a specific provider — verify provider-specific signatures **before** constructing the envelope. This is the hard rule: if `IWebhookEventDispatcher` receives an envelope, the signature has already passed. Handlers trust the envelope; they do not re-verify.

Pragmatically this means each provider adapter ships:

- An ASP.NET Core endpoint (in Bridge, by convention) that reads the raw body, runs the provider's signature check (Stripe's `Stripe-Signature`, Twilio's `X-Twilio-Signature`, GitHub's `X-Hub-Signature-256`, etc.), and decides whether to admit the request.
- A short translation step that produces a `WebhookEventEnvelope` from the verified request.
- A call to `IWebhookEventDispatcher.DispatchAsync(envelope)`.

`SignatureMetadata` carries the scheme, signature value, and a `verified` flag so audit logs can reconstruct exactly which verification step ran.

## The dispatcher

```csharp
public interface IWebhookEventDispatcher
{
    void Register(IWebhookEventHandler handler);
    ValueTask DispatchAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default);
}
```

The dispatcher is append-only: handlers register once at startup and stay registered. `DispatchAsync` snapshots the current handler list and invokes every handler whose `ProviderKey` and `EventType` match the envelope, in registration order. Multiple handlers may fire for a single envelope.

`InMemoryWebhookEventDispatcher` is the shipped reference implementation. Durable adapters can plug in later by replacing the registration — the contract itself is simple enough to re-host.

## Handlers

```csharp
public interface IWebhookEventHandler
{
    string ProviderKey { get; }
    string EventType { get; }
    ValueTask HandleAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken = default);
}
```

A handler binds a `(ProviderKey, EventType)` pair to one piece of business logic:

```csharp
public sealed class MarkSubscriptionPaidHandler : IWebhookEventHandler
{
    public string ProviderKey => "sunfish.providers.acme-billing";
    public string EventType  => "invoice.paid";

    private readonly ISubscriptionService _subscriptions;
    public MarkSubscriptionPaidHandler(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    public ValueTask HandleAsync(WebhookEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Deserialize<AcmeInvoicePaidBody>(envelope.RawBody)
            ?? throw new InvalidDataException("Invoice body was not valid JSON.");
        return _subscriptions.MarkPaidAsync(body.SubscriptionId, body.PaidAt, cancellationToken);
    }
}
```

Registration is one line at startup:

```csharp
services.AddSingleton<IWebhookEventHandler, MarkSubscriptionPaidHandler>();
// ... then in a startup hook:
var dispatcher = sp.GetRequiredService<IWebhookEventDispatcher>();
foreach (var handler in sp.GetServices<IWebhookEventHandler>())
{
    dispatcher.Register(handler);
}
```

## Retry and idempotency

The Foundation contract does not own retry scheduling. Webhook endpoints — the HTTP-facing surface in Bridge — decide HTTP-level retry semantics (return 2xx to acknowledge, 5xx to ask the provider to resend, 4xx to reject). Handler bodies are expected to be **idempotent** by `EventId`: a provider that retries a webhook must be able to re-send the same envelope without producing duplicate side effects. Common implementations deduplicate on `(ProviderKey, EventId)` in the handler's own storage.

## Sync cursors — the other direction

For pull-based integrations (ones where Sunfish polls the provider rather than the provider pushing), `ISyncCursorStore` and `SyncCursor` persist the last-synced position:

```csharp
public sealed record SyncCursor
{
    public required string ProviderKey { get; init; }
    public TenantId? TenantId { get; init; }
    public required string Scope { get; init; }
    public required byte[] Value { get; init; }     // opaque per adapter
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
```

Each adapter defines its own cursor semantics (a timestamp, a sequence number, a provider-specific continuation token). The store exists so that restarts do not replay history and so that a given `(provider, tenant, scope)` tuple has a single authoritative position.

## Related

- [Overview](overview.md)
- [Provider Registry](registry.md)
- [Credentials](credentials.md)
