namespace Sunfish.Anchor.Services;

/// <summary>
/// W#67 / ADR 0046-A6 — single-use, key-name-scoped store for the
/// recovering device's ephemeral X25519 private key. The private key is
/// generated at <c>InitiateRecoveryPage</c> click time, persisted here
/// during the request flow, and read back by
/// <see cref="AnchorRecoveryCompletionHandler"/> to <c>OpenBox</c> the
/// trustee-delivered seed envelopes once quorum + grace elapse.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a new abstraction.</b> MAUI's <c>SecureStorage</c> is the
/// production backing store (DPAPI on Windows, Keychain on Mac/iOS), but
/// it is part of the MAUI workload. The Anchor tests project is
/// deliberately MAUI-free (see <c>CrewChatPageTests.cs</c>), so the
/// completion handler can't take a direct <c>SecureStorage</c>
/// dependency. This interface keeps the handler MAUI-free; the MAUI
/// composition root binds it to <c>MauiSecureStorageEphemeralKeyStore</c>
/// and the tests bind it to <see cref="InMemoryEphemeralRecoveryKeyStore"/>.
/// </para>
/// <para>
/// <b>Key name convention.</b> Production uses
/// <c>recovery:dh-priv</c> (per the hand-off / ADR 0046-A6). Multiple
/// concurrent recoveries are not supported in Phase 1 — the coordinator
/// rejects a second <c>InitiateRecoveryAsync</c> while one is in flight.
/// </para>
/// </remarks>
public interface IEphemeralRecoveryKeyStore
{
    /// <summary>The X25519 private-key slot name used by the recovery flow.</summary>
    public const string RecoveryDhPrivateKeyName = "recovery:dh-priv";

    /// <summary>
    /// Persist the ephemeral private key under <paramref name="keyName"/>.
    /// Overwrites any prior value.
    /// </summary>
    Task SetAsync(string keyName, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the bytes previously written under <paramref name="keyName"/>,
    /// or <c>null</c> if no value is present (e.g., the device was wiped
    /// between initiation and completion).
    /// </summary>
    Task<byte[]?> GetAsync(string keyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove the value under <paramref name="keyName"/>. No-op if the
    /// key is not present. The completion handler calls this after a
    /// successful rekey so the ephemeral private key does not linger.
    /// </summary>
    Task RemoveAsync(string keyName, CancellationToken cancellationToken = default);
}
