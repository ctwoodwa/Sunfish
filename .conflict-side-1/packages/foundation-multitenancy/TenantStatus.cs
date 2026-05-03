using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MultiTenancy;

/// <summary>Lifecycle status of a tenant in the catalog.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TenantStatus
{
    /// <summary>Tenant is provisioned and serving traffic.</summary>
    Active = 0,

    /// <summary>Tenant is registered but temporarily not servicing requests.</summary>
    Suspended = 1,

    /// <summary>Tenant is in the process of being deactivated; data retained.</summary>
    Decommissioning = 2,

    /// <summary>Tenant has been fully deactivated; only read-only historical access allowed.</summary>
    Archived = 3,
}
