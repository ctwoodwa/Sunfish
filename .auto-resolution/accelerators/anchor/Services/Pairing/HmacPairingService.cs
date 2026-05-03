using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Recovery.TenantKey;

namespace Sunfish.Anchor.Services.Pairing;

/// <summary>
/// Per W#23 Phase 0 — reference HMAC-SHA256 <see cref="IPairingService"/>.
/// Mirrors the W#18 vendor-magic-link HMAC pattern; per-tenant key derived
/// via <c>ITenantKeyProvider.DeriveKeyAsync</c> with purpose
/// <c>field-pairing-token-hmac</c>.
/// </summary>
/// <remarks>
/// <para>
/// In-memory token store — Phase 0 substrate is sufficient for pairing flow
/// validation; durability lands in a follow-up phase if needed (the token
/// is short-lived and single-use, so process restart simply invalidates
/// outstanding tokens — operators re-issue).
/// </para>
/// </remarks>
public sealed class HmacPairingService : IPairingService
{
    /// <summary>Per-tenant HMAC purpose label per W#23 Phase 0 (mirrors W#18 <c>vendor-magic-link-hmac</c>).</summary>
    public const string HmacKeyPurpose = "field-pairing-token-hmac";

    /// <summary>Default token TTL — 10 minutes (operator pairs the device live).</summary>
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(10);

    private readonly ITenantKeyProvider _keyProvider;
    private readonly TimeProvider _time;
    private readonly TimeSpan _ttl;

    private readonly ConcurrentDictionary<(TenantId Tenant, string PairingTokenId), PairingToken> _store = new();
    private readonly ConcurrentDictionary<(TenantId Tenant, string PairingTokenId), bool> _revoked = new();

    public HmacPairingService(ITenantKeyProvider keyProvider, TimeProvider? time = null, TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(keyProvider);
        _keyProvider = keyProvider;
        _time = time ?? TimeProvider.System;
        _ttl = ttl ?? DefaultTtl;
    }

    /// <inheritdoc />
    public async Task<PairingToken> IssuePairingTokenAsync(TenantId tenant, string deviceId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(deviceId);
        ct.ThrowIfCancellationRequested();

        var idBytes = RandomNumberGenerator.GetBytes(16);
        var pairingTokenId = ToBase32(idBytes);
        var issuedAt = _time.GetUtcNow();
        var expiresAt = issuedAt + _ttl;

        var key = await _keyProvider.DeriveKeyAsync(tenant, HmacKeyPurpose, ct).ConfigureAwait(false);
        var hmac = ToBase32(ComputeHmac(key.Span, pairingTokenId, deviceId, issuedAt));

        var token = new PairingToken(
            PairingTokenId: pairingTokenId,
            DeviceId: deviceId,
            IssuedAt: issuedAt,
            ExpiresAt: expiresAt,
            ConsumedAt: null,
            Hmac: hmac);

        _store[(tenant, pairingTokenId)] = token;
        return token;
    }

    /// <inheritdoc />
    public async Task<PairingToken?> ConsumePairingTokenAsync(TenantId tenant, PairingToken presented, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ct.ThrowIfCancellationRequested();

        var key = (tenant, presented.PairingTokenId);
        if (_revoked.ContainsKey(key))
        {
            return null;
        }
        if (!_store.TryGetValue(key, out var stored))
        {
            return null;
        }
        if (stored.ConsumedAt is not null)
        {
            return null; // single-use
        }

        var now = _time.GetUtcNow();
        if (now >= stored.ExpiresAt)
        {
            return null; // expired
        }

        // Recompute the HMAC and constant-time compare.
        var derived = await _keyProvider.DeriveKeyAsync(tenant, HmacKeyPurpose, ct).ConfigureAwait(false);
        var expected = ToBase32(ComputeHmac(derived.Span, stored.PairingTokenId, stored.DeviceId, stored.IssuedAt));
        var presentedBytes = Encoding.ASCII.GetBytes(presented.Hmac);
        var expectedBytes = Encoding.ASCII.GetBytes(expected);
        if (presentedBytes.Length != expectedBytes.Length
            || !CryptographicOperations.FixedTimeEquals(presentedBytes, expectedBytes))
        {
            return null;
        }

        // Bind the consumed device id to what's stored — defends against the
        // caller swapping device_id between issue and consume.
        if (!string.Equals(presented.DeviceId, stored.DeviceId, StringComparison.Ordinal))
        {
            return null;
        }

        var consumed = stored with { ConsumedAt = now };
        _store[key] = consumed;
        return consumed;
    }

    /// <inheritdoc />
    public Task RevokePairingAsync(TenantId tenant, string pairingTokenId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(pairingTokenId);
        ct.ThrowIfCancellationRequested();
        _revoked[(tenant, pairingTokenId)] = true;
        return Task.CompletedTask;
    }

    private static byte[] ComputeHmac(ReadOnlySpan<byte> key, string pairingTokenId, string deviceId, DateTimeOffset issuedAt)
    {
        var iso = issuedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);
        var input = $"{pairingTokenId}:{deviceId}:{iso}";
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(input));
    }

    // RFC 4648 base32 (uppercase, no padding) — minimal implementation;
    // mirrors HmacThreadTokenIssuer.ToBase32 from W#20.
    private static readonly char[] Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567".ToCharArray();

    private static string ToBase32(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty) return string.Empty;
        var sb = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                sb.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }
        if (bitsLeft > 0)
        {
            sb.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }
        return sb.ToString();
    }
}
