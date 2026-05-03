using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Encrypts a scalar field at rest into an <see cref="EncryptedField"/>
/// envelope using a per-tenant DEK derived from the host's key substrate
/// (per ADR 0046-A2). Encryption is unrestricted — the host writes
/// ciphertext freely; the access boundary lives on the decrypt path
/// via <see cref="IFieldDecryptor"/>.
/// </summary>
public interface IFieldEncryptor
{
    /// <summary>
    /// Produce an <see cref="EncryptedField"/> envelope wrapping
    /// <paramref name="plaintext"/> for the given tenant.
    /// </summary>
    /// <param name="plaintext">Field bytes to encrypt; treated as opaque.</param>
    /// <param name="tenant">Tenant scope. The DEK is tenant-bound — different tenants produce different ciphertext for the same plaintext.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<EncryptedField> EncryptAsync(ReadOnlyMemory<byte> plaintext, TenantId tenant, CancellationToken ct);
}
