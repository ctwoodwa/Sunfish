using System;
using System.Buffers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;

namespace Sunfish.Foundation.Recovery.Crypto;

/// <summary>
/// Reference <see cref="IFieldEncryptor"/> per ADR 0046-A4. Delegates
/// per-tenant DEK derivation to <see cref="ITenantKeyProvider"/>
/// (purpose label <c>"encrypted-field-aes"</c>) and AES-GCM-encrypts
/// with a fresh 12-byte random nonce + 16-byte tag.
/// </summary>
public sealed class TenantKeyProviderFieldEncryptor : IFieldEncryptor
{
    internal const string PurposeLabel = "encrypted-field-aes";
    internal const int CurrentKeyVersion = 1;
    internal const int NonceLength = 12;
    internal const int TagLength = 16;

    private readonly ITenantKeyProvider _tenantKeys;

    public TenantKeyProviderFieldEncryptor(ITenantKeyProvider tenantKeys)
    {
        ArgumentNullException.ThrowIfNull(tenantKeys);
        _tenantKeys = tenantKeys;
    }

    public async Task<EncryptedField> EncryptAsync(
        ReadOnlyMemory<byte> plaintext,
        TenantId tenant,
        CancellationToken ct)
    {
        var dek = await _tenantKeys.DeriveKeyAsync(tenant, PurposeLabel, ct).ConfigureAwait(false);

        var nonce = new byte[NonceLength];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagLength];
        using (var aes = new AesGcm(dek.Span, tagSizeInBytes: TagLength))
        {
            aes.Encrypt(nonce, plaintext.Span, ciphertext, tag);
        }

        var packed = new byte[ciphertext.Length + tag.Length];
        Buffer.BlockCopy(ciphertext, 0, packed, 0, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, packed, ciphertext.Length, tag.Length);
        return new EncryptedField(packed, nonce, CurrentKeyVersion);
    }
}
