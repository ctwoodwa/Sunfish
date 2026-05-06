using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Assets.Audit;

/// <summary>
/// Supplies ambient actor + tenant context for audit-record construction.
/// </summary>
/// <remarks>
/// Plan D-EXTENSIBILITY-SEAMS. Phase A default is <see cref="NullAuditContextProvider"/>,
/// which returns <see cref="ActorId.System"/> + <see cref="TenantId.System"/>. Consumers
/// can override via DI to fold in HTTP middleware context. Per ADR 0084 §4 (W#1 WS-A) the
/// fallback tenant migrated from <c>TenantId.Default</c> to
/// <see cref="TenantId.System"/> — system context is the correct semantic for an
/// unconfigured ambient context.
/// </remarks>
public interface IAuditContextProvider
{
    /// <summary>Returns the ambient actor.</summary>
    ActorId GetActor();

    /// <summary>Returns the ambient tenant.</summary>
    TenantId GetTenant();
}

/// <summary>Default provider returning <see cref="ActorId.System"/> and <see cref="TenantId.System"/>.</summary>
public sealed class NullAuditContextProvider : IAuditContextProvider
{
    /// <summary>Singleton instance.</summary>
    public static NullAuditContextProvider Instance { get; } = new();

    /// <inheritdoc />
    public ActorId GetActor() => ActorId.System;

    /// <inheritdoc />
    public TenantId GetTenant() => TenantId.System;
}
