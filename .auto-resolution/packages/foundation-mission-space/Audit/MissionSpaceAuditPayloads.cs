using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.MissionSpace.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#40 Foundation.MissionSpace
/// audit-emission set (ADR 0062-A1.4 / A1.7 / A1.9 / A1.12). Mirrors the
/// <c>VersionVectorAuditPayloads</c> + <c>MigrationAuditPayloads</c>
/// conventions: keys alphabetized; bodies opaque to the substrate.
/// </summary>
public static class MissionSpaceAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.FeatureProbed"/>.</summary>
    public static AuditPayload FeatureProbed(string dimension, string probeStatus, double durationMs) =>
        new(new Dictionary<string, object?>
        {
            ["dimension"] = dimension,
            ["duration_ms"] = durationMs,
            ["probe_status"] = probeStatus,
        });

    /// <summary>Body for <see cref="AuditEventType.FeatureProbeFailed"/>.</summary>
    public static AuditPayload FeatureProbeFailed(string dimension, string failureReason, double durationMs) =>
        new(new Dictionary<string, object?>
        {
            ["dimension"] = dimension,
            ["duration_ms"] = durationMs,
            ["failure_reason"] = failureReason,
        });

    /// <summary>Body for <see cref="AuditEventType.FeatureForceEnabled"/>.</summary>
    public static AuditPayload FeatureForceEnabled(
        string featureKey,
        string dimension,
        string operatorPrincipalId,
        string? expiresAt,
        string? reason) =>
        new(new Dictionary<string, object?>
        {
            ["dimension"] = dimension,
            ["expires_at"] = expiresAt,
            ["feature_key"] = featureKey,
            ["operator_principal_id"] = operatorPrincipalId,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.FeatureForceRevoked"/>.</summary>
    public static AuditPayload FeatureForceRevoked(
        string featureKey,
        string dimension,
        string operatorPrincipalId) =>
        new(new Dictionary<string, object?>
        {
            ["dimension"] = dimension,
            ["feature_key"] = featureKey,
            ["operator_principal_id"] = operatorPrincipalId,
        });

    /// <summary>Body for <see cref="AuditEventType.FeatureForceEnableRejected"/>.</summary>
    public static AuditPayload FeatureForceEnableRejected(
        string featureKey,
        string dimension,
        string operatorPrincipalId,
        string policy,
        string? reason) =>
        new(new Dictionary<string, object?>
        {
            ["dimension"] = dimension,
            ["feature_key"] = featureKey,
            ["operator_principal_id"] = operatorPrincipalId,
            ["policy"] = policy,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.MissionEnvelopeChangeBroadcast"/>.</summary>
    public static AuditPayload EnvelopeChangeBroadcast(
        int changedDimensionCount,
        string envelopeHash,
        string severity) =>
        new(new Dictionary<string, object?>
        {
            ["changed_dimension_count"] = changedDimensionCount,
            ["envelope_hash"] = envelopeHash,
            ["severity"] = severity,
        });

    /// <summary>Body for <see cref="AuditEventType.MissionEnvelopeObserverOverflow"/>.</summary>
    public static AuditPayload ObserverOverflow(int maxPending) =>
        new(new Dictionary<string, object?>
        {
            ["max_pending"] = maxPending,
        });

    /// <summary>Body for <see cref="AuditEventType.FeatureVerdictSurfaced"/>.</summary>
    public static AuditPayload FeatureVerdictSurfaced(
        string featureKey,
        string availabilityState,
        string? degradationKind,
        string envelopeHash) =>
        new(new Dictionary<string, object?>
        {
            ["availability_state"] = availabilityState,
            ["degradation_kind"] = degradationKind,
            ["envelope_hash"] = envelopeHash,
            ["feature_key"] = featureKey,
        });

    // ===== ADR 0063 — Foundation.MissionSpace.Requirements (W#41) =====

    /// <summary>Body for <see cref="AuditEventType.MinimumSpecEvaluated"/>.</summary>
    public static AuditPayload MinimumSpecEvaluated(
        string envelopeHash,
        string overall,
        int dimensionCount,
        string? platform) =>
        new(new Dictionary<string, object?>
        {
            ["dimension_count"] = dimensionCount,
            ["envelope_hash"] = envelopeHash,
            ["overall"] = overall,
            ["platform"] = platform,
        });

    /// <summary>Body for <see cref="AuditEventType.InstallBlocked"/>.</summary>
    public static AuditPayload InstallBlocked(
        string envelopeHash,
        string failingDimension,
        string? platform) =>
        new(new Dictionary<string, object?>
        {
            ["envelope_hash"] = envelopeHash,
            ["failing_dimension"] = failingDimension,
            ["platform"] = platform,
        });

    /// <summary>Body for <see cref="AuditEventType.InstallWarned"/>.</summary>
    public static AuditPayload InstallWarned(
        string envelopeHash,
        string warningDimension,
        string? platform) =>
        new(new Dictionary<string, object?>
        {
            ["envelope_hash"] = envelopeHash,
            ["platform"] = platform,
            ["warning_dimension"] = warningDimension,
        });

    /// <summary>
    /// Body for <see cref="AuditEventType.InstallForceEnabled"/>. Per A1.11
    /// council fix — shape mirrors <c>FeatureForceEnabled</c> for parity with
    /// W#40's force-enable surface (operator_principal_id + reason +
    /// override_targets).
    /// </summary>
    public static AuditPayload InstallForceEnabled(
        string operatorPrincipalId,
        string reason,
        IReadOnlyList<string> overrideTargets,
        string envelopeHash,
        string? platform) =>
        new(new Dictionary<string, object?>
        {
            ["envelope_hash"] = envelopeHash,
            ["operator_principal_id"] = operatorPrincipalId,
            ["override_targets"] = overrideTargets,
            ["platform"] = platform,
            ["reason"] = reason,
        });

    /// <summary>Body for <see cref="AuditEventType.PostInstallSpecRegression"/>.</summary>
    public static AuditPayload PostInstallSpecRegression(
        string regressedDimension,
        string previousOutcome,
        string currentOutcome,
        string envelopeHash) =>
        new(new Dictionary<string, object?>
        {
            ["current_outcome"] = currentOutcome,
            ["envelope_hash"] = envelopeHash,
            ["previous_outcome"] = previousOutcome,
            ["regressed_dimension"] = regressedDimension,
        });
}
