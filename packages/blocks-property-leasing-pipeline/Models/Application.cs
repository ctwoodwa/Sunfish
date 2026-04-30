using Sunfish.Blocks.PublicListings.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>
/// Rental application — capability-tier-2 promotion from
/// <see cref="Prospect"/>. FHA-defense layout per ADR 0057: protected-class
/// fields are quarantined to <see cref="Demographics"/>; only
/// <see cref="Facts"/> is visible to
/// <see cref="Services.IApplicationDecisioner"/>.
/// </summary>
public sealed record Application
{
    /// <summary>Unique identifier for this application.</summary>
    public required ApplicationId Id { get; init; }

    /// <summary>Owning tenant.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>The prospect submitting this application.</summary>
    public required ProspectId Prospect { get; init; }

    /// <summary>The listing being applied for.</summary>
    public required PublicListingId Listing { get; init; }

    /// <summary>
    /// Income, credit, eviction history, references — the only fields
    /// <see cref="Services.IApplicationDecisioner"/> consumes.
    /// </summary>
    public required DecisioningFacts Facts { get; init; }

    /// <summary>
    /// Protected-class data quarantined for HUD reporting + civil-rights
    /// compliance. Never passed to decisioning logic; encrypted at rest
    /// per ADR 0046 <c>EncryptedField</c> in Phase 3.
    /// </summary>
    public required DemographicProfile Demographics { get; init; }

    /// <summary>Current lifecycle status.</summary>
    public required ApplicationStatus Status { get; init; }

    /// <summary>Reference to the applicant's signature on the application form (ADR 0054).</summary>
    public required SignatureEventRef ApplicationSignature { get; init; }

    /// <summary>Application fee collected at submission (ADR 0051; routed via <c>IPaymentGateway</c>).</summary>
    public required Money ApplicationFee { get; init; }

    /// <summary>UTC timestamp of submission.</summary>
    public required DateTimeOffset SubmittedAt { get; init; }

    /// <summary>UTC timestamp the operator made an Accept/Decline decision; null while undecided.</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Operator who recorded the decision; null while undecided.</summary>
    public ActorId? DecidedBy { get; init; }
}
