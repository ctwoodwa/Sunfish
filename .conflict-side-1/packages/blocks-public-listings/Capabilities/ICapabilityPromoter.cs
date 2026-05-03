using System.Net;
using Sunfish.Blocks.PublicListings.Models;

namespace Sunfish.Blocks.PublicListings.Capabilities;

/// <summary>
/// Promotes a viewer from Anonymous → Prospect after email verification
/// (per ADR 0059 §"Capability promotion (ADR 0043 addendum)" + ADR 0032
/// macaroon substrate).
/// </summary>
/// <remarks>
/// The verification flow itself (issue email link → user clicks → token
/// consumed) is upstream of this contract — when
/// <see cref="PromoteToProspectAsync(string, IPAddress, CancellationToken)"/>
/// is called, the email is already proven via that flow.
/// </remarks>
public interface ICapabilityPromoter
{
    /// <summary>
    /// Mints a Prospect capability bound to <paramref name="verifiedEmail"/>.
    /// The returned <see cref="ProspectCapability"/> wraps an ADR 0032
    /// macaroon with caveats restricting it to the issuing tenant + the
    /// listed-accessible listings + the email-verified=true assertion +
    /// a TTL.
    /// </summary>
    /// <param name="verifiedEmail">Email address proven via upstream verification.</param>
    /// <param name="ipAddress">Source IP of the verification consumer (audit attribution).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ProspectCapability> PromoteToProspectAsync(string verifiedEmail, IPAddress ipAddress, CancellationToken ct);
}

/// <summary>
/// Bearer credential issued to an email-verified prospect. Carries an
/// ADR 0032 macaroon (serialised as <see cref="MacaroonToken"/>) plus
/// the metadata needed for renderer-tier decisions.
/// </summary>
public sealed record ProspectCapability
{
    /// <summary>Stable identifier for this capability (also embedded as a macaroon caveat).</summary>
    public required ProspectCapabilityId Id { get; init; }

    /// <summary>Serialised macaroon (ADR 0032 wire format).</summary>
    public required string MacaroonToken { get; init; }

    /// <summary>UTC timestamp the capability was minted.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>UTC expiry; typically <c>IssuedAt + 7 days</c> per ADR 0059.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>The listings this prospect is permitted to view at Prospect tier.</summary>
    public required IReadOnlyList<PublicListingId> AccessibleListings { get; init; }
}

/// <summary>Identifier for a <see cref="ProspectCapability"/>.</summary>
public readonly record struct ProspectCapabilityId(Guid Value);
