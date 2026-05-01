namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>Per ADR 0064-A1.5 — composite-confidence band from a <c>JurisdictionProbe</c>.</summary>
public enum Confidence
{
    High,
    Medium,
    Low,
}

/// <summary>Per A1.6 — what kind of policy evaluation a rule performs.</summary>
public enum PolicyEvaluationKind
{
    DataResidencyConstraint,
    DataExportConstraint,
    UserConsentRequirement,
    AutomatedDecisionGate,
    SanctionsScreening,
    FeatureAvailabilityGate,
    NotificationRequirement,
}

/// <summary>Per A1.6 — what action a policy violation triggers.</summary>
public enum PolicyEnforcementAction
{
    Block,
    BlockWithExplanation,
    ReadOnly,
    AuditOnly,
    PromptUserConsent,
    OperatorOverridable,
}

/// <summary>Per A1.6 — outcome of a single policy evaluation.</summary>
public enum PolicyVerdictState
{
    Pass,
    FailWithEnforcement,
    FailAuditOnly,
    IndeterminateProbeFailure,
}

/// <summary>Per A1.13 — the regulatory regimes Sunfish takes a stance on.</summary>
public enum RegulatoryRegime
{
    HIPAA,
    GDPR,
    PCI_DSS_v4,
    SOC2,
    EU_AI_Act,
    FHA,
    CCPA,
    Other,
}

/// <summary>Per A1.13 — Sunfish's stance toward each regime; PCI-DSS reframed to ExplicitlyDisclaimedOpenSource.</summary>
public enum RegulatoryRegimeStance
{
    InScope,
    ExplicitlyDisclaimedOpenSource,
    CommercialProductOnly,
}

/// <summary>Per A1.3 — sanctions screener policy; AdvisoryOnly opts out of blocking enforcement.</summary>
public enum ScreeningPolicy
{
    Default,
    AdvisoryOnly,
}
