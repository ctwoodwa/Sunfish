using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TenantAdmin.Services;

/// <summary>
/// Payload for <see cref="ITenantAdminService.ActivateBundleAsync"/>.
/// </summary>
public sealed record ActivateBundleRequest
{
    /// <summary>The tenant activating the bundle.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Bundle key from <c>BusinessCaseBundleManifest.Key</c>.</summary>
    public required string BundleKey { get; init; }

    /// <summary>Edition key from the bundle's <c>EditionMappings</c>.</summary>
    public required string Edition { get; init; }
}
