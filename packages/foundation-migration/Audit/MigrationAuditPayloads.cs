using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Migration.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#35 Foundation.Migration
/// audit-emission set (ADR 0028-A5/A8). Mirrors the
/// <c>VersionVectorAuditPayloads</c> + <c>TaxonomyAuditPayloadFactory</c>
/// + <c>FieldEncryptionAuditPayloadFactory</c> conventions: keys
/// alphabetized; bodies opaque to the substrate.
/// </summary>
public static class MigrationAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.HardwareTierChanged"/>.</summary>
    public static AuditPayload HardwareTierChanged(string nodeId, FormFactorKind previousFormFactor, FormFactorKind currentFormFactor, TriggeringEventKind trigger) =>
        new(new Dictionary<string, object?>
        {
            ["current_form_factor"] = currentFormFactor.ToString(),
            ["node_id"] = nodeId,
            ["previous_form_factor"] = previousFormFactor.ToString(),
            ["triggering_event"] = trigger.ToString(),
        });

    /// <summary>Body for <see cref="AuditEventType.PlaintextSequestered"/> + <see cref="AuditEventType.CiphertextSequestered"/>.</summary>
    public static AuditPayload Sequestered(string nodeId, string recordId, string requiredCapability, SequestrationFlagKind flag) =>
        new(new Dictionary<string, object?>
        {
            ["flag"] = flag.ToString(),
            ["node_id"] = nodeId,
            ["record_id"] = recordId,
            ["required_capability"] = requiredCapability,
        });

    /// <summary>Body for <see cref="AuditEventType.DataReleased"/>.</summary>
    public static AuditPayload DataReleased(string nodeId, string recordId, string requiredCapability) =>
        new(new Dictionary<string, object?>
        {
            ["node_id"] = nodeId,
            ["record_id"] = recordId,
            ["required_capability"] = requiredCapability,
        });

    /// <summary>Body for <see cref="AuditEventType.FormFactorQuorumIneligible"/>.</summary>
    public static AuditPayload FormFactorQuorumIneligible(string nodeId, string recordId, string requiredCapability) =>
        new(new Dictionary<string, object?>
        {
            ["node_id"] = nodeId,
            ["record_id"] = recordId,
            ["required_capability"] = requiredCapability,
        });

    /// <summary>Body for <see cref="AuditEventType.FieldWriteSequestered"/>.</summary>
    public static AuditPayload FieldWriteSequestered(string nodeId, string fieldEntryId, string requiredCapability) =>
        new(new Dictionary<string, object?>
        {
            ["field_entry_id"] = fieldEntryId,
            ["node_id"] = nodeId,
            ["required_capability"] = requiredCapability,
        });

    /// <summary>Body for <see cref="AuditEventType.AdapterRollbackDetected"/>.</summary>
    public static AuditPayload AdapterRollbackDetected(string nodeId, string adapterId, string previousVersion, string currentVersion) =>
        new(new Dictionary<string, object?>
        {
            ["adapter_id"] = adapterId,
            ["current_version"] = currentVersion,
            ["node_id"] = nodeId,
            ["previous_version"] = previousVersion,
        });
}
