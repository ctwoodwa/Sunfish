namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>Lifecycle state of an <see cref="Inquiry"/>.</summary>
public enum InquiryStatus
{
    /// <summary>Submitted but the prospect has not yet email-verified.</summary>
    Submitted,

    /// <summary>Email verified; capability promoted to <see cref="Prospect"/>.</summary>
    PromotedToProspect,

    /// <summary>Inquiry was withdrawn before promotion (terminal).</summary>
    Withdrawn,
}

/// <summary>Lifecycle state of an <see cref="Application"/>.</summary>
public enum ApplicationStatus
{
    /// <summary>Application form submitted; payment + signature pending.</summary>
    Submitted,

    /// <summary>Payment + signature confirmed; background check kicking off.</summary>
    AwaitingBackgroundCheck,

    /// <summary>Background check returned; awaiting operator decision.</summary>
    AwaitingDecision,

    /// <summary>Operator accepted; <see cref="LeaseOffer"/> can be issued.</summary>
    Accepted,

    /// <summary>Operator declined; <see cref="AdverseActionNotice"/> required if BG-check finding cited.</summary>
    Declined,

    /// <summary>Applicant withdrew before decision (terminal).</summary>
    Withdrawn,
}

/// <summary>Outcome of a <see cref="BackgroundCheckResult"/> per FCRA conventions.</summary>
public enum BackgroundCheckOutcome
{
    /// <summary>No adverse findings; safe to proceed to decision on non-BG factors.</summary>
    Clear,

    /// <summary>One or more findings present; operator must consider per FCRA §615.</summary>
    HasFindings,

    /// <summary>Provider returned an error; retry or manual review.</summary>
    Error,
}

/// <summary>Lifecycle state of a <see cref="LeaseOffer"/>.</summary>
public enum LeaseOfferStatus
{
    /// <summary>Offer minted; awaiting prospect acceptance.</summary>
    Issued,

    /// <summary>Prospect accepted; <c>blocks-leases</c> Lease.Draft created.</summary>
    Accepted,

    /// <summary>Prospect declined the offer.</summary>
    Declined,

    /// <summary>Offer expired without prospect action.</summary>
    Expired,
}
