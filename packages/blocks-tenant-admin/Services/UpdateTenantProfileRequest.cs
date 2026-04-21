using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TenantAdmin.Services;

/// <summary>
/// Payload for <see cref="ITenantAdminService.UpdateTenantProfileAsync"/>.
/// Null fields on this request are treated as "leave unchanged". If the tenant
/// has no existing profile, an update request creates one (set <see cref="DisplayName"/>).
/// </summary>
public sealed record UpdateTenantProfileRequest
{
    /// <summary>The tenant whose profile is being updated.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>New display name, or null to leave unchanged.</summary>
    public string? DisplayName { get; init; }

    /// <summary>New contact email, or null to leave unchanged.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>New contact phone, or null to leave unchanged.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>New primary bundle key, or null to leave unchanged.</summary>
    public string? BundleKey { get; init; }
}
