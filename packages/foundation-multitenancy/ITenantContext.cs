namespace Sunfish.Foundation.MultiTenancy;

/// <summary>
/// The resolved tenant for the current scope (request, job, operation). Identity-only —
/// user identity and authorization live in separate abstractions. Distinct from
/// <c>Sunfish.Foundation.Authorization.ITenantContext</c> (see ADR 0008).
/// </summary>
public interface ITenantContext
{
    /// <summary>The resolved tenant metadata, or null if no tenant has been resolved.</summary>
    TenantMetadata? Tenant { get; }

    /// <summary>True when <see cref="Tenant"/> is non-null.</summary>
    bool IsResolved => Tenant is not null;
}
