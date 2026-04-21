using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.TenantAdmin.Models;

/// <summary>
/// Tenant profile record. One profile per <see cref="TenantId"/> —
/// stores the human-friendly presentation data for a tenant plus the
/// bundle key (if any) the tenant has activated as its primary business case.
/// </summary>
public sealed record TenantProfile : IMustHaveTenant
{
    /// <summary>The tenant this profile belongs to.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Human-readable display name for the tenant.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Primary contact email.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Primary contact phone, free-form.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>UTC timestamp when this profile was created.</summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>Optional bundle key currently primary for this tenant.</summary>
    public string? BundleKey { get; init; }
}
