using System.Collections.Generic;

namespace Sunfish.Foundation.Authorization;

/// <summary>
/// Resolves the current tenant and caller identity. Scoped per request.
/// Accelerators / apps register an implementation (e.g. claims-based) in DI.
/// </summary>
public interface ITenantContext
{
    string TenantId { get; }
    string UserId { get; }
    IReadOnlyList<string> Roles { get; }
    bool HasPermission(string permission);
}
