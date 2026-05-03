using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Recovery.TenantKey;

/// <summary>
/// Provides per-tenant key material for cryptographic operations (HMAC,
/// envelope encryption, etc.). W#20 Phase 0 stub (per W#20 hand-off
/// addendum); ADR 0046 Stage 06 will replace with real tenant-key
/// derivation backed by Foundation.Recovery's KEK hierarchy.
/// </summary>
public interface ITenantKeyProvider
{
    /// <summary>
    /// Derive a 32-byte key for the given tenant + purpose label.
    /// </summary>
    /// <param name="tenant">Tenant identity.</param>
    /// <param name="purpose">Purpose label — e.g., <c>thread-token-hmac</c> or <c>encrypted-field-aes</c>. Different purposes derive different keys for the same tenant.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>32-byte derived key (suitable for HMAC-SHA256 + AES-256).</returns>
    Task<ReadOnlyMemory<byte>> DeriveKeyAsync(TenantId tenant, string purpose, CancellationToken ct);
}
