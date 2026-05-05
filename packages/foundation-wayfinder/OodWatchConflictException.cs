using System;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Thrown by <see cref="IOodWatchService"/> when a single-Active-watch
/// invariant would be violated — either <c>StartWatchAsync</c> with an
/// existing Active watch for the same (TenantId, OodRole) pair, or
/// <c>HandoverWatchAsync</c> against a watch that is no longer Active.
/// Per ADR 0078 §1.
/// </summary>
public sealed class OodWatchConflictException : InvalidOperationException
{
    /// <summary>Creates a conflict exception with the (existing-watch, tenant, role) context.</summary>
    public OodWatchConflictException(OodWatchId existingWatchId, TenantId tenantId, OodRole role)
        : base($"An active watch already exists for tenant {tenantId.Value} / role {role}: {existingWatchId.Value}")
    {
        ExistingWatchId = existingWatchId;
        TenantId = tenantId;
        Role = role;
    }

    /// <summary>The Active watch that would be displaced.</summary>
    public OodWatchId ExistingWatchId { get; }

    /// <summary>Tenant scope of the conflict.</summary>
    public TenantId TenantId { get; }

    /// <summary>OOD role with the existing Active watch.</summary>
    public OodRole Role { get; }
}
