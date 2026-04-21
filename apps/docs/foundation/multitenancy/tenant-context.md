---
uid: foundation-multitenancy-tenant-context
title: Multitenancy — Tenant Context
description: The ITenantContext abstraction — what it exposes, how it is populated, and how consumers read it.
---

# Multitenancy — Tenant Context

## Purpose

`ITenantContext` is the question "**which tenant is this operation for?**" and nothing else. It does not know about callers, permissions, or authorization. A module that depends on `ITenantContext` is declaring a dependency on the tenant scope — no more.

## Shape

```csharp
public interface ITenantContext
{
    TenantMetadata? Tenant { get; }
    bool IsResolved => Tenant is not null;
}
```

`Tenant` is the resolved `TenantMetadata` for the current scope, or `null` if no tenant has been resolved (for example, during infrastructure startup or from a system-level job). `IsResolved` is a default-interface convenience for the "is a tenant even on the call stack?" check.

## How it gets populated

Sunfish does not prescribe a single population strategy — hosts decide.

- **Bridge (ASP.NET Core accelerator)** — A `BridgeTenantContext : ITenantContext` reads from Finbuckle's `IMultiTenantContext` via `IHttpContextAccessor`, looks up the corresponding `TenantMetadata` in the catalog, and caches it per scope. Finbuckle handles strategy (host, subdomain, path, header); the adapter only translates.
- **Lite-mode / single-tenant apps** — A fixed implementation returns the single configured tenant every time. `InMemoryTenantCatalog` pairs well with a `FixedTenantContext` composition.
- **Background jobs** — The job scheduler pushes an ambient `ITenantContext` (for example, through an `AsyncLocal<T>` accessor) so downstream DI-resolved services see the correct tenant.
- **Tests** — A test harness sets the current tenant explicitly before exercising tenant-scoped code paths.

## Resolving on demand

When a request carries a candidate identifier (host segment, claim value, route parameter, header value) and you need to look that up, use `ITenantResolver`:

```csharp
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ITenantResolver _resolver;

    public TenantResolutionMiddleware(RequestDelegate next, ITenantResolver resolver)
    {
        _next = next;
        _resolver = resolver;
    }

    public async Task InvokeAsync(HttpContext context, IScopedTenantWriter writer)
    {
        if (context.Request.Headers.TryGetValue("X-Tenant", out var header))
        {
            var tenant = await _resolver.ResolveAsync(header.ToString(), context.RequestAborted);
            if (tenant is { } resolved)
            {
                writer.Set(resolved);
            }
        }

        await _next(context);
    }
}
```

`ITenantResolver` returns `ValueTask<TenantMetadata?>` — `null` when the candidate does not match any registered tenant. Hosts decide what happens next (reject the request, fall back to a default tenant, continue without a tenant).

## Consuming the tenant

```csharp
public sealed class ReportGenerator
{
    private readonly ITenantContext _tenant;

    public ReportGenerator(ITenantContext tenant) => _tenant = tenant;

    public ReportHandle Generate()
    {
        if (_tenant.Tenant is not { } current)
        {
            throw new InvalidOperationException("Reports require an active tenant.");
        }

        return new ReportHandle(
            tenantId: current.Id,
            title: $"Report for {current.DisplayName ?? current.Name}");
    }
}
```

If your module truly needs tenant **and** caller identity **and** authorization together, hold three injected dependencies rather than one overloaded interface (see ADR 0008).

## Related

- [Overview](overview.md)
- [Tenant-Scoped Markers](tenant-scoped-markers.md)
- [ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary](xref:adr-0008-foundation-multitenancy)
