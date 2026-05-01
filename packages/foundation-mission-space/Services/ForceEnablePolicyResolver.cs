using System;

namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per-dimension <see cref="ForceEnablePolicy"/> table per ADR
/// 0062-A1.9. Hardware + Runtime are <see cref="ForceEnablePolicy.NotOverridable"/>
/// (operator force-enable throws). Regulatory + Edition are
/// <see cref="ForceEnablePolicy.OverridableWithCaveat"/> (force-enable
/// allowed but verdict surfaces as <see cref="FeatureAvailabilityState.DegradedAvailable"/>
/// with <see cref="DegradationKind.AdvisoryCaveat"/>). The remaining
/// 6 dimensions are freely <see cref="ForceEnablePolicy.Overridable"/>.
/// </summary>
public static class ForceEnablePolicyResolver
{
    /// <summary>Per-dimension policy lookup per A1.9.</summary>
    public static ForceEnablePolicy ResolveFor(DimensionChangeKind dimension) => dimension switch
    {
        DimensionChangeKind.Hardware => ForceEnablePolicy.NotOverridable,
        DimensionChangeKind.Runtime => ForceEnablePolicy.NotOverridable,
        DimensionChangeKind.Regulatory => ForceEnablePolicy.OverridableWithCaveat,
        DimensionChangeKind.Edition => ForceEnablePolicy.OverridableWithCaveat,
        DimensionChangeKind.User => ForceEnablePolicy.Overridable,
        DimensionChangeKind.Network => ForceEnablePolicy.Overridable,
        DimensionChangeKind.TrustAnchor => ForceEnablePolicy.Overridable,
        DimensionChangeKind.SyncState => ForceEnablePolicy.Overridable,
        DimensionChangeKind.VersionVector => ForceEnablePolicy.Overridable,
        DimensionChangeKind.FormFactor => ForceEnablePolicy.Overridable,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown dimension."),
    };

    /// <summary>Convenience: true iff <paramref name="dimension"/> permits operator force-enable per A1.9.</summary>
    public static bool IsForceEnablePermitted(DimensionChangeKind dimension) =>
        ResolveFor(dimension) != ForceEnablePolicy.NotOverridable;
}
