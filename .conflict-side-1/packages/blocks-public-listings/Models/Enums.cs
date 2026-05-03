namespace Sunfish.Blocks.PublicListings.Models;

/// <summary>Lifecycle status of a public listing.</summary>
public enum PublicListingStatus
{
    /// <summary>Listing is being authored; not yet visible publicly.</summary>
    Draft,

    /// <summary>Listing is live and visible to anonymous browsers (subject to redaction policy).</summary>
    Published,

    /// <summary>Listing was removed from public view.</summary>
    Unlisted
}

/// <summary>How precisely the listing's address is rendered to the viewer.</summary>
public enum AddressRedactionLevel
{
    /// <summary>Anonymous tier: e.g., "West End" or "Block 1200, Main Street".</summary>
    NeighborhoodOnly,

    /// <summary>Prospect tier: e.g., "1200 block of Main St".</summary>
    BlockNumber,

    /// <summary>Applicant tier: full street address.</summary>
    FullAddress
}

/// <summary>Capability tier of the viewer; drives per-tier redaction.</summary>
public enum RedactionTier
{
    /// <summary>No verification; minimum data.</summary>
    Anonymous,

    /// <summary>Email-verified prospect; intermediate data.</summary>
    Prospect,

    /// <summary>Submitted application; full data.</summary>
    Applicant
}

/// <summary>How showings are scheduled for the listing.</summary>
public enum ShowingAvailabilityKind
{
    /// <summary>One or more open-house slots; viewers drop in.</summary>
    OpenHouse,

    /// <summary>By appointment; uses ADR 0057's appointment-scheduling surface.</summary>
    ByAppointment,

    /// <summary>Self-guided tour via smart-lock access (per ADR 0057).</summary>
    SelfGuidedSmartLock
}
