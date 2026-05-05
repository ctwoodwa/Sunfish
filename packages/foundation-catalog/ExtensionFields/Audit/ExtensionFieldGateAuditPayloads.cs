using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Catalog.ExtensionFields.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the ADR 0075 extension-field
/// feature-gate audit-emission set. Mirrors the
/// <see cref="Sunfish.Foundation.Migration.Audit.MigrationAuditPayloads"/>
/// convention: keys alphabetized; bodies opaque to the substrate. Per
/// ADR 0075 §Audit emission.
/// </summary>
public static class ExtensionFieldGateAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.ExtensionFieldGated"/>.</summary>
    public static AuditPayload Gated(string entityTypeFullName, string fieldKey, string featureKey) =>
        new(new Dictionary<string, object?>
        {
            ["entity_type"] = entityTypeFullName,
            ["feature_key"] = featureKey,
            ["field_key"] = fieldKey,
        });

    /// <summary>Body for <see cref="AuditEventType.ExtensionFieldFiltered"/>.</summary>
    public static AuditPayload Filtered(string entityTypeFullName, string fieldKey, string featureKey) =>
        new(new Dictionary<string, object?>
        {
            ["entity_type"] = entityTypeFullName,
            ["feature_key"] = featureKey,
            ["field_key"] = fieldKey,
        });

    /// <summary>Body for <see cref="AuditEventType.ExtensionFieldSequestered"/>.</summary>
    public static AuditPayload Sequestered(
        string entityTypeFullName, string fieldKey, string featureKey, string nodeId) =>
        new(new Dictionary<string, object?>
        {
            ["entity_type"] = entityTypeFullName,
            ["feature_key"] = featureKey,
            ["field_key"] = fieldKey,
            ["node_id"] = nodeId,
        });

    /// <summary>Body for <see cref="AuditEventType.ExtensionFieldRedacted"/>.</summary>
    public static AuditPayload Redacted(
        string entityTypeFullName, string fieldKey, string featureKey, bool capabilityGranted) =>
        new(new Dictionary<string, object?>
        {
            ["capability_granted"] = capabilityGranted,
            ["entity_type"] = entityTypeFullName,
            ["feature_key"] = featureKey,
            ["field_key"] = fieldKey,
        });

    /// <summary>Body for <see cref="AuditEventType.ExtensionFieldGateEvaluationFailed"/>.</summary>
    public static AuditPayload GateEvaluationFailed(
        string entityTypeFullName, string fieldKey, string featureKey, string exceptionMessage) =>
        new(new Dictionary<string, object?>
        {
            ["entity_type"] = entityTypeFullName,
            ["exception_message"] = exceptionMessage,
            ["feature_key"] = featureKey,
            ["field_key"] = fieldKey,
        });
}
