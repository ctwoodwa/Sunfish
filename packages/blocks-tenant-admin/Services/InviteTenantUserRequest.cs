using Sunfish.Blocks.TenantAdmin.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.TenantAdmin.Services;

/// <summary>
/// Payload for <see cref="ITenantAdminService.InviteTenantUserAsync"/>.
/// </summary>
public sealed record InviteTenantUserRequest
{
    /// <summary>The tenant issuing the invitation.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Invitee's email address.</summary>
    public required string Email { get; init; }

    /// <summary>Optional display name.</summary>
    public string? DisplayName { get; init; }

    /// <summary>Role granted on acceptance. Defaults to <see cref="TenantRole.Member"/>.</summary>
    public TenantRole Role { get; init; } = TenantRole.Member;
}
