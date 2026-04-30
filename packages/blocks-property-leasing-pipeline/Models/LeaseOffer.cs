using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Pre-lease offer issued after an <see cref="Application"/> reaches
/// <see cref="ApplicationStatus.Accepted"/>. Phase 1 ships only the
/// entity shape; Phase 6 wires the boundary call into <c>blocks-leases</c>
/// (<c>ILeaseService.CreateFromOfferAsync</c>) on
/// <see cref="LeaseOfferStatus.Accepted"/>.
/// </summary>
public sealed record LeaseOffer
{
    /// <summary>Unique identifier for this offer.</summary>
    public required LeaseOfferId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The application that ripened into this offer.</summary>
    public required ApplicationId Application { get; init; }

    /// <summary>Proposed monthly rent.</summary>
    public required Money MonthlyRent { get; init; }

    /// <summary>Required security deposit.</summary>
    public required Money SecurityDeposit { get; init; }

    /// <summary>Proposed lease term start date.</summary>
    public required DateOnly TermStart { get; init; }

    /// <summary>Proposed lease term end date.</summary>
    public required DateOnly TermEnd { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required LeaseOfferStatus Status { get; init; }

    /// <summary>UTC timestamp the offer expires if not acted on.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>UTC timestamp of issuance.</summary>
    public required DateTimeOffset IssuedAt { get; init; }
}
