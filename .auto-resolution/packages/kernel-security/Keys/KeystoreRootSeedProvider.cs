using System.Security.Cryptography;
using Sunfish.Foundation.LocalFirst.Encryption;

namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// <see cref="IRootSeedProvider"/> backed by an <see cref="IKeystore"/> slot.
/// First-call-of-first-launch generates a 32-byte RNG seed and persists it;
/// subsequent calls return the cached in-process value without re-hitting the
/// keystore.
/// </summary>
/// <remarks>
/// <para>
/// Slot name <c>"sunfish:root-seed:v1"</c> is namespaced separately from the
/// per-team subkey slots (<c>"sunfish:team:{teamId}:primary"</c>) and from the
/// pre-existing <c>"sunfish-primary"</c> slot used by older Wave-2 code paths,
/// so no consumer can collide with this provider's storage.
/// </para>
/// <para>
/// Concurrency: the first call materializes a <see cref="Lazy{T}"/>
/// <see cref="Task{TResult}"/> under
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>. Concurrent first
/// callers therefore share a single <c>GetKeyAsync</c> / <c>SetKeyAsync</c>
/// round-trip; the RNG draw happens exactly once per process-lifetime of this
/// provider.
/// </para>
/// <para>
/// Failure modes: if <see cref="IKeystore.SetKeyAsync"/> throws (for example
/// <see cref="PlatformNotSupportedException"/> from the Wave-2 macOS / Linux
/// stubs in <see cref="Keystore.CreateForCurrentPlatform(string?)"/>), the
/// exception propagates. Per-install isolation requires the keystore to work,
/// so silently falling back to an on-disk or in-memory seed would be a
/// correctness regression. The Windows DPAPI happy path is the only one that
/// must succeed today; mac/Linux support lands with the Wave-2 keystore work.
/// </para>
/// </remarks>
public sealed class KeystoreRootSeedProvider : IRootSeedProvider
{
    /// <summary>Keystore slot name for the install's root seed (v1 derivation).</summary>
    public const string SlotName = "sunfish:root-seed:v1";

    /// <summary>Length of an Ed25519 root seed, in bytes.</summary>
    public const int SeedLength = 32;

    private readonly IKeystore _keystore;
    private readonly object _gate = new();
    private Lazy<Task<ReadOnlyMemory<byte>>>? _cached;

    /// <summary>
    /// Construct a provider bound to the supplied keystore.
    /// </summary>
    /// <param name="keystore">Platform keystore. Callers typically inject the
    /// one produced by <see cref="Keystore.CreateForCurrentPlatform(string?)"/>;
    /// tests pass <see cref="InMemoryKeystore"/>.</param>
    public KeystoreRootSeedProvider(IKeystore keystore)
    {
        _keystore = keystore ?? throw new ArgumentNullException(nameof(keystore));
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>> GetRootSeedAsync(CancellationToken ct)
    {
        var lazy = _cached;
        if (lazy is null)
        {
            lock (_gate)
            {
                lazy = _cached ??= new Lazy<Task<ReadOnlyMemory<byte>>>(
                    () => ResolveAsync(ct),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }

        return new ValueTask<ReadOnlyMemory<byte>>(lazy.Value);
    }

    private async Task<ReadOnlyMemory<byte>> ResolveAsync(CancellationToken ct)
    {
        var existing = await _keystore.GetKeyAsync(SlotName, ct).ConfigureAwait(false);
        if (existing is { } buf && buf.Length == SeedLength)
        {
            // Happy path after first launch: the install's seed was already
            // provisioned; return it verbatim.
            return buf;
        }

        // Either no slot yet (fresh install) or a slot with the wrong length
        // (corrupt / partially-written blob from an aborted provisioning).
        // Regenerate via RNG and overwrite. We do not attempt to recover the
        // prior bytes — a wrong-length slot is meaningless as an Ed25519 seed.
        var seed = RandomNumberGenerator.GetBytes(SeedLength);
        await _keystore.SetKeyAsync(SlotName, seed, ct).ConfigureAwait(false);
        return seed;
    }
}
