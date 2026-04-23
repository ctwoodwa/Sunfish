using System.Security.Cryptography;
using System.Text;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// Default <see cref="ITenantSeedProvider"/> implementation. Pulls the
/// install-level root seed from the supplied <see cref="IRootSeedProvider"/>
/// once (lazy), then HKDF-Expands it per tenant on each call.
/// </summary>
/// <remarks>
/// <para>
/// <b>Root-seed caching.</b> The underlying <see cref="IRootSeedProvider"/>
/// already caches the seed in-process (Lazy{Task}), but we cache again here
/// behind a <see cref="Lazy{T}"/> wrapper to collapse the async contract to
/// a synchronous API — <c>DeriveTenantSeed</c> must not block on I/O after
/// first call, and callers invoke it from <see cref="TenantProcessSupervisor"/>
/// which holds a per-tenant lock and should not await.
/// </para>
/// <para>
/// <b>Blocking first call.</b> We resolve the root seed via
/// <c>GetAwaiter().GetResult()</c> on first derivation. The keystore read is
/// a single local round-trip (Windows DPAPI / InMemoryKeystore in tests) with
/// no ambient SynchronizationContext inside the Bridge host. This matches
/// the pattern in <c>apps/local-node-host/Program.cs</c> which does the same
/// at startup.
/// </para>
/// <para>
/// <b>Thread-safety.</b> <see cref="HKDF"/> is stateless and
/// <see cref="Lazy{T}"/> uses
/// <see cref="LazyThreadSafetyMode.ExecutionAndPublication"/>. Concurrent
/// <c>DeriveTenantSeed</c> calls are therefore safe — the first caller pays
/// the I/O cost and every subsequent caller sees the cached seed.
/// </para>
/// </remarks>
public sealed class TenantSeedProvider : ITenantSeedProvider
{
    /// <summary>
    /// HKDF info-label prefix. The full label appended to each tenant is
    /// <c>"sunfish:bridge:tenant-seed:v1:" + tenantId.ToString("D")</c>.
    /// </summary>
    public const string InfoPrefix = "sunfish:bridge:tenant-seed:v1:";

    /// <summary>Length of an Ed25519 seed, in bytes.</summary>
    public const int SeedLength = 32;

    private readonly Lazy<ReadOnlyMemory<byte>> _rootSeed;

    /// <summary>
    /// Construct a provider bound to the supplied root-seed source.
    /// </summary>
    /// <param name="rootSeedProvider">The install-level root-seed provider —
    /// typically <see cref="KeystoreRootSeedProvider"/> in production and
    /// a test double backed by <see cref="InMemoryKeystore"/> in unit
    /// tests.</param>
    public TenantSeedProvider(IRootSeedProvider rootSeedProvider)
    {
        ArgumentNullException.ThrowIfNull(rootSeedProvider);

        // Collapse the async IRootSeedProvider contract to a sync memoized
        // value. Safe at Bridge startup (no SynchronizationContext; keystore
        // call is local); see class remarks.
        _rootSeed = new Lazy<ReadOnlyMemory<byte>>(
            () => rootSeedProvider
                .GetRootSeedAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult(),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    public byte[] DeriveTenantSeed(Guid tenantId)
    {
        var rootSeed = _rootSeed.Value;

        // Info label. "D" format yields the canonical 36-char hyphenated GUID
        // (e.g. "11111111-2222-3333-4444-555555555555") — stable across .NET
        // versions and culture-invariant.
        var info = Encoding.UTF8.GetBytes(InfoPrefix + tenantId.ToString("D"));

        Span<byte> output = stackalloc byte[SeedLength];
        HKDF.Expand(HashAlgorithmName.SHA256, rootSeed.Span, output, info);
        return output.ToArray();
    }
}
