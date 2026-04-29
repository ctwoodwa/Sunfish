using System.Security.Cryptography;
using System.Text;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.TenantKey;

/// <summary>
/// W#20 Phase 0 stub implementation — derives keys deterministically via
/// HKDF-SHA256 over the UTF-8 bytes of <c>(tenantId.Value || purpose)</c>
/// with a fixed development salt. **NOT secure for production** — every
/// installation that runs this stub derives the same keys for the same
/// inputs. ADR 0046 Stage 06 replaces with real tenant-key-hierarchy
/// derivation (per-tenant DEK from KEK; KEK from operator master key).
/// </summary>
public sealed class InMemoryTenantKeyProvider : ITenantKeyProvider
{
    private static readonly byte[] DevelopmentSalt = Encoding.UTF8.GetBytes("sunfish-phase-1-stub-not-for-production");

    /// <inheritdoc />
    public Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        // TenantId is a string-wrapper; build IKM as UTF-8(tenant.Value || ":" || purpose).
        var separator = (byte)':';
        var tenantBytes = Encoding.UTF8.GetByteCount(tenant.Value);
        var purposeBytes = Encoding.UTF8.GetByteCount(purpose);
        var ikm = new byte[tenantBytes + 1 + purposeBytes];
        Encoding.UTF8.GetBytes(tenant.Value, ikm.AsSpan(0, tenantBytes));
        ikm[tenantBytes] = separator;
        Encoding.UTF8.GetBytes(purpose, ikm.AsSpan(tenantBytes + 1));

        var key = new byte[32];
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm,
            key,
            DevelopmentSalt,
            ReadOnlySpan<byte>.Empty);

        return Task.FromResult<ReadOnlyMemory<byte>>(key);
    }
}
