using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Blocks.PublicListings.Capabilities;

/// <summary>
/// Reference <see cref="IProspectCapabilityVerifier"/> per the W#28
/// Phase 5c-4 unblock addendum. Pairs with
/// <see cref="MacaroonCapabilityPromoter"/>: the promoter writes
/// Sunfish-specific caveats (<c>tenant = X</c>, <c>listing-allowed = Y</c>,
/// etc.) which fall outside the foundation
/// <see cref="FirstPartyCaveatParser"/> grammar; this verifier therefore
/// performs signature-chain verification inline (via
/// <see cref="MacaroonCodec.ComputeChain"/> + <see cref="IRootKeyStore"/>)
/// and parses every caveat block-locally rather than delegating to the
/// foundation <see cref="IMacaroonVerifier"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why bypass <see cref="IMacaroonVerifier"/>:</b> the foundation
/// verifier evaluates every caveat through <see cref="FirstPartyCaveatParser"/>,
/// which fails closed on unknown predicates. The Phase 4 promoter's
/// caveats use Sunfish-specific keys that the foundation parser doesn't
/// recognise — calling the generic verifier on a promoter-minted
/// macaroon would always reject. Per the Phase 5c-4 addendum's
/// halt-condition #1 ("adjust the verifier's generic-evaluation call
/// shape; do NOT change the foundation interface"), this verifier
/// adapts in-build.
/// </para>
/// </remarks>
public sealed class MacaroonProspectCapabilityVerifier : IProspectCapabilityVerifier
{
    private readonly IRootKeyStore _keys;

    public MacaroonProspectCapabilityVerifier(IRootKeyStore keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys = keys;
    }

    public async Task<VerifiedProspectCapability> VerifyAsync(
        string tokenBase64Url,
        TenantId requestingTenant,
        PublicListingId requestedListing,
        DateTimeOffset now,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(tokenBase64Url);

        Macaroon macaroon;
        try
        {
            macaroon = MacaroonCodec.DecodeBase64Url(tokenBase64Url);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Denied(Truncate(tokenBase64Url), $"decode failed: {ex.Message}");
        }

        var rootKey = await _keys.GetRootKeyAsync(macaroon.Location, ct).ConfigureAwait(false);
        if (rootKey is null)
        {
            throw Denied(macaroon.Identifier, $"no-root-key for location '{macaroon.Location}'");
        }

        var expected = MacaroonCodec.ComputeChain(rootKey, macaroon.Identifier, macaroon.Caveats);
        if (macaroon.Signature is null
            || macaroon.Signature.Length != expected.Length
            || !CryptographicOperations.FixedTimeEquals(expected, macaroon.Signature))
        {
            throw Denied(macaroon.Identifier, "signature-mismatch");
        }

        var parsed = ParseCaveats(macaroon.Caveats, macaroon.Identifier);

        if (!parsed.Tenant.Equals(requestingTenant))
        {
            throw Denied(macaroon.Identifier,
                $"wrong-tenant: caveat={parsed.Tenant.Value}, requesting={requestingTenant.Value}");
        }

        if (!parsed.AllowedListings.Contains(requestedListing))
        {
            throw Denied(macaroon.Identifier,
                $"listing-not-in-allowed-set: requested={requestedListing.Value}");
        }

        if (!parsed.EmailVerified)
        {
            throw Denied(macaroon.Identifier, "email-not-verified");
        }

        if (now > parsed.ExpiresAt)
        {
            throw Denied(macaroon.Identifier,
                $"expired: caveat={parsed.ExpiresAt:O}, now={now:O}");
        }

        return new VerifiedProspectCapability
        {
            CapabilityId = parsed.CapabilityId,
            Tenant = parsed.Tenant,
            Email = parsed.Email,
            ExpiresAt = parsed.ExpiresAt,
            AllowedListings = parsed.AllowedListings,
        };
    }

    private static ProspectCapabilityDeniedException Denied(string capabilityIdOrToken, string reason)
        => new(capabilityIdOrToken, reason);

    private static string Truncate(string token)
        => token.Length <= 8 ? token : token[..8] + "...";

    private static ParsedCaveats ParseCaveats(IReadOnlyList<Caveat> caveats, string identifier)
    {
        Guid? capabilityId = null;
        TenantId? tenant = null;
        string? email = null;
        bool? emailVerified = null;
        DateTimeOffset? expiresAt = null;
        var allowedListings = new List<PublicListingId>();

        foreach (var c in caveats)
        {
            var (key, value) = SplitKeyValue(c.Predicate);
            if (key is null)
            {
                throw Denied(identifier, $"malformed-caveat: '{c.Predicate}'");
            }

            switch (key)
            {
                case ProspectCaveatNames.CapabilityId:
                    if (!Guid.TryParseExact(value, "D", out var capId))
                    {
                        throw Denied(identifier, $"capability-id not a guid: '{value}'");
                    }
                    capabilityId = capId;
                    break;

                case ProspectCaveatNames.Tenant:
                    tenant = new TenantId(value);
                    break;

                case ProspectCaveatNames.Email:
                    email = value;
                    break;

                case ProspectCaveatNames.EmailVerified:
                    emailVerified = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                    break;

                case ProspectCaveatNames.Expires:
                    if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedExpiry))
                    {
                        throw Denied(identifier, $"expires not iso8601: '{value}'");
                    }
                    expiresAt = parsedExpiry;
                    break;

                case ProspectCaveatNames.ListingAllowed:
                    if (!Guid.TryParseExact(value, "D", out var listingGuid))
                    {
                        throw Denied(identifier, $"listing-allowed not a guid: '{value}'");
                    }
                    allowedListings.Add(new PublicListingId(listingGuid));
                    break;

                case ProspectCaveatNames.IssuedFromIp:
                    // Recorded for forensics; not enforced at verify-time.
                    break;

                default:
                    throw Denied(identifier, $"unknown-caveat-key: '{key}'");
            }
        }

        if (capabilityId is null) throw Denied(identifier, "missing-caveat: capability-id");
        if (tenant is null) throw Denied(identifier, "missing-caveat: tenant");
        if (email is null) throw Denied(identifier, "missing-caveat: email");
        if (emailVerified is null) throw Denied(identifier, "missing-caveat: email-verified");
        if (expiresAt is null) throw Denied(identifier, "missing-caveat: expires");

        return new ParsedCaveats(
            capabilityId.Value,
            tenant.Value,
            email,
            emailVerified.Value,
            expiresAt.Value,
            allowedListings);
    }

    private static (string? Key, string Value) SplitKeyValue(string predicate)
    {
        var eq = predicate.IndexOf('=');
        if (eq < 0)
        {
            return (null, string.Empty);
        }
        var key = predicate[..eq].Trim();
        var value = predicate[(eq + 1)..].Trim();
        return (key, value);
    }

    private sealed record ParsedCaveats(
        Guid CapabilityId,
        TenantId Tenant,
        string Email,
        bool EmailVerified,
        DateTimeOffset ExpiresAt,
        IReadOnlyList<PublicListingId> AllowedListings);
}
