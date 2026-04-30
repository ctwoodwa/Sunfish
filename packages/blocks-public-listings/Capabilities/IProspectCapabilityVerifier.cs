using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.PublicListings.Capabilities;

/// <summary>
/// Verifies a Prospect-tier macaroon token issued by
/// <see cref="MacaroonCapabilityPromoter"/> (W#28 Phase 5c-4 per ADR 0059
/// + the Phase 5c-4 unblock addendum). Returns the projected capability or
/// throws <see cref="ProspectCapabilityDeniedException"/> with a specific
/// reason — matches the both-or-neither shape of W#32's
/// <see cref="Sunfish.Foundation.Recovery.Crypto.IFieldDecryptor"/>.
/// </summary>
public interface IProspectCapabilityVerifier
{
    /// <summary>
    /// Decode <paramref name="tokenBase64Url"/>, verify the macaroon
    /// signature chain against the issuer's root key, and check the
    /// Sunfish-specific caveats (tenant, listing-allowed, email-verified,
    /// expires). Returns the verified capability on success.
    /// </summary>
    /// <exception cref="ProspectCapabilityDeniedException">
    /// Decode failed; signature mismatch; tenant caveat doesn't match
    /// <paramref name="requestingTenant"/>; <paramref name="requestedListing"/>
    /// not in the allowed-listings caveat set; email not verified;
    /// capability expired at <paramref name="now"/>.
    /// </exception>
    Task<VerifiedProspectCapability> VerifyAsync(
        string tokenBase64Url,
        TenantId requestingTenant,
        PublicListingId requestedListing,
        DateTimeOffset now,
        CancellationToken ct);
}

/// <summary>
/// Verified projection of a Prospect-tier capability returned by
/// <see cref="IProspectCapabilityVerifier.VerifyAsync"/>. Distinct from
/// the Phase 4 <see cref="ProspectCapability"/> (issuer-side) — that
/// record carries the macaroon-token wire form; this record is the
/// post-verification projection where the token has been consumed and
/// the caveats have been parsed.
/// </summary>
public sealed record VerifiedProspectCapability
{
    public required Guid CapabilityId { get; init; }
    public required TenantId Tenant { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    public required IReadOnlyList<PublicListingId> AllowedListings { get; init; }
}

/// <summary>
/// Thrown when a Prospect-capability token fails verification. The
/// reason string is audit-grade and may be surfaced to the caller —
/// the macaroon's signature gate prevents replay-as-error-oracle abuse.
/// </summary>
public sealed class ProspectCapabilityDeniedException : Exception
{
    public ProspectCapabilityDeniedException(string capabilityIdOrToken, string reason)
        : base($"Prospect capability denied for {capabilityIdOrToken}: {reason}")
    {
        CapabilityIdOrToken = capabilityIdOrToken;
        Reason = reason;
    }

    public string CapabilityIdOrToken { get; }
    public string Reason { get; }
}
