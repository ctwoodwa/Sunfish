using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Bridge.Client.Services;

/// <summary>
/// Captured per-circuit identity context for InteractiveServer Blazor components.
/// Populated during the initial HTTP connection (when IBrowserTenantContext and
/// IHttpContextAccessor are both live); remains stable for the circuit lifetime.
/// Callers MUST check <see cref="IsResolved"/> before reading identity fields.
/// </summary>
public interface IBridgeRequestContext
{
    /// <summary>True once the circuit captured a resolved tenant + authenticated actor.</summary>
    bool IsResolved { get; }

    /// <summary>Tenant scoped to the request's Host subdomain.</summary>
    TenantId TenantId { get; }

    /// <summary>Authenticated actor derived from the bearer claims.</summary>
    ActorId ActorId { get; }
}
