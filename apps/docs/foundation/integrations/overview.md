---
uid: foundation-integrations-overview
title: Integrations — Overview
description: The third-party integration framework — provider registry, credentials, webhook dispatch, and sync cursors.
---

# Integrations — Overview

## What this package gives you

`Sunfish.Foundation.Integrations` is the seam for connecting Sunfish to external providers — billing engines, payment gateways, bank feeds, channel managers, messaging services, identity providers, and the rest of the categories declared in `Sunfish.Foundation.Catalog.Bundles.ProviderCategory`. It offers five small, focused contracts:

- **`IProviderRegistry`** — the registry of adapters that have been wired into the host.
- **`CredentialsReference`** — an opaque pointer to credentials that never carries the plaintext secret.
- **`IWebhookEventDispatcher`** + **`IWebhookEventHandler`** + **`WebhookEventEnvelope`** — inbound webhook dispatch.
- **`ISyncCursorStore`** + **`SyncCursor`** — persisted position markers for pull-based integrations.
- **`IProviderHealthCheck`** + **`ProviderHealthReport`** — live health reporting for admin surfaces.

The package source lives at `packages/foundation-integrations/`. Each contract has an in-memory reference implementation and is intended to be overridden by adapter packages (the package that knows how to talk to Stripe, Plaid, Twilio, etc.) and by host adapters (Bridge's durable provider registry, Bridge's Postgres-backed cursor store).

## Why "provider" rather than "integration"

Bundles (`Sunfish.Foundation.Catalog.Bundles`) declare **requirements** in terms of categories — "this bundle needs a Billing provider". Individual vendor adapters register **descriptors** that claim a category — "I am the Stripe adapter, I serve the Billing category". Separating the requirement from the vendor keeps bundles portable: swapping the concrete billing adapter does not change any bundle manifest.

## Key types

| Type | Purpose |
|---|---|
| [`ProviderDescriptor`](xref:Sunfish.Foundation.Integrations.ProviderDescriptor) | Metadata for one registered provider (key, category, name, version, capabilities, supported regions). |
| [`IProviderRegistry`](xref:Sunfish.Foundation.Integrations.IProviderRegistry) | Registry contract — `Register`, `GetAll`, `GetByCategory`, `TryGet`. |
| [`CredentialsReference`](xref:Sunfish.Foundation.Integrations.CredentialsReference) | Opaque reference to externally stored credentials. |
| [`WebhookEventEnvelope`](xref:Sunfish.Foundation.Integrations.WebhookEventEnvelope) | Normalized inbound webhook record. |
| [`IWebhookEventDispatcher`](xref:Sunfish.Foundation.Integrations.IWebhookEventDispatcher) / [`IWebhookEventHandler`](xref:Sunfish.Foundation.Integrations.IWebhookEventHandler) | Dispatch + handler contracts. |
| [`SyncCursor`](xref:Sunfish.Foundation.Integrations.SyncCursor) / [`ISyncCursorStore`](xref:Sunfish.Foundation.Integrations.ISyncCursorStore) | Last-synced position for a `(provider, tenant, scope)` tuple. |
| [`ProviderHealthStatus`](xref:Sunfish.Foundation.Integrations.ProviderHealthStatus) / [`IProviderHealthCheck`](xref:Sunfish.Foundation.Integrations.IProviderHealthCheck) / [`ProviderHealthReport`](xref:Sunfish.Foundation.Integrations.ProviderHealthReport) | Live health reporting. |

## Registering the defaults

```csharp
using Sunfish.Foundation.Integrations;

services.AddSunfishIntegrations();
```

`AddSunfishIntegrations` registers `InMemoryProviderRegistry`, `InMemoryWebhookEventDispatcher`, and `InMemorySyncCursorStore` as singletons. Provider adapter packages add themselves to the registry at startup; durable cursor-store adapters plug into the same slot.

## Related

- [Provider Registry](registry.md)
- [Credentials](credentials.md)
- [Webhooks](webhooks.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
