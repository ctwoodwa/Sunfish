using System.Security.Cryptography;
using Sunfish.Bridge.Orchestration;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Keys;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Orchestration;

/// <summary>
/// Unit tests for Wave 5.2 stop-work #1 <see cref="TenantSeedProvider"/>.
/// Verifies that HKDF-derivation yields deterministic, tenant-isolated,
/// root-seed-independent 32-byte seeds — the cryptographic property that
/// closes the per-tenant isolation hole in
/// <see cref="TenantProcessSupervisor"/>.
/// </summary>
public sealed class TenantSeedProviderTests
{
    private static readonly Guid TenantA = new("11111111-2222-3333-4444-555555555555");
    private static readonly Guid TenantB = new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    /// <summary>
    /// Build a <see cref="TenantSeedProvider"/> backed by an
    /// <see cref="InMemoryKeystore"/> pre-seeded with a deterministic root
    /// seed so individual asserts can be reasoned about without chasing RNG
    /// state. The root-seed bytes are arbitrary; what matters is that they
    /// are identical across calls within one test.
    /// </summary>
    private static TenantSeedProvider BuildProvider(byte[]? rootSeed = null)
    {
        var seed = rootSeed ?? Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var keystore = new InMemoryKeystore();
        keystore.SetKeyAsync(KeystoreRootSeedProvider.SlotName, seed, CancellationToken.None)
            .GetAwaiter().GetResult();
        var root = new KeystoreRootSeedProvider(keystore);
        return new TenantSeedProvider(root);
    }

    [Fact]
    public void Deterministic_for_same_tenant()
    {
        var provider = BuildProvider();

        var first = provider.DeriveTenantSeed(TenantA);
        var second = provider.DeriveTenantSeed(TenantA);

        Assert.Equal(32, first.Length);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Distinct_for_different_tenants()
    {
        var provider = BuildProvider();

        var a = provider.DeriveTenantSeed(TenantA);
        var b = provider.DeriveTenantSeed(TenantB);

        Assert.Equal(32, a.Length);
        Assert.Equal(32, b.Length);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Distinct_from_root_seed()
    {
        // Use a known-distinctive root seed so the assert is meaningful (a
        // provider that trivially returns its input would match).
        var rootSeed = RandomNumberGenerator.GetBytes(32);
        var provider = BuildProvider(rootSeed);

        var tenantSeed = provider.DeriveTenantSeed(TenantA);

        Assert.Equal(32, tenantSeed.Length);
        Assert.NotEqual(rootSeed, tenantSeed);
    }

    [Fact]
    public void Thousand_tenants_have_unique_seeds()
    {
        // Analogue of the Wave 6.1 1000-team uniqueness test: generate 1000
        // random tenant ids, derive 1000 seeds, assert the hex set has
        // exactly 1000 members. Collision would indicate either a weak KDF
        // or a bug in the info-label construction.
        var provider = BuildProvider();
        var tenantIds = Enumerable.Range(0, 1000)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var seedHexes = tenantIds
            .Select(id => Convert.ToHexString(provider.DeriveTenantSeed(id)))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(1000, seedHexes.Count);
    }

    [Fact]
    public void Allows_empty_tenantId()
    {
        // Guid.Empty is allowed — it is a legitimate info-label input. The
        // supervisor layer validates that an id refers to a real tenant
        // registration; this provider is a pure derivation surface.
        var provider = BuildProvider();

        var seed = provider.DeriveTenantSeed(Guid.Empty);

        Assert.Equal(32, seed.Length);
    }

    [Fact]
    public void Rejects_null_rootSeedProvider_ctor()
    {
        Assert.Throws<ArgumentNullException>(
            () => new TenantSeedProvider(null!));
    }
}
