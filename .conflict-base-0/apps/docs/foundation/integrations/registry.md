---
uid: foundation-integrations-registry
title: Integrations — Provider Registry
description: How provider adapters register themselves, how consumers look them up, and how the registry pairs with bundle provider requirements.
keywords:
  - provider registry
  - IProviderRegistry
  - ProviderDescriptor
  - IProviderHealthCheck
  - adapter registration
  - ADR 0013
---

# Integrations — Provider Registry

## Registering a provider

Every provider adapter package registers a `ProviderDescriptor` at startup. The descriptor is declarative metadata — it does not contain adapter logic, transport code, or credentials.

```csharp
public sealed record ProviderDescriptor
{
    public required string Key { get; init; }            // reverse-DNS style
    public required ProviderCategory Category { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public IReadOnlyList<string> SupportedRegions { get; init; } = [];
}
```

An adapter exposes a DI extension method that registers both the descriptor and the concrete services the adapter provides:

```csharp
public static IServiceCollection AddAcmeBillingProvider(
    this IServiceCollection services,
    Action<AcmeBillingOptions> configure)
{
    services.Configure(configure);
    services.AddSingleton<IBillingGateway, AcmeBillingGateway>();

    // Register the descriptor
    services.AddSingleton(new ProviderDescriptor
    {
        Key = "sunfish.providers.acme-billing",
        Category = ProviderCategory.Billing,
        Name = "ACME Billing",
        Version = "1.0.0",
        Capabilities = ["invoicing", "usage-metering", "tax-calculation"],
        SupportedRegions = ["us", "eu"],
    });

    // Self-register the descriptor into the runtime registry on startup.
    services.AddHostedService<ProviderRegistrar<AcmeBillingDescriptor>>();
    return services;
}
```

An alternative pattern registers the descriptor directly into `IProviderRegistry` via a startup `IHostedService` or a registration action passed to `AddSunfishIntegrations`. Host accelerators standardize one pattern; adapter packages follow it.

## Looking up providers

Consumers read the registry to enumerate what is available or to resolve a specific provider by key or category.

```csharp
public interface IProviderRegistry
{
    void Register(ProviderDescriptor descriptor);
    IReadOnlyList<ProviderDescriptor> GetAll();
    IReadOnlyList<ProviderDescriptor> GetByCategory(ProviderCategory category);
    bool TryGet(string key, [NotNullWhen(true)] out ProviderDescriptor? descriptor);
}
```

Typical usage:

```csharp
public sealed class BillingProviderChooser
{
    private readonly IProviderRegistry _providers;

    public BillingProviderChooser(IProviderRegistry providers) => _providers = providers;

    public ProviderDescriptor? PickForRegion(string region)
        => _providers
            .GetByCategory(ProviderCategory.Billing)
            .FirstOrDefault(p => p.SupportedRegions.Contains(region, StringComparer.OrdinalIgnoreCase));
}
```

Duplicate provider keys throw on registration — every descriptor has a single canonical owner.

## Pairing with bundle requirements

The `ProviderCategory` enum is shared between `Sunfish.Foundation.Catalog.Bundles` and `Sunfish.Foundation.Integrations`. Bundles declare requirements in terms of categories; adapters register descriptors tagged with those categories. At activation time, a host can validate that every `Required` provider category in a bundle's `ProviderRequirements` has at least one descriptor in the registry, and refuse activation otherwise.

## Health reporting

Adapters that can introspect their own liveness implement `IProviderHealthCheck`:

```csharp
public interface IProviderHealthCheck
{
    string ProviderKey { get; }
    ValueTask<ProviderHealthReport> CheckAsync(CancellationToken cancellationToken = default);
}
```

Bridge admin and ops dashboards aggregate reports across every registered check to show live integration status. The `ProviderHealthStatus` enum — `Unknown`, `Healthy`, `Degraded`, `Unhealthy` — keeps the reporting coarse on purpose; granular latency and error-code detail live in the free-form `Detail` string.

## Related

- [Overview](overview.md)
- [Credentials](credentials.md)
- [Webhooks](webhooks.md)
- [Catalog — Bundle Manifests](../catalog/bundle-manifests.md)
