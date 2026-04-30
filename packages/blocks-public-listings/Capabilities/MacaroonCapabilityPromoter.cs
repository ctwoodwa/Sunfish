using System.Net;
using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Macaroons;

namespace Sunfish.Blocks.PublicListings.Capabilities;

/// <summary>
/// Macaroon-backed <see cref="ICapabilityPromoter"/>. Mints a macaroon at
/// the configured location with caveats binding the capability to the
/// issuing tenant, the accessible-listings set, the verified email, and a
/// TTL (default 7 days per ADR 0059).
/// </summary>
public sealed class MacaroonCapabilityPromoter : ICapabilityPromoter
{
    /// <summary>Default location string for Sunfish public-listing capabilities.</summary>
    public const string DefaultLocation = "sunfish/public-listings";

    /// <summary>Default capability TTL — 7 days per ADR 0059.</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromDays(7);

    private readonly IMacaroonIssuer _issuer;
    private readonly TenantId _tenant;
    private readonly IReadOnlyList<PublicListingId> _accessibleListings;
    private readonly string _location;
    private readonly TimeSpan _ttl;
    private readonly TimeProvider _time;

    /// <summary>
    /// Creates a promoter that mints macaroons at <paramref name="location"/> via
    /// <paramref name="issuer"/>, bound to <paramref name="tenant"/> and the
    /// <paramref name="accessibleListings"/> set, with a <paramref name="ttl"/>
    /// expiry. <paramref name="time"/> defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    public MacaroonCapabilityPromoter(
        IMacaroonIssuer issuer,
        TenantId tenant,
        IReadOnlyList<PublicListingId> accessibleListings,
        string? location = null,
        TimeSpan? ttl = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(accessibleListings);
        if (tenant == default)
        {
            throw new ArgumentException("Tenant is required.", nameof(tenant));
        }
        _issuer = issuer;
        _tenant = tenant;
        _accessibleListings = accessibleListings;
        _location = location ?? DefaultLocation;
        _ttl = ttl ?? DefaultTtl;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ProspectCapability> PromoteToProspectAsync(string verifiedEmail, IPAddress ipAddress, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(verifiedEmail);
        ArgumentNullException.ThrowIfNull(ipAddress);

        var capabilityId = new ProspectCapabilityId(Guid.NewGuid());
        var issuedAt = _time.GetUtcNow();
        var expiresAt = issuedAt + _ttl;

        var caveats = new List<Caveat>
        {
            new($"capability-id = {capabilityId.Value:D}"),
            new($"tenant = {_tenant.Value}"),
            new($"email = {verifiedEmail}"),
            new("email-verified = true"),
            new($"issued-from-ip = {ipAddress}"),
            new($"expires = {expiresAt:O}"),
        };

        foreach (var listingId in _accessibleListings)
        {
            caveats.Add(new($"listing-allowed = {listingId.Value:D}"));
        }

        var macaroon = await _issuer.MintAsync(_location, capabilityId.Value.ToString("D"), caveats, ct).ConfigureAwait(false);
        var token = MacaroonCodec.EncodeBase64Url(macaroon);

        return new ProspectCapability
        {
            Id = capabilityId,
            MacaroonToken = token,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            AccessibleListings = _accessibleListings,
        };
    }
}
