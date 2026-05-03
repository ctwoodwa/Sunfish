using System;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Thrown when <see cref="IFieldDecryptor.DecryptAsync"/> rejects a
/// decrypt request. The capability id and short reason are also written
/// to the denial <see cref="Sunfish.Kernel.Audit.AuditRecord"/> when an
/// audit trail is wired (ADR 0046-A4).
/// </summary>
public sealed class FieldDecryptionDeniedException : Exception
{
    public FieldDecryptionDeniedException(string capabilityId, string reason)
        : base($"Field decryption denied (capability='{capabilityId}'): {reason}")
    {
        CapabilityId = capabilityId;
        Reason = reason;
    }

    public string CapabilityId { get; }

    public string Reason { get; }
}
