using System;
using System.Collections.Generic;
using Sunfish.Blocks.PropertyLeasingPipeline.Models;
using Sunfish.Foundation.Integrations.Payments;
using Sunfish.Foundation.Integrations.Signatures;

namespace Sunfish.Bridge.Listings;

/// <summary>
/// Route-local form-post primitive for W#28 Phase 5c-4 Slice C
/// (<c>POST /listings/criteria/{token}/start-application</c>) per
/// ADR 0059 amendment A1. The route maps this primitive to
/// <c>Sunfish.Blocks.PropertyLeasingPipeline.Services.SubmitApplicationRequest</c>
/// at the controller boundary; this DTO never crosses the block
/// boundary.
/// </summary>
/// <param name="ListingId">Target listing id (must be in the verified capability's <c>AllowedListings</c> set).</param>
/// <param name="Facts">Non-protected decisioning facts.</param>
/// <param name="Demographics">Protected-class fields collected from the form (plaintext); the leasing-pipeline service encrypts every non-null field at the boundary per W#22 Phase 9.</param>
/// <param name="ApplicationFee">Application fee per ADR 0051.</param>
/// <param name="SignatureEventId">Reference to the operator-witnessed application signature (ADR 0054).</param>
public sealed record StartApplicationFormPost(
    Guid ListingId,
    DecisioningFacts Facts,
    DemographicProfileSubmission Demographics,
    Money ApplicationFee,
    Guid SignatureEventId);
