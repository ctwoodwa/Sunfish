using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Sunfish.Foundation.MissionSpace.Regulatory;

/// <summary>
/// Composite-confidence jurisdiction probe result per ADR 0064-A1.4 + A1.5.
/// Serializes via <see cref="Sunfish.Foundation.Crypto.CanonicalJson.Serialize"/>
/// (camelCase keys per ADR 0028-A7.8).
/// </summary>
public sealed record JurisdictionProbe
{
    [JsonPropertyName("jurisdictionCode")]
    public required string JurisdictionCode { get; init; }

    [JsonPropertyName("confidence")]
    [JsonConverter(typeof(JsonStringEnumConverter<Confidence>))]
    public required Confidence Confidence { get; init; }

    /// <summary>Free-form signal sources (e.g., <c>"ip-geo"</c>, <c>"user-declaration"</c>, <c>"tenant-config"</c>).</summary>
    [JsonPropertyName("signalSources")]
    public IReadOnlyList<string> SignalSources { get; init; } = Array.Empty<string>();

    [JsonPropertyName("probedAt")]
    public required DateTimeOffset ProbedAt { get; init; }
}

/// <summary>
/// A single jurisdictional policy rule per A1.6. Phase 1 substrate ships the
/// type; rule content is Phase 3 (counsel-engagement-gated).
/// </summary>
public sealed record JurisdictionalPolicyRule
{
    [JsonPropertyName("ruleId")]
    public required string RuleId { get; init; }

    [JsonPropertyName("regime")]
    [JsonConverter(typeof(JsonStringEnumConverter<RegulatoryRegime>))]
    public required RegulatoryRegime Regime { get; init; }

    [JsonPropertyName("evaluationKind")]
    [JsonConverter(typeof(JsonStringEnumConverter<PolicyEvaluationKind>))]
    public required PolicyEvaluationKind EvaluationKind { get; init; }

    [JsonPropertyName("enforcementAction")]
    [JsonConverter(typeof(JsonStringEnumConverter<PolicyEnforcementAction>))]
    public required PolicyEnforcementAction EnforcementAction { get; init; }

    /// <summary>Per A1.6 — feature-keys this rule applies to (nullable = applies to all features).</summary>
    [JsonPropertyName("relevantFeatures")]
    public IReadOnlySet<string>? RelevantFeatures { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("ruleVersion")]
    public required string RuleVersion { get; init; }
}

/// <summary>Per A1.6 — verdict from <see cref="JurisdictionalPolicyRule"/> evaluation.</summary>
public sealed record PolicyVerdict
{
    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter<PolicyVerdictState>))]
    public required PolicyVerdictState State { get; init; }

    [JsonPropertyName("evaluations")]
    public IReadOnlyList<PolicyRuleEvaluation> Evaluations { get; init; } = Array.Empty<PolicyRuleEvaluation>();

    [JsonPropertyName("evaluatedAt")]
    public required DateTimeOffset EvaluatedAt { get; init; }
}

/// <summary>Per A1.6 — one rule's contribution to a <see cref="PolicyVerdict"/>.</summary>
public sealed record PolicyRuleEvaluation
{
    [JsonPropertyName("ruleId")]
    public required string RuleId { get; init; }

    [JsonPropertyName("state")]
    [JsonConverter(typeof(JsonStringEnumConverter<PolicyVerdictState>))]
    public required PolicyVerdictState State { get; init; }

    [JsonPropertyName("enforcementAction")]
    [JsonConverter(typeof(JsonStringEnumConverter<PolicyEnforcementAction>))]
    public PolicyEnforcementAction? EnforcementAction { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

/// <summary>Per A1.6 + A1.13 — Sunfish's acknowledgment of how it relates to a given regime.</summary>
public sealed record RegimeAcknowledgment
{
    [JsonPropertyName("regime")]
    [JsonConverter(typeof(JsonStringEnumConverter<RegulatoryRegime>))]
    public required RegulatoryRegime Regime { get; init; }

    [JsonPropertyName("stance")]
    [JsonConverter(typeof(JsonStringEnumConverter<RegulatoryRegimeStance>))]
    public required RegulatoryRegimeStance Stance { get; init; }

    /// <summary>Localization-key for the human-readable rationale; resolved by the host.</summary>
    [JsonPropertyName("rationaleKey")]
    public required string RationaleKey { get; init; }
}

/// <summary>Per A1.6 — a data-residency constraint on a record class.</summary>
public sealed record DataResidencyConstraint
{
    [JsonPropertyName("recordClass")]
    public required string RecordClass { get; init; }

    [JsonPropertyName("allowedJurisdictions")]
    public IReadOnlyList<string> AllowedJurisdictions { get; init; } = Array.Empty<string>();

    [JsonPropertyName("prohibitedJurisdictions")]
    public IReadOnlyList<string> ProhibitedJurisdictions { get; init; } = Array.Empty<string>();
}

/// <summary>Per A1.6 — verdict from <see cref="DataResidencyConstraint"/> evaluation.</summary>
public sealed record EnforcementVerdict
{
    [JsonPropertyName("isPermitted")]
    public required bool IsPermitted { get; init; }

    [JsonPropertyName("violatedConstraintId")]
    public string? ViolatedConstraintId { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }
}

/// <summary>Per A1.6 — sanctions screener result.</summary>
public sealed record SanctionsScreeningResult
{
    [JsonPropertyName("subjectId")]
    public required string SubjectId { get; init; }

    [JsonPropertyName("hits")]
    public IReadOnlyList<SanctionsListEntry> Hits { get; init; } = Array.Empty<SanctionsListEntry>();

    [JsonPropertyName("policy")]
    [JsonConverter(typeof(JsonStringEnumConverter<ScreeningPolicy>))]
    public required ScreeningPolicy Policy { get; init; }

    [JsonPropertyName("screenedAt")]
    public required DateTimeOffset ScreenedAt { get; init; }
}

/// <summary>Per A1.6 — a single sanctions-list match.</summary>
public sealed record SanctionsListEntry
{
    [JsonPropertyName("listSource")]
    public required string ListSource { get; init; }

    [JsonPropertyName("matchedName")]
    public required string MatchedName { get; init; }

    [JsonPropertyName("matchScore")]
    public required double MatchScore { get; init; }

    [JsonPropertyName("listVersion")]
    public required string ListVersion { get; init; }
}

/// <summary>Per A1.6 — placeholder EU AI Act tier classification (Phase 1 carries the type only).</summary>
public sealed record EuAiActTierClassification
{
    [JsonPropertyName("featureKey")]
    public required string FeatureKey { get; init; }

    [JsonPropertyName("tier")]
    public required string Tier { get; init; }

    [JsonPropertyName("classifiedAt")]
    public required DateTimeOffset ClassifiedAt { get; init; }
}
