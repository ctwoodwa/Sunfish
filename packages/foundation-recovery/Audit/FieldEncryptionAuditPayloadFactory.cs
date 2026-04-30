using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.Crypto;
using Sunfish.Kernel.Audit;

namespace Sunfish.Foundation.Recovery.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the field-encryption
/// substrate (ADR 0046-A4 + A5). Mirrors the pattern of
/// <c>Sunfish.Foundation.Taxonomy.Audit.TaxonomyAuditPayloadFactory</c>:
/// the caller signs the payload via <see cref="Sunfish.Foundation.Crypto.IOperationSigner"/>
/// and constructs the <see cref="AuditRecord"/>.
/// </summary>
internal static class FieldEncryptionAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.FieldDecrypted"/>.</summary>
    public static AuditPayload Decrypted(IDecryptCapability capability, TenantId tenant, int keyVersion) =>
        new(new Dictionary<string, object?>
        {
            ["capability_id"] = capability.CapabilityId,
            ["key_version"] = keyVersion,
            ["tenant"] = tenant.Value,
        });

    /// <summary>Body for <see cref="AuditEventType.FieldDecryptionDenied"/>.</summary>
    public static AuditPayload DecryptionDenied(IDecryptCapability capability, TenantId tenant, string reason) =>
        new(new Dictionary<string, object?>
        {
            ["capability_id"] = capability.CapabilityId,
            ["reason"] = reason,
            ["tenant"] = tenant.Value,
        });
}
