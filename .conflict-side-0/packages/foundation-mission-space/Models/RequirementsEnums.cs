namespace Sunfish.Foundation.MissionSpace;

/// <summary>
/// Per ADR 0063-A1.1 — bundle-author intent for the
/// <see cref="MinimumSpec"/> at install-time UX.
/// </summary>
public enum SpecPolicy
{
    /// <summary>Install is blocked when the spec doesn't match.</summary>
    Required,

    /// <summary>Install proceeds with a UX warning when the spec doesn't match.</summary>
    Recommended,

    /// <summary>Spec is shown to the operator but does not gate install (per A1.8 explicit Informational rule).</summary>
    Informational,
}

/// <summary>Per A1.8 — overall install verdict from a <see cref="SystemRequirementsResult"/>.</summary>
public enum OverallVerdict
{
    /// <summary>All Required dimensions pass.</summary>
    Pass,

    /// <summary>At least one Recommended dimension fails (operator gets a warning) but no Required dimension fails.</summary>
    WarnOnly,

    /// <summary>At least one Required dimension fails (install blocked unless force-enabled per A1.11).</summary>
    Block,
}

/// <summary>Per A1.1 — per-dimension policy classifier on a <see cref="DimensionEvaluation"/>.</summary>
public enum DimensionPolicyKind
{
    Required,
    Recommended,
    Informational,
    Unevaluated,
}

/// <summary>Per A1.1 — per-dimension pass/fail outcome on a <see cref="DimensionEvaluation"/>.</summary>
public enum DimensionPassFail
{
    Pass,
    Fail,
    Unevaluated,
}

/// <summary>Per A1.1 — render-mode hint for the per-platform <c>ISystemRequirementsRenderer</c> consumer.</summary>
public enum SystemRequirementsRenderMode
{
    PreInstallFullPage,
    PostInstallInlineExplanation,
    PostInstallRegressionBanner,
}
