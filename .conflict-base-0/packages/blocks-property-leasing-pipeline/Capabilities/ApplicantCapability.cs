using Sunfish.Blocks.PropertyLeasingPipeline.Models;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Capabilities;

/// <summary>
/// Capability-tier-2 credential issued to a <see cref="Prospect"/> after
/// the application fee is paid + the application is signed (per ADR 0043
/// addendum). Phase 2 ships the shape; Phase 3 wires macaroon issuance
/// once the FCRA workflow + signature substrate are in place.
/// </summary>
public sealed record ApplicantCapability
{
    /// <summary>Stable identifier for this capability.</summary>
    public required ApplicantCapabilityId Id { get; init; }

    /// <summary>Macaroon-encoded bearer token (ADR 0032).</summary>
    public required string Token { get; init; }

    /// <summary>The application this capability is bound to.</summary>
    public required ApplicationId Application { get; init; }

    /// <summary>UTC timestamp the capability was minted.</summary>
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>UTC expiry; typically the lease-decision window per ADR 0057.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>Identifier for an <see cref="ApplicantCapability"/>.</summary>
public readonly record struct ApplicantCapabilityId(Guid Value);
