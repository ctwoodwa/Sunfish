using System.Collections.Generic;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Versioning.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the W#34 Foundation.Versioning
/// audit-emission set (ADR 0028-A6 / A7.4). Mirrors the
/// <c>TaxonomyAuditPayloadFactory</c> + <c>FieldEncryptionAuditPayloadFactory</c>
/// conventions: keys alphabetized; bodies opaque to the substrate.
/// </summary>
public static class VersionVectorAuditPayloads
{
    /// <summary>Body for <see cref="AuditEventType.VersionVectorIncompatibilityRejected"/>.</summary>
    public static AuditPayload IncompatibilityRejected(string remoteNodeId, FailedRule rule, string detail) =>
        new(new Dictionary<string, object?>
        {
            ["failed_rule"] = rule.ToString(),
            ["failed_rule_detail"] = detail,
            ["remote_node_id"] = remoteNodeId,
        });

    /// <summary>Body for <see cref="AuditEventType.LegacyDeviceReconnected"/>.</summary>
    public static AuditPayload LegacyReconnected(string remoteNodeId, string remoteKernel, int kernelMinorLag) =>
        new(new Dictionary<string, object?>
        {
            ["kernel_minor_lag"] = kernelMinorLag,
            ["remote_kernel"] = remoteKernel,
            ["remote_node_id"] = remoteNodeId,
        });
}
