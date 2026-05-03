using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Decrypts an <see cref="EncryptedField"/> after validating an
/// <see cref="IDecryptCapability"/> per ADR 0046-A2/A3. Every successful
/// decrypt and every denial emits an audit record (per ADR 0046-A4).
/// </summary>
public interface IFieldDecryptor
{
    /// <summary>
    /// Validate <paramref name="capability"/> for the given tenant + clock
    /// and, if valid, AES-GCM-decrypt <paramref name="field"/> using the
    /// per-tenant DEK at <see cref="EncryptedField.KeyVersion"/>.
    /// </summary>
    /// <exception cref="FieldDecryptionDeniedException">
    /// Capability rejected, ciphertext truncated, AES-GCM tag verification
    /// failed, or unsupported <see cref="EncryptedField.KeyVersion"/>.
    /// </exception>
    Task<ReadOnlyMemory<byte>> DecryptAsync(
        EncryptedField field,
        IDecryptCapability capability,
        TenantId tenant,
        CancellationToken ct);
}
