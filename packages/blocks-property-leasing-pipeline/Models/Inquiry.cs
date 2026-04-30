using System.Net;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Public-facing inquiry on a listing — submitted by an Anonymous viewer
/// before email verification + capability promotion. The route-local
/// form-post primitive (defined in <c>accelerators/bridge</c> per ADR
/// 0059 amendment A1) is mapped to this domain shape at the Bridge
/// controller boundary.
/// </summary>
/// <remarks>
/// FHA-defense note: <see cref="MessageBody"/> is free-text; no
/// structured demographic fields are captured at the inquiry boundary.
/// The protected-class quarantine (see <see cref="DemographicProfile"/>)
/// only applies once the prospect has been promoted to
/// <see cref="Application"/>.
/// </remarks>
public sealed record Inquiry
{
    /// <summary>Unique identifier for this inquiry.</summary>
    public required InquiryId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The listing the prospect is inquiring about.</summary>
    public required PublicListingId Listing { get; init; }

    /// <summary>Prospect's name as entered on the inquiry form.</summary>
    public required string ProspectName { get; init; }

    /// <summary>Prospect's email — used to drive the verification flow that promotes Anonymous → Prospect.</summary>
    public required string ProspectEmail { get; init; }

    /// <summary>Optional phone number for SMS callbacks.</summary>
    public string? ProspectPhone { get; init; }

    /// <summary>Free-text message body from the inquiry form.</summary>
    public required string MessageBody { get; init; }

    /// <summary>Source IP captured by the Bridge route at submission.</summary>
    public required IPAddress ClientIp { get; init; }

    /// <summary>User-agent header captured by the Bridge route at submission.</summary>
    public required string UserAgent { get; init; }

    /// <summary>UTC timestamp of submission.</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required InquiryStatus Status { get; init; }
}
