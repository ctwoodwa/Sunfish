namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Onboarding-flow state for a <see cref="Vendor"/> record per ADR 0058.
/// Drives the magic-link onboarding flow + W-9 capture sequence shipped
/// in W#18 Phases 4 + 5.
/// </summary>
public enum VendorOnboardingState
{
    /// <summary>Vendor record exists but the onboarding flow has not yet started.</summary>
    Pending,

    /// <summary>A W-9 magic-link has been issued; awaiting consumption.</summary>
    W9Requested,

    /// <summary>The vendor returned a W-9 document via the magic-link flow.</summary>
    W9Received,

    /// <summary>Onboarding complete; vendor is operationally usable.</summary>
    Active,

    /// <summary>Vendor was suspended (e.g., insurance lapse, complaint).</summary>
    Suspended,

    /// <summary>Vendor was retired and cannot receive new work orders (terminal).</summary>
    Retired,
}
