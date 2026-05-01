using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Regulatory.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#39 Foundation.MissionSpace.Regulatory
/// audit-emission set (ADR 0064-A1.3 / A1.5 / A1.7 / A1.16). Mirrors the
/// <c>VersionVectorAuditPayloads</c> + <c>MissionSpaceAuditPayloads</c> conventions:
/// keys alphabetized; bodies opaque to the substrate.
/// </summary>
public static class RegulatoryAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.PolicyEvaluated"/>.</summary>
    public static AuditPayload PolicyEvaluated(
        string featureKey,
        string jurisdictionCode,
        int evaluationCount,
        string verdictState) =>
        new(new Dictionary<string, object?>
        {
            ["evaluation_count"] = evaluationCount,
            ["feature_key"] = featureKey,
            ["jurisdiction_code"] = jurisdictionCode,
            ["verdict_state"] = verdictState,
        });

    /// <summary>Body for <see cref="AuditEventType.PolicyEnforcementBlocked"/>.</summary>
    public static AuditPayload PolicyEnforcementBlocked(
        string featureKey,
        string jurisdictionCode,
        string ruleId,
        string enforcementAction) =>
        new(new Dictionary<string, object?>
        {
            ["enforcement_action"] = enforcementAction,
            ["feature_key"] = featureKey,
            ["jurisdiction_code"] = jurisdictionCode,
            ["rule_id"] = ruleId,
        });

    /// <summary>Body for <see cref="AuditEventType.JurisdictionProbedWithLowConfidence"/>.</summary>
    public static AuditPayload JurisdictionProbedWithLowConfidence(
        string jurisdictionCode,
        int signalCount) =>
        new(new Dictionary<string, object?>
        {
            ["jurisdiction_code"] = jurisdictionCode,
            ["signal_count"] = signalCount,
        });

    /// <summary>Body for <see cref="AuditEventType.DataResidencyViolation"/>.</summary>
    public static AuditPayload DataResidencyViolation(
        string recordClass,
        string jurisdictionCode,
        string detail) =>
        new(new Dictionary<string, object?>
        {
            ["detail"] = detail,
            ["jurisdiction_code"] = jurisdictionCode,
            ["record_class"] = recordClass,
        });

    /// <summary>Body for <see cref="AuditEventType.SanctionsScreeningHit"/>.</summary>
    public static AuditPayload SanctionsScreeningHit(
        string subjectId,
        string listSource,
        string listVersion,
        double matchScore) =>
        new(new Dictionary<string, object?>
        {
            ["list_source"] = listSource,
            ["list_version"] = listVersion,
            ["match_score"] = matchScore,
            ["subject_id"] = subjectId,
        });

    /// <summary>Body for <see cref="AuditEventType.RegimeAcknowledgmentSurfaced"/>.</summary>
    public static AuditPayload RegimeAcknowledgmentSurfaced(
        string regime,
        string stance) =>
        new(new Dictionary<string, object?>
        {
            ["regime"] = regime,
            ["stance"] = stance,
        });

    /// <summary>Body for <see cref="AuditEventType.EuAiActTierClassified"/>.</summary>
    public static AuditPayload EuAiActTierClassified(
        string featureKey,
        string tier) =>
        new(new Dictionary<string, object?>
        {
            ["feature_key"] = featureKey,
            ["tier"] = tier,
        });

    /// <summary>Body for <see cref="AuditEventType.SanctionsAdvisoryOnlyConfigured"/>.</summary>
    public static AuditPayload SanctionsAdvisoryOnlyConfigured(string operatorPrincipalId) =>
        new(new Dictionary<string, object?>
        {
            ["operator_principal_id"] = operatorPrincipalId,
        });

    /// <summary>Body for <see cref="AuditEventType.RegulatoryRuleContentReloaded"/>.</summary>
    public static AuditPayload RegulatoryRuleContentReloaded(string ruleSetVersion, int ruleCount) =>
        new(new Dictionary<string, object?>
        {
            ["rule_count"] = ruleCount,
            ["rule_set_version"] = ruleSetVersion,
        });

    /// <summary>Body for <see cref="AuditEventType.RegulatoryPolicyCacheInvalidated"/>.</summary>
    public static AuditPayload RegulatoryPolicyCacheInvalidated(string trigger) =>
        new(new Dictionary<string, object?>
        {
            ["trigger"] = trigger,
        });
}
