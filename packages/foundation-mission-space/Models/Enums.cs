namespace Sunfish.Foundation.MissionSpace;

/// <summary>The 10 mission-envelope dimensions per ADR 0062-A1.2.</summary>
public enum DimensionChangeKind
{
    Hardware,
    User,
    Regulatory,
    Runtime,
    FormFactor,
    Edition,
    Network,
    TrustAnchor,
    SyncState,
    VersionVector,
}

/// <summary>Severity of an <see cref="EnvelopeChange"/> per A1.10.</summary>
public enum EnvelopeChangeSeverity
{
    Informational,
    Warning,
    Critical,

    /// <summary>Per A1.10 — the probe is unreliable; change is suspect.</summary>
    ProbeUnreliable,
}

/// <summary>Per A1.2 — the verdict an <see cref="IFeatureGate{TFeature}"/> renders.</summary>
public enum FeatureAvailabilityState
{
    Available,
    DegradedAvailable,
    Unavailable,
}

/// <summary>5-value taxonomy per A1.2.</summary>
public enum DegradationKind
{
    ReadOnly,
    ReducedSurface,
    PerformanceLimited,
    PartiallyHidden,
    AdvisoryCaveat,
}

/// <summary>Per A1.10 — the health of an <see cref="IDimensionProbe{TDimension}"/>.</summary>
public enum ProbeStatus
{
    Healthy,
    Stale,
    Failed,
    PartiallyDegraded,
    Unreachable,
}

/// <summary>Per A1.6 — cost class drives wall-clock timeout + cache TTL.</summary>
public enum ProbeCostClass
{
    Low,
    Medium,
    High,
    DeepHigh,
    Live,
}

/// <summary>Per A1.9 — operator force-enable policy per dimension.</summary>
public enum ForceEnablePolicy
{
    NotOverridable,
    OverridableWithCaveat,
    Overridable,
}
