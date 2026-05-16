namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Single-use write path for restoring the install's root seed after a
/// successful social-recovery grace period (ADR 0046-A6). The recovering
/// device decrypts the trustee-delivered seed envelope and calls
/// <see cref="RestoreRootSeedAsync"/> exactly once to persist the seed
/// into the platform keystore. Subsequent <see cref="IRootSeedProvider.GetRootSeedAsync"/>
/// calls return the restored seed, unblocking SQLCipher rekey and full
/// per-team subkey re-derivation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Injection discipline.</b> Only
/// <c>Sunfish.Anchor.Services.AnchorRecoveryCompletionHandler</c> should
/// take a dependency on this interface. Splitting the write surface from
/// <see cref="IRootSeedProvider"/> (read-only) keeps the rest of the code
/// base unable to overwrite the install's seed even by accident — the
/// only consumer that needs the write path is the recovery completion
/// handler.
/// </para>
/// <para>
/// <b>Idempotency.</b> Implementations may be called multiple times if
/// the completion handler retries; the keystore slot is overwritten on
/// each call. The provider's in-process seed cache (see
/// <see cref="KeystoreRootSeedProvider"/>) is invalidated so the next
/// <c>GetRootSeedAsync</c> returns the freshly-written value rather than
/// a stale pre-recovery cached buffer.
/// </para>
/// </remarks>
public interface IRootSeedRestorer
{
    /// <summary>
    /// Persists <paramref name="recoveredSeed"/> as the install's new root
    /// seed and invalidates any in-process cache so subsequent reads
    /// observe the restored value. Throws if the seed length is not
    /// <see cref="KeystoreRootSeedProvider.SeedLength"/> (32 bytes).
    /// </summary>
    Task RestoreRootSeedAsync(ReadOnlyMemory<byte> recoveredSeed, CancellationToken ct);
}
